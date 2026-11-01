using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using ComponentFactory.Krypton.Toolkit;
using HoaDonDienTu.WinForms.Bootstrap;
using HoaDonDienTu.WinForms.Login;
using HoaDonDienTu.WinForms.Shell;
using NUnit.Framework;

namespace HoaDonDienTu.WinForms.Tests
{
    /// <summary>
    /// UI smoke test (DD-01 mục 19.6): dựng cửa sổ chính thật với adapter giả của Debug, bấm control thật bằng
    /// PerformClick và bơm message loop. Cần phiên làm việc có desktop tương tác; bỏ qua bằng
    /// <c>/TestCaseFilter:"TestCategory!=UiSmoke"</c> khi chạy trên máy không có desktop.
    /// </summary>
    [TestFixture]
    [Category("UiSmoke")]
    [Apartment(ApartmentState.STA)]
    [NonParallelizable]
    public class ShellUiSmokeTests
    {
        private CompositionRoot _root;
        private AppApplicationContext _context;
        private MainShellForm _form;
        private string _oldMode;
        private string _oldJob;

        [SetUp]
        public void SetUp()
        {
            _oldMode = Environment.GetEnvironmentVariable("HDDT_DEV_MODE");
            _oldJob = Environment.GetEnvironmentVariable("HDDT_DEV_JOB");
        }

        [TearDown]
        public void TearDown()
        {
            CloseOpenDialogs();
            if (_form != null && !_form.IsDisposed)
            {
                _form.Dispose();
            }

            if (_context != null) _context.Dispose();
            if (_root != null) _root.Dispose();
            Environment.SetEnvironmentVariable("HDDT_DEV_MODE", _oldMode);
            Environment.SetEnvironmentVariable("HDDT_DEV_JOB", _oldJob);
        }

        private void Start(string mode, string job = null)
        {
            Environment.SetEnvironmentVariable("HDDT_DEV_MODE", mode);
            Environment.SetEnvironmentVariable("HDDT_DEV_JOB", job);
            _root = CompositionRoot.Create();
            // Các test khác tự bấm đăng nhập; riêng test auto-login dùng StartWithAutoLogin.
            _root.AutoLoginOnStartup = false;
            _context = new AppApplicationContext(_root);
            _form = (MainShellForm)_context.MainForm;
            _form.Show();
            Assert.That(WaitFor(IsReady), Is.True, "Shell không khởi động xong");
        }

        private bool IsReady()
        {
            return ContentTitle() != null && ContentTitle() != "Đang khởi tạo...";
        }

        /// <summary>Khởi động với đăng nhập tự mở; <paramref name="script"/> điều khiển hộp thoại xuất hiện lúc khởi động.</summary>
        private void StartWithAutoLogin(string mode, Func<Form, bool> script)
        {
            Environment.SetEnvironmentVariable("HDDT_DEV_MODE", mode);
            Environment.SetEnvironmentVariable("HDDT_DEV_JOB", null);
            _root = CompositionRoot.Create();
            Assert.That(_root.AutoLoginOnStartup, Is.True, "Mặc định phải tự mở đăng nhập");
            _context = new AppApplicationContext(_root);
            _form = (MainShellForm)_context.MainForm;
            RunDialogScript(() =>
            {
                _form.Show();
                WaitFor(IsReady);
            }, script);
        }

        [Test]
        public void Standard_StartsOnDashboard_LoginAndDownloadAvailable()
        {
            Start("Standard");

            Assert.That(ContentTitle(), Is.EqualTo("Tổng quan"));
            Assert.That(Control<KryptonButton>("btnPortalLogin").Enabled, Is.True);
            Assert.That(Control<KryptonCheckButton>("navDownload").Enabled, Is.True);
            Assert.That(Control<KryptonCheckButton>("navCompanies").Visible, Is.False, "Standard ẩn menu doanh nghiệp");
            Assert.That(Control<KryptonButton>("btnCompanyContext").Values.Text, Does.StartWith("CÔNG TY MẪU"));
        }

        [Test]
        public void Standard_AutoOpensLoginAtStartup_CorrectTaxCodeLogsIn()
        {
            var sawDialog = false;

            StartWithAutoLogin("Standard", dialog =>
            {
                sawDialog = dialog is PortalLoginForm;
                return sawDialog && PressText(dialog, "Đăng nhập đúng MST");
            });

            Assert.That(sawDialog, Is.True, "Form đăng nhập phải tự mở khi khởi động Standard");
            Assert.That(WaitFor(() => _root.SessionStore.HasActiveSession), Is.True);
            Assert.That(ContentTitle(), Is.EqualTo("Tổng quan"));
        }

