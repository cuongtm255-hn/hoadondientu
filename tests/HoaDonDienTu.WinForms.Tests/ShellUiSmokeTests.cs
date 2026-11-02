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

            Assert.That(WaitFor(() => ProgressMessage().Contains("Đã tải xong toàn bộ"), 90000), Is.True, ProgressMessage());

            var grid = Control<KryptonDataGridView>("gridSegments");
            Assert.That(grid.Rows.Count, Is.EqualTo(12), "Năm 2026 → 12 tháng");
            Assert.That(Control<KryptonLabel>("lblStat1Value").Values.Text, Is.EqualTo("100%"));
            Assert.That(Control<KryptonButton>("btnPause").Enabled, Is.False);
            Assert.That(Control<KryptonButton>("btnCancel").Enabled, Is.False, "Phiên đã kết thúc");
            var root = _root.ContextManager.Current.StorageRoot;
            var files = System.IO.Directory.GetFiles(System.IO.Path.Combine(root, "2026"), "*.xml", System.IO.SearchOption.AllDirectories);
            Assert.That(files.Length, Is.GreaterThan(20));
            Assert.That(Control<KryptonLabel>("lblStat3Value").Values.Text, Is.EqualTo(files.Length.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)));
        }

        [Test]
        public void Download_PauseThenResume_ContinuesFromTheCheckpoint()
        {
            LoginAndOpenDownload();
            Click("btnStartDownload");
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
        public void Standard_License_ShowsStandardState_AndActivateNeedsAKey()
        {
            Start("Standard");

            OpenLicenseScreen();

            Assert.That(Control<KryptonLabel>("lblModeTitle").Values.Text, Is.EqualTo("Chế độ chuẩn"));
            Assert.That(Control<KryptonLabel>("lblLast").Values.Text, Is.EqualTo("Chưa kích hoạt"));
            Assert.That(Control<KryptonLabel>("lblMachine").Values.Text, Does.Contain("••••"));
            Assert.That(Control<KryptonButton>("btnActivate").Enabled, Is.False);
            Assert.That(Control<KryptonButton>("btnDeactivate").Enabled, Is.False);

            Control<KryptonTextBox>("txtLicenseKey").Text = "HDDT-SVC-0001";
            Assert.That(Control<KryptonButton>("btnActivate").Enabled, Is.True);
        }

        [Test]
        public void Standard_License_Activate_UnlocksCompanyMenuWithoutRestart()
        {
            Start("Standard");
            Assert.That(Control<KryptonCheckButton>("navCompanies").Visible, Is.False);
            OpenLicenseScreen();

            Control<KryptonTextBox>("txtLicenseKey").Text = "HDDT-SVC-0001";
            Click("btnActivate");

            Assert.That(WaitFor(() => ContentTitle() == "Quản lý doanh nghiệp"), Is.True, "Sau kích hoạt phải vào SCR-10 (AC-13)");
            Assert.That(WaitFor(() => Control<KryptonCheckButton>("navCompanies").Visible), Is.True);
            Assert.That(_root.Entitlement.CurrentMode, Is.EqualTo(HoaDonDienTu.Domain.Common.AppMode.Service));
            Assert.That(System.IO.File.Exists(System.IO.Path.Combine(_dataDir, "license.dat")), Is.True);
        }

        [Test]
        public void Standard_License_Activated_StaysServiceAfterRestart()
        {
            Start("Standard");
            OpenLicenseScreen();
            Control<KryptonTextBox>("txtLicenseKey").Text = "HDDT-SVC-0001";
            Click("btnActivate");
            Assert.That(WaitFor(() => ContentTitle() == "Quản lý doanh nghiệp"), Is.True);

            // Khởi động lại cùng thư mục dữ liệu: license.dat được đọc và xác minh chữ ký ngay lúc khởi động.
            _form.Close();
            _context.Dispose();
            _root.Dispose();
            Start("Standard");

            Assert.That(ContentTitle(), Is.EqualTo("Quản lý doanh nghiệp"));
            Assert.That(_root.Entitlement.CurrentMode, Is.EqualTo(HoaDonDienTu.Domain.Common.AppMode.Service));
            Assert.That(Control<KryptonCheckButton>("navCompanies").Visible, Is.True);
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
        public void ReadOnly_License_ShowsExpiredLabel_AndNoDownloadAction()
        {
            Start("ReadOnly");

            OpenLicenseScreen();

            Assert.That(Control<KryptonLabel>("lblModeTitle").Values.Text, Does.StartWith("Chỉ đọc"));
            Assert.That(Control<KryptonCheckButton>("navDownload").Enabled, Is.False);
            Assert.That(Control<KryptonButton>("btnActivate").Enabled, Is.False, "Chưa nhập key");
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
