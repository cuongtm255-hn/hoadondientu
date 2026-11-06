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
        private string _oldDataDir;
        private string _dataDir;

        [SetUp]
        public void SetUp()
        {
            UseUiSynchronizationContext();
            _oldMode = Environment.GetEnvironmentVariable("HDDT_DEV_MODE");
            _oldJob = Environment.GetEnvironmentVariable("HDDT_DEV_JOB");
            // Mỗi test có thư mục dữ liệu riêng (danh mục + kho thật trên đĩa) để không ảnh hưởng nhau hay dữ liệu dev.
            _oldDataDir = Environment.GetEnvironmentVariable("HDDT_DATA_DIR");
            _dataDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "hddt-ui-" + Guid.NewGuid().ToString("N"));
            Environment.SetEnvironmentVariable("HDDT_DATA_DIR", _dataDir);
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
            Environment.SetEnvironmentVariable("HDDT_DATA_DIR", _oldDataDir);
            SynchronizationContext.SetSynchronizationContext(null);
            try
            {
                if (System.IO.Directory.Exists(_dataDir)) System.IO.Directory.Delete(_dataDir, true);
            }
            catch (System.IO.IOException)
            {
            }
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
        public void Standard_StartsOnDashboard_LoginAvailable_DownloadWaitsForPortalLogin()
        {
            Start("Standard");

            Assert.That(ContentTitle(), Is.EqualTo("Tổng quan"));
            Assert.That(Control<KryptonButton>("btnPortalLogin").Enabled, Is.True);
            Assert.That(Control<KryptonCheckButton>("navDownload").Enabled, Is.False, "Chưa đăng nhập cổng Thuế thì chưa tải được");
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

            Assert.That(ContentTitle(), Is.EqualTo("Quản lý doanh nghiệp"));
            Assert.That(Control<KryptonCheckButton>("navCompanies").Visible, Is.True);
            Assert.That(Control<KryptonButton>("btnPortalLogin").Enabled, Is.False, "Chưa chọn doanh nghiệp thì chưa đăng nhập được");
            Assert.That(Control<KryptonButton>("btnCompanyContext").Values.Text, Is.EqualTo("Chưa chọn doanh nghiệp"));
        }

        [Test]
        public void ReadOnly_HasNoDownloadOrLoginAction()
        {
            Start("ReadOnly");

            Assert.That(ContentTitle(), Is.EqualTo("Quản lý doanh nghiệp"));
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

            Assert.That(ContentTitle(), Is.EqualTo("Khai báo doanh nghiệp"));
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
        public void Archive_Opens_WithEmptyMockStore_AndDirectionToggleWorks()
        {
            Start("Standard");

            Click("navInvoiceArchive");
            Assert.That(WaitFor(() => ContentTitle() == "Kho hóa đơn"), Is.True);
            Assert.That(WaitFor(() => Control<KryptonLabel>("lblStat1Value").Values.Text == "0"), Is.True);
            Assert.That(Control<DataGridView>("gridInvoices").Rows.Count, Is.EqualTo(0));

            Click("btnSold");

            Assert.That(WaitFor(() => Control<DataGridView>("gridInvoices").Columns["colCounterpartyId"].HeaderText == "MST người mua"), Is.True);
        }

        [Test]
        public void Dashboard_ShowsEmptyState_AndOpenArchiveButtonNavigates()
        {
            Start("Standard");

            Assert.That(ContentTitle(), Is.EqualTo("Tổng quan"));
            Assert.That(WaitFor(() => Control<KryptonLabel>("lblStat4Sub").Values.Text == "Không có việc cần xử lý"), Is.True);
            Assert.That(Control<KryptonLabel>("lblStat1Value").Values.Text, Is.EqualTo("0"));
            Assert.That(Control<KryptonLabel>("lblNoSession").Visible, Is.True);
            Assert.That(Control<DataGridView>("gridRecent").Rows.Count, Is.EqualTo(0));

            Click("btnOpenArchive");

            Assert.That(WaitFor(() => ContentTitle() == "Kho hóa đơn"), Is.True);
        }

        [Test]
        public void History_Opens_WithEmptyMockStore_AndNewSessionGoesToDownload()
        {
            Start("Standard");

            Click("navDownloadHistory");
            Assert.That(WaitFor(() => ContentTitle() == "Lịch sử tải"), Is.True);
            Assert.That(WaitFor(() => Control<KryptonLabel>("lblStat1Value").Values.Text == "0"), Is.True);
            Assert.That(Control<DataGridView>("gridSessions").Rows.Count, Is.EqualTo(0));

            Click("btnNewSession");

            Assert.That(ContentTitle(), Is.EqualTo("Lịch sử tải"), "Chưa đăng nhập cổng Thuế thì chưa mở được màn hình tải");
            Assert.That(WaitFor(() => Control<System.Windows.Forms.Control>("lblMessage").Text.Contains("đăng nhập cổng Thuế")), Is.True);

            ClickOpeningDialog("btnPortalLogin", dialog => PressText(dialog, "Đăng nhập đúng MST"));
            Click("btnNewSession");

            Assert.That(WaitFor(() => ContentTitle() == "Tải hóa đơn"), Is.True);
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

            Assert.That(WaitFor(() => Control<KryptonLabel>("lblPortalSession").Values.Text == string.Empty), Is.True);
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
        public void Download_PastYear_Purchase_Shows12Rows_AC02()
        {
            LoginAndOpenDownload();

            Control<KryptonNumericUpDown>("numYear").Value = DateTime.Today.Year - 1;

            Assert.That(Control<KryptonCheckButton>("rdoPurchase").Checked, Is.True);
            Assert.That(Control<KryptonCheckButton>("btnKindYear").Checked, Is.True);
            Assert.That(PlanRows(), Is.EqualTo(12));
            Assert.That(Control<KryptonDataGridView>("gridPlan").Rows[0].Cells[1].Value, Is.EqualTo("01/01/" + (DateTime.Today.Year - 1) + "–31/01/" + (DateTime.Today.Year - 1)));
            Assert.That(Control<KryptonButton>("btnStartDownload").Enabled, Is.True);
        }

        [Test]
        public void Download_CurrentYear_OnlyListsMonthsUpToToday()
        {
            LoginAndOpenDownload();

            Control<KryptonNumericUpDown>("numYear").Value = DateTime.Today.Year;

            Assert.That(PlanRows(), Is.EqualTo(DateTime.Today.Month), "Không liệt kê tháng chưa tới");
            var last = Control<KryptonDataGridView>("gridPlan").Rows[DateTime.Today.Month - 1].Cells[1].Value.ToString();
            Assert.That(last, Does.EndWith(DateTime.Today.ToString("dd/MM/yyyy")), "Tháng hiện tại được cắt tới hôm nay");
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
        public void Download_IsLockedUntilPortalLogin_AndOpensAfterIt()
        {
            Start("Standard");
            Assert.That(Control<KryptonCheckButton>("navDownload").Enabled, Is.False);

            ClickOpeningDialog("btnPortalLogin", dialog => PressText(dialog, "Đăng nhập đúng MST"));

            Assert.That(Control<KryptonCheckButton>("navDownload").Enabled, Is.True);
            Click("navDownload");
            Assert.That(WaitFor(() => ContentTitle() == "Tải hóa đơn"), Is.True);
            Assert.That(Control<KryptonButton>("btnStartDownload").Enabled, Is.True);
        }

        [Test]
        public void Download_SessionLostWhileOpen_LocksStart_AndLoginUnlocksItAgain()
        {
            LoginAndOpenDownload();

            _root.SessionStore.Clear();
            Assert.That(Control<KryptonButton>("btnStartDownload").Enabled, Is.False);
            Assert.That(Control<System.Windows.Forms.Control>("lblStartReason").Text, Does.Contain("đăng nhập"));
            Assert.That(Control<KryptonCheckButton>("navDownload").Enabled, Is.False);

            ClickOpeningDialog("btnPortalLogin", dialog => PressText(dialog, "Đăng nhập đúng MST"));

            Assert.That(Control<KryptonButton>("btnStartDownload").Enabled, Is.True, "Đăng nhập xong khi đang ở màn hình tải thì nút bắt đầu bật lại");
        }

        [Test]
        public void Download_Start_GoesToProgressScreen()
        {
            LoginAndOpenDownload();

            Click("btnStartDownload");

            Assert.That(WaitFor(() => ContentTitle() == "Tiến độ tải hóa đơn"), Is.True);
        }

#if DEBUG
        private string ProgressMessage()
        {
            return Control<KryptonWrapLabel>("lblMessage").Text;
        }

        [Test]
        public void Download_RunsToCompletion_OnMockData_AndWritesXmlIntoTheCompanyFolder()
        {
            LoginAndOpenDownload();
            Click("btnStartDownload");
            Assert.That(WaitFor(() => ContentTitle() == "Tiến độ tải hóa đơn"), Is.True, "Tạo phiên (SQLite) rồi mở SCR-04");

            Assert.That(WaitFor(() => ProgressMessage().Contains("Đã tải xong toàn bộ"), 90000), Is.True, ProgressMessage());

            var grid = Control<KryptonDataGridView>("gridSegments");
            Assert.That(grid.Rows.Count, Is.EqualTo(DateTime.Today.Month * 3), "Cả năm hiện tại, chỉ tới hôm nay: mỗi tháng × 3 kết quả kiểm tra (5, 6, 8)");
            Assert.That(Control<KryptonLabel>("lblStat1Value").Values.Text, Is.EqualTo("100%"));
            Assert.That(Control<KryptonButton>("btnPause").Enabled, Is.False);
            Assert.That(Control<KryptonButton>("btnCancel").Enabled, Is.False, "Phiên đã kết thúc");
            var root = _root.ContextManager.Current.StorageRoot;
            var files = System.IO.Directory.GetFiles(System.IO.Path.Combine(root, "2026"), "*.xml", System.IO.SearchOption.AllDirectories);
            Assert.That(files.Length, Is.GreaterThan(0));
            Assert.That(Control<KryptonLabel>("lblStat3Value").Values.Text, Is.EqualTo(files.Length.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)));
        }

        [Test]
        public void Download_PauseThenResume_ContinuesFromTheCheckpoint()
        {
            LoginAndOpenDownload();
            Click("btnStartDownload");
            Assert.That(WaitFor(() => ContentTitle() == "Tiến độ tải hóa đơn"), Is.True, "Tạo phiên (SQLite) rồi mở SCR-04");
            Assert.That(WaitFor(() => Control<KryptonButton>("btnPause").Enabled), Is.True);

            Click("btnPause");
            Assert.That(WaitFor(() => Control<KryptonButton>("btnResume").Enabled, 30000), Is.True, ProgressMessage());
            Assert.That(ProgressMessage(), Does.Contain("tạm dừng"));

            Click("btnResume");
            Assert.That(WaitFor(() => ProgressMessage().Contains("Đã tải xong toàn bộ"), 90000), Is.True, ProgressMessage());
        }
#endif

        [Test]
        public void Download_DirectionToggle_IsExclusive()
        {
            LoginAndOpenDownload();

            Click("rdoSold");

            Assert.That(Control<KryptonCheckButton>("rdoSold").Checked, Is.True);
            Assert.That(Control<KryptonCheckButton>("rdoPurchase").Checked, Is.False);
        }

#if DEBUG
        // ---- Chính sách test (CLAUDE.md): bản Debug luôn hiện nhãn TEST/MOCK ----

        [Test]
        public void DebugBuild_AlwaysShowsTestMarker_InTitleAndBanner()
        {
            Start("Standard");

            Assert.That(_form.Text, Does.Contain(MainShellForm.TestMarker));
            var banner = Control<KryptonPanel>("pnlTestBanner");
            Assert.That(banner.Visible, Is.True);
            Assert.That(banner.Dock, Is.EqualTo(DockStyle.Top));
            Assert.That(Control<KryptonLabel>("lblTestBanner").Values.Text, Does.Contain("dữ liệu giả"));
        }

        [TestCase("Service")]
        [TestCase("ReadOnly")]
        [TestCase("StandardFirstRun")]
        public void TestMarker_IsShownInEveryDevelopmentMode(string mode)
        {
            Start(mode);

            Assert.That(Control<KryptonPanel>("pnlTestBanner").Visible, Is.True);
        }
#endif

        // ---- SCR-10 / SCR-11 (DD-02 mục 8.3) ----

        private System.Windows.Forms.Control CurrentScreenControl()
        {
            return _form.Controls.Find("pnlContent", true).First().Controls[0];
        }

        private int CompanyRows()
        {
            return Control<KryptonDataGridView>("gridCompanies").Rows.Count;
        }

        /// <summary>Bấm nút "Thao tác" của dòng trong danh sách doanh nghiệp (cell button của DataGridView).</summary>
        private void ClickRowAction(int rowIndex)
        {
            var list = CurrentScreenControl();
            var grid = Control<KryptonDataGridView>("gridCompanies");
            var method = list.GetType().GetMethod("OnGridCellContentClick", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null);
            method.Invoke(list, new object[] { grid, new DataGridViewCellEventArgs(grid.Columns["colAction"].Index, rowIndex) });
        }

        private static string AllLabelText(Form dialog)
        {
            return string.Join("|", Descendants(dialog).OfType<KryptonLabel>().Select(l => l.Values.Text));
        }

        [Test]
        public void Service_StartsAtCompanyList_WithBannerAndThreeCompanies()
        {
            Start("Service");

            Assert.That(CompanyRows(), Is.EqualTo(3));
            Assert.That(Control<KryptonButton>("btnAddCompany").Enabled, Is.True);
            Assert.That(Control<KryptonLabel>("lblBanner").Values.Text, Does.Contain("Licence dịch vụ"));
            Assert.That(Control<KryptonLabel>("lblStat1Value").Values.Text, Is.EqualTo("3"));
            Assert.That(Control<KryptonButton>("btnPortalLogin").Enabled, Is.False, "Chưa mở doanh nghiệp nào");
        }

        [Test]
        public void Service_Work_OpensLoginForThatCompanyNameAndTaxCode_ThenHeaderFollows()
        {
            Start("Service");
            string seen = null;

            RunDialogScript(() => ClickRowAction(1), dialog =>
            {
                seen = AllLabelText(dialog);
                return PressText(dialog, "Đăng nhập đúng MST");
            });

            Assert.That(seen, Does.Contain("CÔNG TY MẪU 2").And.Contain("0101234561"));
            Assert.That(WaitFor(() => ContentTitle() == "Tổng quan"), Is.True);
            Assert.That(Control<KryptonButton>("btnCompanyContext").Values.Text, Is.EqualTo("CÔNG TY MẪU 2"));
            Assert.That(Control<KryptonLabel>("lblTaxCode").Values.Text, Does.Contain("0101234561"));
            Assert.That(_root.SessionStore.GetSnapshot().TaxCode.Value, Is.EqualTo("0101234561"));
        }

        [Test]
        public void Service_SwitchingCompany_ClearsPreviousSession_BeforeLoginOfTheNewOne()
        {
            Start("Service");
            RunDialogScript(() => ClickRowAction(0), dialog => PressText(dialog, "Đăng nhập đúng MST"));
            Assert.That(_root.SessionStore.GetSnapshot().TaxCode.Value, Is.EqualTo("0101234560"));

            Click("navCompanies");
            Assert.That(WaitFor(() => ContentTitle() == "Quản lý doanh nghiệp"), Is.True);
            var sessionWhileLoggingIn = true;
            string seen = null;
            RunDialogScript(() => ClickRowAction(2), dialog =>
            {
                sessionWhileLoggingIn = _root.SessionStore.HasActiveSession;
                seen = AllLabelText(dialog);
                return PressText(dialog, "Đăng nhập đúng MST");
            });

            Assert.That(sessionWhileLoggingIn, Is.False, "Token của MST cũ phải bị xóa trước khi mở đăng nhập MST mới (FR-AUTH-012)");
            Assert.That(seen, Does.Contain("0101234562"));
            Assert.That(_root.SessionStore.GetSnapshot().TaxCode.Value, Is.EqualTo("0101234562"));
            Assert.That(_root.ContextManager.Current.TaxCode.Value, Is.EqualTo("0101234562"));
        }

        [Test]
        public void Service_AddCompany_CreatesStorage_SwitchesAndOpensLogin()
        {
            Start("Service");
            Click("btnAddCompany");
            Assert.That(WaitFor(() => ContentTitle() == "Thêm doanh nghiệp"), Is.True);

            var path = System.IO.Path.Combine(_dataDir, "Companies", "0309999999");
            Control<KryptonTextBox>("txtTaxCode").Text = "0309999999";
            Control<KryptonTextBox>("txtDisplayName").Text = "Công ty Mới";
            Assert.That(Control<KryptonTextBox>("txtStoragePath").Text, Is.EqualTo(path), "Thư mục gợi ý theo MST");
            Assert.That(WaitFor(() => Control<KryptonButton>("btnPrimary").Enabled), Is.True);

            ClickOpeningDialog("btnPrimary", dialog => PressText(dialog, "Đăng nhập đúng MST"));

            Assert.That(System.IO.File.Exists(System.IO.Path.Combine(path, "company.identity.json")), Is.True);
            Assert.That(_root.ContextManager.Current.TaxCode.Value, Is.EqualTo("0309999999"));
            Assert.That(WaitFor(() => ContentTitle() == "Tổng quan"), Is.True);
            Assert.That(Control<KryptonButton>("btnCompanyContext").Values.Text, Is.EqualTo("Công ty Mới"));
        }

        [Test]
        public void Service_AddCompany_DuplicateTaxCode_IsRejectedLive()
        {
            Start("Service");
            Click("btnAddCompany");
            Assert.That(WaitFor(() => ContentTitle() == "Thêm doanh nghiệp"), Is.True);

            Control<KryptonTextBox>("txtTaxCode").Text = "0101234560";
            Control<KryptonTextBox>("txtDisplayName").Text = "Trùng";

            Assert.That(Control<KryptonButton>("btnPrimary").Enabled, Is.False);
            Assert.That(Control<KryptonLabel>("lblCheck2").Values.Text, Does.Contain("CÔNG TY MẪU 1"));
        }

        [Test]
        public void Service_CancelOnAddScreen_ReturnsToList()
        {
            Start("Service");
            Click("btnAddCompany");
            Assert.That(WaitFor(() => ContentTitle() == "Thêm doanh nghiệp"), Is.True);

            Click("btnCancel");

            Assert.That(WaitFor(() => ContentTitle() == "Quản lý doanh nghiệp"), Is.True);
        }

        [Test]
        public void Standard_FirstRun_DeclaresTheOnlyCompany_ThenLogsIn()
        {
            Start("StandardFirstRun");
            Assert.That(ContentTitle(), Is.EqualTo("Khai báo doanh nghiệp"));
            Assert.That(Control<KryptonButton>("btnCancel").Visible, Is.False);
            Assert.That(Control<KryptonButton>("btnPortalLogin").Enabled, Is.False, "Chưa có doanh nghiệp");

            Control<KryptonTextBox>("txtTaxCode").Text = "0101234567";
            Control<KryptonTextBox>("txtDisplayName").Text = "Công ty Của Tôi";
            Assert.That(WaitFor(() => Control<KryptonButton>("btnPrimary").Enabled), Is.True);

            ClickOpeningDialog("btnPrimary", dialog => PressText(dialog, "Đăng nhập đúng MST"));

            Assert.That(WaitFor(() => ContentTitle() == "Tổng quan"), Is.True);
            Assert.That(Control<KryptonButton>("btnCompanyContext").Values.Text, Is.EqualTo("Công ty Của Tôi"));
            Assert.That(_root.SessionStore.GetSnapshot().TaxCode.Value, Is.EqualTo("0101234567"));
            Assert.That(Control<KryptonCheckButton>("navCompanies").Visible, Is.False, "Standard không có lối vào quản lý nhiều MST");
        }

        [Test]
        public void Standard_FirstRun_InvalidTaxCode_KeepsPrimaryDisabled()
        {
            Start("StandardFirstRun");

            Control<KryptonTextBox>("txtTaxCode").Text = "12345";
            Control<KryptonTextBox>("txtDisplayName").Text = "X";

            Assert.That(Control<KryptonButton>("btnPrimary").Enabled, Is.False);
            Assert.That(Control<KryptonLabel>("lblCheck1").Values.Text, Does.StartWith("✕"));
        }

        [Test]
        public void ReadOnly_List_ShowsWarning_LocksAdd_AndWorkOpensStoreWithoutLogin()
        {
            Start("ReadOnly");

            Assert.That(Control<KryptonLabel>("lblBanner").Values.Text, Does.Contain("Chỉ đọc"));
            Assert.That(Control<KryptonButton>("btnAddCompany").Enabled, Is.False);

            ClickRowAction(0);

            Assert.That(WaitFor(() => ContentTitle() == "Tổng quan"), Is.True);
            Assert.That(_root.ContextManager.HasCurrent, Is.True);
            Assert.That(_root.SessionStore.HasActiveSession, Is.False);
            Assert.That(Control<KryptonButton>("btnPortalLogin").Enabled, Is.False, "ReadOnly khóa đăng nhập");
            Assert.That(Control<KryptonCheckButton>("navDownload").Enabled, Is.False);
        }

        // ---- SCR-12 (DD-03 mục 8.3) ----

        private void OpenLicenseScreen()
        {
            _root.NavigationService.NavigateAsync(HoaDonDienTu.Application.Navigation.ScreenId.License, null, System.Threading.CancellationToken.None);
            Assert.That(WaitFor(() => ContentTitle() == "Bản quyền ứng dụng"), Is.True);
        }

        [Test]
        public void License_ShowsTheTrial_AndActivateNeedsAKey()
        {
            Start("Standard");

            OpenLicenseScreen();

            Assert.That(Control<KryptonLabel>("lblModeTitle").Values.Text, Is.EqualTo("Đang dùng thử"));
            Assert.That(Control<KryptonLabel>("lblLast").Values.Text, Is.EqualTo("Chưa kích hoạt"));
            Assert.That(Control<KryptonLabel>("lblMachine").Values.Text, Does.Match("^[0-9A-F]{8}(-[0-9A-F]{8}){3}$"), "Mã máy dạng XXXXXXXX-XXXXXXXX-XXXXXXXX-XXXXXXXX");
            Assert.That(Control<KryptonButton>("btnActivate").Enabled, Is.False);

            Control<KryptonTextBox>("txtLicenseKey").Text = "HDDT1|abc|def";
            Assert.That(Control<KryptonButton>("btnActivate").Enabled, Is.True);
        }

        [Test]
        public void Settings_LicenseEntry_OpensScr12_AndBackReturnsToSettings()
        {
            Start("Standard");
            _root.NavigationService.NavigateAsync(HoaDonDienTu.Application.Navigation.ScreenId.Settings, null, System.Threading.CancellationToken.None);
            Assert.That(WaitFor(() => ContentTitle() == "Cài đặt"), Is.True);
            Assert.That(Control<KryptonButton>("btnSecLicense").Values.ExtraText, Is.EqualTo("Chưa kích hoạt"));

            Click("btnSecLicense");
            Assert.That(WaitFor(() => ContentTitle() == "Bản quyền ứng dụng"), Is.True);

            Click("btnBack");
            Assert.That(WaitFor(() => ContentTitle() == "Cài đặt"), Is.True);
        }

        [Test]
        public void Settings_StorageSection_ShowsBackupActions_AndTheConnectionCheckIsMocked()
        {
            Start("Standard");
            _root.NavigationService.NavigateAsync(HoaDonDienTu.Application.Navigation.ScreenId.Settings, null, System.Threading.CancellationToken.None);
            Assert.That(WaitFor(() => ContentTitle() == "Cài đặt"), Is.True);

            Click("btnSecStorage");

            Assert.That(WaitFor(() => _form.Controls.Find("btnAction_backup", true).Length == 1), Is.True, "Mục Lưu trữ phải có nút Tạo bản sao lưu");
            Assert.That(_form.Controls.Find("btnAction_restore", true).Length, Is.EqualTo(1));
            Assert.That(_form.Controls.Find("btnAction_open-folder", true).Length, Is.EqualTo(1));

            Click("btnSecPortal");
            Assert.That(WaitFor(() => _form.Controls.Find("btnAction_check-connection", true).Length == 1), Is.True);
            Click("btnAction_check-connection");

            // Bản Debug không gọi cổng thật: kết quả là mô phỏng và được ghi rõ.
            Assert.That(WaitFor(() => _form.Controls.Find("pnlCard", true).FirstOrDefault() != null
                && _form.Controls.Find("pnlCard", true)[0].Controls.Find("tlpRows", true).FirstOrDefault() != null
                && _form.Controls.Find("tlpRows", true)[0].Controls.OfType<System.Windows.Forms.Label>().Any(l => l.Text.Contains("mô phỏng"))), Is.True);
        }

        // ---- helpers ----

        // ---- NFR-PERF-003: mở ứng dụng ----

        [Test]
        public void Startup_ReachesTheDashboard_WithinFiveSeconds_NFR_PERF_003()
        {
            // Đo cả dựng CompositionRoot, tạo cửa sổ chính, đọc danh mục + kiểm tra licence và hiện màn hình Tổng quan (Debug, dữ liệu giả,
            // thư mục dữ liệu mới). Trên máy phát triển; số tuyệt đối chỉ để theo dõi xu hướng.
            var watch = Stopwatch.StartNew();
            Start("Standard");
            watch.Stop();

            TestContext.WriteLine("PERF startup to dashboard: {0:N0} ms", watch.Elapsed.TotalMilliseconds);
            Assert.That(ContentTitle(), Is.EqualTo("Tổng quan"));
            Assert.That(watch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(5)), "NFR-PERF-003");
        }

        // ---- Smoke 05 → 06 → 07 với kho thật (T08-08) và khôi phục danh mục ----

        private const string SmokeInvoiceNumber = "0000128";

        /// <summary>
        /// Dựng sẵn kho của doanh nghiệp mẫu trong thư mục dữ liệu của test: một hóa đơn mua vào GIẢ trong SQLite company.db thật và file XML thật
        /// (SHA-256 khớp). Trả về thư mục kho. Không gọi cổng Thuế.
        /// </summary>
        private string SeedStoreWithOneInvoice()
        {
            HoaDonDienTu.Infrastructure.Development.DevelopmentDataSeeder.Seed(_dataDir, "Standard");
            var items = new HoaDonDienTu.Infrastructure.Companies.JsonCompanyCatalog(System.IO.Path.Combine(_dataDir, "companies.json"))
                .GetItemsAsync(CancellationToken.None).GetAwaiter().GetResult();
            var item = items.Single();

            var xml = System.Text.Encoding.UTF8.GetBytes("<HDon><DLHDon><TTChung><SHDon>" + SmokeInvoiceNumber + "</SHDon></TTChung></DLHDon></HDon>");
            string sha;
            using (var hash = System.Security.Cryptography.SHA256.Create()) sha = string.Concat(hash.ComputeHash(xml).Select(b => b.ToString("x2")));
            var relative = @"2026\10\MuaVao_" + SmokeInvoiceNumber + "_v1.xml";
            var full = System.IO.Path.Combine(item.StoragePath, relative);
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full));
            System.IO.File.WriteAllBytes(full, xml);

            var invoice = new HoaDonDienTu.Domain.Invoices.NormalizedInvoice
            {
                Direction = HoaDonDienTu.Domain.Invoices.InvoiceDirection.Purchase,
                InvoiceNumber = SmokeInvoiceNumber, InvoiceSymbol = "C26TAA", FormSymbol = "1", IssueDate = new DateTime(2026, 10, 31),
                SellerTaxCode = "0109988776", SellerName = "CÔNG TY GIẢ (mô phỏng)", BuyerTaxCode = item.TaxCode.Value, BuyerName = item.DisplayName,
                StatusRaw = "1", CheckResultRaw = "5", TotalBeforeTax = 1000m, TotalTax = 100m, TotalPayment = 1100m, Currency = "VND"
            };
            var now = DateTime.UtcNow;
            var stored = new HoaDonDienTu.Application.Invoices.StoredInvoice
            {
                InvoiceId = Guid.NewGuid(),
                BusinessKey = HoaDonDienTu.Domain.Invoices.BusinessKey.Build(invoice),
                Invoice = invoice,
                FirstSeenAtUtc = now,
                LastSeenAtUtc = now,
                Versions = new System.Collections.Generic.List<HoaDonDienTu.Application.Invoices.InvoiceVersionRecord>
                {
                    new HoaDonDienTu.Application.Invoices.InvoiceVersionRecord
                    {
                        VersionId = Guid.NewGuid(), VersionNo = 1, Sha256 = sha, RelativePath = relative, SizeBytes = xml.Length, CreatedAtUtc = now, IsCurrent = true
                    }
                }
            };
            var context = new FixedStoreContext(new HoaDonDienTu.Application.Companies.CompanyContext(item.Id, item.TaxCode, item.DisplayName, item.StoragePath, null));
            var provider = new HoaDonDienTu.Infrastructure.Persistence.Sqlite.CompanyDatabaseProvider(context);
            new HoaDonDienTu.Infrastructure.Persistence.Sqlite.SqliteInvoiceRepository(provider).AddAsync(stored, CancellationToken.None).GetAwaiter().GetResult();
            System.Data.SQLite.SQLiteConnection.ClearAllPools();
            return item.StoragePath;
        }

        private sealed class FixedStoreContext : HoaDonDienTu.Application.Companies.ICompanyContextAccessor
        {
            public FixedStoreContext(HoaDonDienTu.Application.Companies.CompanyContext current)
            {
                Current = current;
            }

            public bool HasCurrent { get { return true; } }
            public HoaDonDienTu.Application.Companies.CompanyContext Current { get; private set; }
        }

        private string LabelText(string name)
        {
            var label = _form.Controls.Find(name, true).OfType<KryptonLabel>().FirstOrDefault();
            return label == null ? null : label.Values.Text;
        }

        [Test]
        public void Invoice_OpensFromTheArchive_ThroughDetail_ToTheViewer_AndBack_05_06_07()
        {
            SeedStoreWithOneInvoice();
            Start("Standard");

            // SCR-05: kho có đúng một hóa đơn mua vào đọc từ SQLite thật.
            Click("navInvoiceArchive");
            Assert.That(WaitFor(() => ContentTitle() == "Kho hóa đơn"), Is.True);
            var grid = Control<KryptonDataGridView>("gridInvoices");
            Assert.That(WaitFor(() => grid.Rows.Count == 1), Is.True, "Kho phải hiện hóa đơn đã lưu");
            Assert.That(grid.Rows[0].Cells["colNumber"].Value, Is.EqualTo(SmokeInvoiceNumber));
            Assert.That(Control<KryptonButton>("btnExportExcel").Enabled, Is.True, "Có dòng thì cho xuất");

            // Mở chi tiết bằng cách bấm đúp dòng (cùng đường đi với người dùng).
            var archive = _form.Controls.Find("pnlContent", true).Single().Controls.OfType<HoaDonDienTu.WinForms.Archive.ArchiveControl>().Single();
            var open = typeof(HoaDonDienTu.WinForms.Archive.ArchiveControl).GetMethod("OnCellDoubleClick", BindingFlags.NonPublic | BindingFlags.Instance);
            UseUiSynchronizationContext();
            open.Invoke(archive, new object[] { grid, new DataGridViewCellEventArgs(0, 0) });

            // SCR-06
            Assert.That(WaitFor(() => ContentTitle() == "Chi tiết hóa đơn " + SmokeInvoiceNumber), Is.True, ContentTitle());
            Assert.That(WaitFor(() => Control<KryptonButton>("btnView").Enabled), Is.True, "XML còn nguyên nên mở được chứng từ");
            Assert.That(WaitFor(() => Control<KryptonButton>("btnExportZip").Enabled), Is.True);

            // SCR-07
            Click("btnView");
            Assert.That(WaitFor(() => ContentTitle() == "Xem chứng từ"), Is.True, ContentTitle());
            Assert.That(WaitFor(() => (LabelText("lblSubtitle") ?? string.Empty).Contains(SmokeInvoiceNumber)), Is.True, LabelText("lblSubtitle"));
            Assert.That(LabelText("lblSubtitle"), Does.Contain("C26TAA").And.Contain("v1"));

            // Quay lại SCR-06 rồi SCR-05.
            Click("btnClose");
            Assert.That(WaitFor(() => ContentTitle() == "Chi tiết hóa đơn " + SmokeInvoiceNumber), Is.True, ContentTitle());
            Click("btnBack");
            Assert.That(WaitFor(() => ContentTitle() == "Kho hóa đơn"), Is.True, ContentTitle());
            Assert.That(WaitFor(() => Control<KryptonDataGridView>("gridInvoices").Rows.Count == 1), Is.True, "Quay lại vẫn giữ kết quả");
        }

        [Test]
        public void Recovery_RebuildsTheCatalogFromTheStoresOnDisk_AndKeepsTheDamagedFile()
        {
            // Chế độ Recovery tạo companies.json hỏng; thêm một kho có file định danh vào thư mục dữ liệu mặc định.
            System.IO.Directory.CreateDirectory(_dataDir);
            HoaDonDienTu.Infrastructure.Development.DevelopmentDataSeeder.Seed(_dataDir, "Recovery");
            var storePath = System.IO.Path.Combine(_dataDir, "Companies", "0101234567");
            new HoaDonDienTu.Infrastructure.Companies.FileSystemCompanyStorage().Initialize(
                storePath, HoaDonDienTu.Domain.Companies.CompanyId.NewId(), HoaDonDienTu.Domain.Companies.TaxCode.Parse("0101234567"));
            Start("Recovery");

            Assert.That(ContentTitle(), Is.EqualTo("Cần khôi phục"));
            var list = Control<System.Windows.Forms.ListView>("listStores");
            Assert.That(WaitFor(() => list.Items.Count == 1), Is.True, "Quét tự động phải tìm thấy kho");
            Assert.That(list.Items[0].Checked, Is.True);
            Assert.That(WaitFor(() => Control<KryptonButton>("btnRebuild").Enabled), Is.True);

            Click("btnRebuild");

            Assert.That(WaitFor(() => Control<KryptonButton>("btnRestart").Visible), Is.True, "Đăng ký xong thì mời khởi động lại");
            var registered = new HoaDonDienTu.Infrastructure.Companies.JsonCompanyCatalog(System.IO.Path.Combine(_dataDir, "companies.json"))
                .GetItemsAsync(CancellationToken.None).GetAwaiter().GetResult();
            Assert.That(registered.Single().StoragePath, Is.EqualTo(storePath));
            Assert.That(System.IO.Directory.GetFiles(_dataDir, "companies.json.corrupt-*").Length, Is.EqualTo(1), "File danh mục hỏng được giữ lại");
        }

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

        /// <summary>
        /// Như vòng lặp chính của Application.Run: tiếp nối sau await (repository SQLite hoàn tất trên luồng khác) phải quay về luồng giao diện.
        /// Đóng một hộp thoại modal đặt lại context về SynchronizationContext trơn (tiếp nối chạy trên thread pool) nên gọi lại trước mỗi thao tác.
        /// </summary>
        private static void UseUiSynchronizationContext()
        {
            if (!(SynchronizationContext.Current is WindowsFormsSynchronizationContext))
            {
                SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
            }
        }

        private void Click(string name)
        {
            UseUiSynchronizationContext();
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