        [Test]
        public void Standard_AutoLogin_Cancel_StaysOnDashboardLoggedOut()
        {
            StartWithAutoLogin("Standard", dialog => PressText(dialog, "Hủy"));

            Assert.That(ContentTitle(), Is.EqualTo("Tổng quan"));
            Assert.That(_root.SessionStore.HasActiveSession, Is.False);
            Assert.That(Control<KryptonButton>("btnPortalLogin").Enabled, Is.True, "Vẫn đăng nhập lại được bằng nút");
        }

        [Test]
        public void Service_DoesNotAutoOpenLogin()
        {
            Environment.SetEnvironmentVariable("HDDT_DEV_MODE", "Service");
            _root = CompositionRoot.Create();
            _context = new AppApplicationContext(_root);
            _form = (MainShellForm)_context.MainForm;
            _form.Show();

            Assert.That(WaitFor(IsReady), Is.True, "Service không được bị chặn bởi hộp thoại đăng nhập");
            Assert.That(System.Windows.Forms.Application.OpenForms.Cast<Form>().Any(f => f is PortalLoginForm), Is.False);
        }

        [Test]
        public void Service_StartsOnCompanyList_NotOnLastCompany()
        {
            Start("Service");

            Assert.That(ContentTitle(), Is.EqualTo("Doanh nghiệp"));
            Assert.That(Control<KryptonCheckButton>("navCompanies").Visible, Is.True);
            Assert.That(Control<KryptonButton>("btnPortalLogin").Enabled, Is.False, "Chưa chọn doanh nghiệp thì chưa đăng nhập được");
            Assert.That(Control<KryptonButton>("btnCompanyContext").Values.Text, Is.EqualTo("Chưa chọn doanh nghiệp"));
        }

        [Test]
        public void ReadOnly_HasNoDownloadOrLoginAction()
        {
            Start("ReadOnly");

            Assert.That(ContentTitle(), Is.EqualTo("Doanh nghiệp"));
            Assert.That(Control<KryptonCheckButton>("navDownload").Enabled, Is.False);
            Assert.That(Control<KryptonButton>("btnPortalLogin").Enabled, Is.False);
        }

        [Test]
        public void Recovery_DisablesActions()
        {
            Start("Recovery");

            Assert.That(ContentTitle(), Is.EqualTo("Cần khôi phục"));
            Assert.That(Control<KryptonButton>("btnPortalLogin").Enabled, Is.False);
        }

        [Test]
        public void Standard_FirstRun_OpensCompanySetup()
        {
            Start("StandardFirstRun");

            Assert.That(ContentTitle(), Is.EqualTo("Thông tin doanh nghiệp"));
        }

        [Test]
        public void Menu_ChangesScreen_AndMarksSelection()
        {
            Start("Standard");

            Click("navInvoiceArchive");
            Assert.That(WaitFor(() => ContentTitle() == "Kho hóa đơn"), Is.True);

            Assert.That(Control<KryptonCheckButton>("navInvoiceArchive").Checked, Is.True);
            Assert.That(Control<KryptonCheckButton>("navDashboard").Checked, Is.False);
        }

        [Test]
        public void Login_Cancel_ReturnsToSameScreen_StillLoggedOut()
        {
            Start("Standard");
            Click("navInvoiceArchive");
            Assert.That(WaitFor(() => ContentTitle() == "Kho hóa đơn"), Is.True);

            ClickOpeningDialog("btnPortalLogin", dialog => { PressText(dialog, "Hủy"); return true; });

            Assert.That(ContentTitle(), Is.EqualTo("Kho hóa đơn"));
            Assert.That(Control<KryptonLabel>("lblPortalSession").Values.Text, Is.EqualTo("Chưa đăng nhập"));
            Assert.That(_root.SessionStore.HasActiveSession, Is.False);
        }

        [Test]
        public void Login_CorrectTaxCode_ShowsLoggedIn()
        {
            Start("Standard");

            ClickOpeningDialog("btnPortalLogin", dialog => PressText(dialog, "Đăng nhập đúng MST"));

            Assert.That(WaitFor(() => Control<KryptonLabel>("lblPortalSession").Values.Text == "Đã đăng nhập"), Is.True);
            Assert.That(_root.SessionStore.HasActiveSession, Is.True);
            Assert.That(Control<KryptonButton>("btnPortalLogin").Visible, Is.False);
        }

        [Test]
        public void Login_WrongTaxCode_KeepsDialogOpen_ThenCancelLeavesLoggedOut()
        {
            Start("Standard");
            var step = 0;

            ClickOpeningDialog("btnPortalLogin", dialog =>
            {
                if (step == 0)
                {
                    PressText(dialog, "Đăng nhập sai MST");
                    step = 1;
                    return false;
                }

                // Sau khi sai MST, hộp thoại vẫn mở và cho thử lại.
                Assert.That(dialog.IsDisposed, Is.False);
                PressText(dialog, "Hủy");
                return true;
            });

            Assert.That(_root.SessionStore.HasActiveSession, Is.False);
            Assert.That(Control<KryptonLabel>("lblPortalSession").Values.Text, Is.EqualTo("Chưa đăng nhập"));
        }

        [Test]
        public void CloseApp_ClearsSessionAndReleasesCompany()
        {
            Start("Standard");
            ClickOpeningDialog("btnPortalLogin", dialog => PressText(dialog, "Đăng nhập đúng MST"));
            Assert.That(_root.SessionStore.HasActiveSession, Is.True);

            _form.Close();

            Assert.That(WaitFor(() => _form.IsDisposed || !_root.ContextManager.HasCurrent), Is.True);
            Assert.That(_root.SessionStore.HasActiveSession, Is.False);
            Assert.That(_root.ContextManager.HasCurrent, Is.False);
        }

        [Test]
        public void CloseApp_WithRunningJob_AsksThenPausesAndExits()
        {
            Start("Standard", "1");
            var asked = false;

            RunDialogScript(() => _form.Close(), dialog =>
            {
                asked = dialog is ShutdownPromptForm;
                return asked && PressText(dialog, "Tạm dừng và thoát");
            });

            Assert.That(asked, Is.True, "Phải hỏi người dùng khi đang có tác vụ tải");
            Assert.That(WaitFor(() => !_root.ContextManager.HasCurrent), Is.True);
        }

        [Test]
        public void CloseApp_WithRunningJob_StayKeepsAppOpen()
        {
            Start("Standard", "1");

            RunDialogScript(() => _form.Close(), dialog => dialog is ShutdownPromptForm && PressText(dialog, "Ở lại ứng dụng"));

            Assert.That(_form.IsDisposed, Is.False);
            Assert.That(_root.ContextManager.HasCurrent, Is.True);
        }

        // ---- SCR-03 Tải hóa đơn (DD-04 mục 6.2) ----

        private void LoginAndOpenDownload()
        {
            Start("Standard");
            ClickOpeningDialog("btnPortalLogin", dialog => PressText(dialog, "Đăng nhập đúng MST"));
            Assert.That(_root.SessionStore.HasActiveSession, Is.True);
            Click("navDownload");
            Assert.That(WaitFor(() => ContentTitle() == "Tải hóa đơn"), Is.True, "Không mở được SCR-03");
        }

        private int PlanRows()
        {
            return Control<KryptonDataGridView>("gridPlan").Rows.Count;
        }

        [Test]
        public void Download_Year2026_Purchase_Shows12Rows_AC02()
        {
            LoginAndOpenDownload();

            Control<KryptonNumericUpDown>("numYear").Value = 2026;

            Assert.That(Control<KryptonCheckButton>("rdoPurchase").Checked, Is.True);
            Assert.That(Control<KryptonCheckButton>("btnKindYear").Checked, Is.True);
            Assert.That(PlanRows(), Is.EqualTo(12));
            Assert.That(Control<KryptonDataGridView>("gridPlan").Rows[0].Cells[1].Value, Is.EqualTo("01/01/2026–31/01/2026"));
            Assert.That(Control<KryptonButton>("btnStartDownload").Enabled, Is.True);
        }

        [Test]
        public void Download_CustomRange_15Jan_To_10Mar_Shows3Rows_AC03()
        {
            LoginAndOpenDownload();

            Click("btnKindCustom");
            Control<KryptonDateTimePicker>("dtpTo").Value = new DateTime(2026, 3, 10);
            Control<KryptonDateTimePicker>("dtpFrom").Value = new DateTime(2026, 1, 15);

            Assert.That(PlanRows(), Is.EqualTo(3));
            var grid = Control<KryptonDataGridView>("gridPlan");
            Assert.That(grid.Rows[0].Cells[1].Value, Is.EqualTo("15/01/2026–31/01/2026"));
            Assert.That(grid.Rows[2].Cells[1].Value, Is.EqualTo("01/03/2026–10/03/2026"));
        }

        [Test]
        public void Download_InvalidRange_ShowsErrorInControl_AndLocksStart()
        {
            LoginAndOpenDownload();

            Click("btnKindCustom");
            Control<KryptonDateTimePicker>("dtpTo").Value = new DateTime(2026, 3, 1);
            Control<KryptonDateTimePicker>("dtpFrom").Value = new DateTime(2026, 3, 10);

            Assert.That(PlanRows(), Is.EqualTo(0));
            Assert.That(Control<System.Windows.Forms.Control>("lblPeriodMessage").Text, Does.Contain("lớn hơn hoặc bằng"));
            Assert.That(Control<KryptonButton>("btnStartDownload").Enabled, Is.False);
        }

        [Test]
        public void Download_NotLoggedIn_StartDisabled_WithReasonNextToButton()
        {
            Start("Standard");

            Click("navDownload");
            Assert.That(WaitFor(() => ContentTitle() == "Tải hóa đơn"), Is.True);

            Assert.That(Control<KryptonButton>("btnStartDownload").Enabled, Is.False);
            Assert.That(Control<System.Windows.Forms.Control>("lblStartReason").Text, Does.Contain("đăng nhập"));
            Assert.That(PlanRows(), Is.EqualTo(12), "Kế hoạch vẫn xem trước được khi chưa đăng nhập");
        }

        [Test]
        public void Download_Start_GoesToProgressScreen()
        {
            LoginAndOpenDownload();

            Click("btnStartDownload");

            Assert.That(WaitFor(() => ContentTitle() == "Tiến độ tải"), Is.True);
        }

        [Test]
        public void Download_DirectionToggle_IsExclusive()
        {
            LoginAndOpenDownload();

            Click("rdoSold");

            Assert.That(Control<KryptonCheckButton>("rdoSold").Checked, Is.True);
            Assert.That(Control<KryptonCheckButton>("rdoPurchase").Checked, Is.False);
        }

        // ---- helpers ----

        private T Control<T>(string name) where T : System.Windows.Forms.Control
        {
            var found = _form.Controls.Find(name, true).OfType<T>().FirstOrDefault();
            Assert.That(found, Is.Not.Null, "Không thấy control " + name);
            return found;
        }

        private string ContentTitle()
        {
            var content = _form.Controls.Find("pnlContent", true).FirstOrDefault();
            if (content == null || content.Controls.Count == 0) return null;
            // Screen thật đặt tên lblTitle; placeholder thì lấy nhãn đầu tiên.
            var named = content.Controls.Find("lblTitle", true).OfType<KryptonLabel>().FirstOrDefault();
            var label = named ?? content.Controls[0].Controls.OfType<KryptonLabel>().FirstOrDefault();
            return label == null ? null : label.Values.Text;
        }

        private void Click(string name)
        {
            Invoke(Control<System.Windows.Forms.Control>(name));
        }

        private static void Invoke(System.Windows.Forms.Control control)
        {
            var method = control.GetType().GetMethod("PerformClick", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null, control.GetType().Name + " không có PerformClick");
            method.Invoke(control, null);
        }

        private void ClickOpeningDialog(string controlName, Func<Form, bool> script)
        {
            RunDialogScript(() => Click(controlName), script);
        }

        /// <summary>
        /// Chạy <paramref name="open"/> (thường chặn trong ShowDialog), đồng thời một timer trong vòng lặp modal
        /// gọi <paramref name="script"/> với hộp thoại đang mở cho tới khi script trả về true.
        /// </summary>
        private void RunDialogScript(Action open, Func<Form, bool> script)
        {
            var watch = Stopwatch.StartNew();
            using (var timer = new System.Windows.Forms.Timer { Interval = 50 })
            {
                timer.Tick += delegate
                {
                    if (watch.ElapsedMilliseconds > 15000)
                    {
                        timer.Stop();
                        CloseOpenDialogs();
                        return;
                    }

                    var dialog = System.Windows.Forms.Application.OpenForms
                        .Cast<Form>()
                        .FirstOrDefault(f => f != _form && f.Visible && (f is PortalLoginForm || f is ShutdownPromptForm));
                    if (dialog != null && script(dialog)) timer.Stop();
                };
                timer.Start();
                open();
                // Nếu open() không chặn (ví dụ Close() hoãn lại), bơm tiếp để script chạy xong.
                WaitFor(() => !timer.Enabled, 15000);
            }
        }

        private static bool PressText(Form dialog, string text)
        {
            var button = Descendants(dialog).OfType<KryptonButton>().FirstOrDefault(b => b.Values.Text == text && b.Visible && b.Enabled);
            if (button == null) return false;
            button.PerformClick();
            return true;
        }

        private static System.Collections.Generic.IEnumerable<System.Windows.Forms.Control> Descendants(System.Windows.Forms.Control root)
        {
            foreach (System.Windows.Forms.Control child in root.Controls)
            {
                yield return child;
                foreach (var nested in Descendants(child)) yield return nested;
            }
        }

        private void CloseOpenDialogs()
        {
            foreach (var form in System.Windows.Forms.Application.OpenForms.Cast<Form>().Where(f => f != _form).ToArray())
            {
                if (form is ShutdownPromptForm) form.DialogResult = DialogResult.OK;
                else if (form is PortalLoginForm) PressText(form, "Hủy");
            }
        }

        private static bool WaitFor(Func<bool> condition, int timeoutMs = 10000)
        {
            var watch = Stopwatch.StartNew();
            while (watch.ElapsedMilliseconds < timeoutMs)
            {
                System.Windows.Forms.Application.DoEvents();
                if (condition()) return true;
                Thread.Sleep(10);
            }

            return false;
        }
    }
}
