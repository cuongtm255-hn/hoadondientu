using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.WinForms.Common;
using HoaDonDienTu.WinForms.Companies;
using NUnit.Framework;

namespace HoaDonDienTu.WinForms.Tests
{
    /// <summary>Màn hình khôi phục danh mục doanh nghiệp (T02-07) trên dịch vụ giả; control thật được thử riêng trong một form.</summary>
    [TestFixture]
    [Apartment(System.Threading.ApartmentState.STA)]
    public class RecoveryPresenterTests : CompanyUiTestBase
    {
        private FakeView _view;
        private FakeService _service;
        private FakePicker _picker;
        private int _restarts;
        private RecoveryPresenter _presenter;

        [SetUp]
        public void SetUp()
        {
            _view = new FakeView();
            _service = new FakeService();
            _picker = new FakePicker();
            _restarts = 0;
            _presenter = new RecoveryPresenter(_view, _service, Runner, Errors, () => new List<string> { @"D:\Data\Companies" }, _picker, () => _restarts++);
        }

        [TearDown]
        public void TearDown()
        {
            _presenter.Dispose();
        }

        private static RecoveredStore Store(string path, string tax, bool registered = false, string problem = null)
        {
            return new RecoveredStore { Path = path, TaxCode = TaxCode.Parse(tax), CompanyId = CompanyId.NewId(), AlreadyRegistered = registered, Problem = problem };
        }

        [Test]
        public void Opening_ExplainsWhy_AndScansTheDefaultFolderAtOnce()
        {
            _service.Stores.Add(Store(@"D:\Data\Companies\0101234567", "0101234567"));

            _presenter.Initialize();

            Assert.That(_view.Intro, Does.Contain("không đọc được").And.Contain("tránh tạo kho rỗng"));
            Assert.That(UiWait.Until(() => _view.Rows != null), Is.True);
            Assert.That(_service.ScannedFolders.Single(), Is.EqualTo(new[] { @"D:\Data\Companies" }));
            Assert.That(_view.Rows.Single().TaxCode, Is.EqualTo("0101234567"));
            Assert.That(_view.Rows.Single().CanRegister, Is.True);
            Assert.That(_view.Message, Does.Contain("Tìm thấy 1 kho"));
        }

        [Test]
        public void NothingFound_SaysWhereItLooked_AsAnError()
        {
            _presenter.Initialize();

            Assert.That(UiWait.Until(() => _view.Message != null && _view.Message.StartsWith("Không tìm thấy")), Is.True, _view.Message);
            Assert.That(_view.Message, Does.Contain(@"D:\Data\Companies").And.Contain("Chọn thư mục khác"));
            Assert.That(_view.MessageIsError, Is.True);
        }

        [Test]
        public void Rows_ShowWhyAStoreCannotBeRegistered()
        {
            _service.Stores.Add(Store(@"D:\A", "0101234567", registered: true));
            _service.Stores.Add(Store(@"D:\B", "0109988776", problem: "Trùng MST với kho D:\\A."));
            _service.Stores.Add(Store(@"D:\C", "0100100001"));

            _presenter.Initialize();

            Assert.That(UiWait.Until(() => _view.Rows != null && _view.Rows.Count == 3), Is.True);
            Assert.That(_view.Rows.Single(r => r.Path == @"D:\A").Status, Is.EqualTo("Đã có trong danh mục"));
            Assert.That(_view.Rows.Single(r => r.Path == @"D:\B").Status, Does.StartWith("Trùng MST"));
            Assert.That(_view.Rows.Single(r => r.Path == @"D:\C").Status, Is.EqualTo("Có thể đăng ký"));
            Assert.That(_view.Rows.Count(r => r.CanRegister), Is.EqualTo(1));
        }

        [Test]
        public void BrowsingForAnotherFolder_ScansItToo_AndKeepsTheDefault()
        {
            _presenter.Initialize();
            Assert.That(UiWait.Until(() => _service.ScannedFolders.Count == 1), Is.True);
            _picker.Result = @"E:\Kho-cu";

            _view.RaiseBrowse();

            Assert.That(UiWait.Until(() => _service.ScannedFolders.Count == 2), Is.True);
            Assert.That(_service.ScannedFolders[1], Is.EqualTo(new[] { @"D:\Data\Companies", @"E:\Kho-cu" }));

            _picker.Result = null;
            _view.RaiseBrowse();
            Thread.Sleep(80);
            Assert.That(_service.ScannedFolders.Count, Is.EqualTo(2), "Hủy hộp thoại không quét lại");
        }

        [Test]
        public void Rebuild_RegistersOnlyTheTickedStores_ThenOffersARestart()
        {
            _service.Stores.Add(Store(@"D:\A", "0101234567"));
            _service.Stores.Add(Store(@"D:\B", "0109988776"));
            _service.Rebuilt = new RecoveryResult { Registered = 1, NotRegistered = new List<string>(), QuarantinedCatalog = @"D:\Data\companies.json.corrupt-1" };
            _presenter.Initialize();
            Assert.That(UiWait.Until(() => _view.Rows != null && _view.Rows.Count == 2), Is.True);
            _view.Selected = new List<string> { @"D:\A" };

            _view.RaiseRebuild();

            Assert.That(UiWait.Until(() => _service.RebuildCalls.Count == 1), Is.True);
            Assert.That(_service.RebuildCalls[0].Select(s => s.Path), Is.EqualTo(new[] { @"D:\A" }));
            Assert.That(UiWait.Until(() => _view.RestartShown), Is.True);
            Assert.That(_view.Message, Does.Contain("Đã đăng ký 1 kho").And.Contain("companies.json.corrupt-1").And.Contain("Khởi động lại"));
            Assert.That(_view.MessageIsError, Is.False);
        }

        [Test]
        public void Rebuild_WithNothingTicked_ChangesNothing()
        {
            _service.Stores.Add(Store(@"D:\A", "0101234567"));
            _presenter.Initialize();
            Assert.That(UiWait.Until(() => _view.Rows != null), Is.True);
            _view.Selected = new List<string>();

            _view.RaiseRebuild();

            Assert.That(_view.Message, Is.EqualTo("Chưa chọn kho nào để đăng ký."));
            Assert.That(_view.MessageIsError, Is.True);
            Assert.That(_service.RebuildCalls, Is.Empty);
        }

        [Test]
        public void Rebuild_IgnoresTickedStoresThatCannotBeRegistered()
        {
            _service.Stores.Add(Store(@"D:\A", "0101234567", registered: true));
            _presenter.Initialize();
            Assert.That(UiWait.Until(() => _view.Rows != null), Is.True);
            _view.Selected = new List<string> { @"D:\A" };

            _view.RaiseRebuild();

            Assert.That(_service.RebuildCalls, Is.Empty);
            Assert.That(_view.Message, Is.EqualTo("Chưa chọn kho nào để đăng ký."));
        }

        [Test]
        public void Restart_CallsTheRestartAction()
        {
            _presenter.Initialize();

            _view.RaiseRestart();

            Assert.That(_restarts, Is.EqualTo(1));
        }

        [Test]
        public void TheRealControl_ShowsRows_TicksTheRegistrableOnes_AndEnablesRegister()
        {
            using (var form = new Form { Width = 1000, Height = 700 })
            {
                var control = new RecoveryControl { Dock = DockStyle.Fill };
                form.Controls.Add(control);
                form.Show();

                control.ShowStores(new List<RecoveryRow>
                {
                    new RecoveryRow { Path = @"D:\A", TaxCode = "0101234567", Status = "Có thể đăng ký", CanRegister = true },
                    new RecoveryRow { Path = @"D:\B", TaxCode = "0109988776", Status = "Đã có trong danh mục", CanRegister = false }
                });

                Assert.That(control.SelectedPaths, Is.EqualTo(new[] { @"D:\A" }));
                var rebuild = (ComponentFactory.Krypton.Toolkit.KryptonButton)control.Controls.Find("btnRebuild", true).Single();
                Assert.That(rebuild.Enabled, Is.True);

                control.SetBusy(true);
                Assert.That(rebuild.Enabled, Is.False);
                control.SetBusy(false);

                control.ShowRestart(true);
                Assert.That(control.Controls.Find("btnRestart", true).Single().Visible, Is.True);
                Assert.That(rebuild.Visible, Is.False);
            }
        }

        // ---- Fakes ----

        private sealed class FakeView : IRecoveryView
        {
            private readonly object _gate = new object();
            private string _message;
            private bool _isError;
            private IList<RecoveryRow> _rows;
            private bool _restartShown;

            public event EventHandler ScanRequested;
            public event EventHandler BrowseRequested;
            public event EventHandler RebuildRequested;
            public event EventHandler RestartRequested;

            public string Intro;
            public IList<string> Selected = new List<string>();

            public IList<string> SelectedPaths { get { return Selected; } }
            public string Message { get { lock (_gate) { return _message; } } }
            public bool MessageIsError { get { lock (_gate) { return _isError; } } }
            public IList<RecoveryRow> Rows { get { lock (_gate) { return _rows; } } }
            public bool RestartShown { get { lock (_gate) { return _restartShown; } } }

            public void ShowIntro(string text) { Intro = text; }
            public void ShowStores(IList<RecoveryRow> rows) { lock (_gate) { _rows = rows; } }
            public void ShowMessage(string text, bool isError) { lock (_gate) { _message = text; _isError = isError; } }
            public void SetBusy(bool busy) { }
            public void ShowRestart(bool visible) { lock (_gate) { _restartShown = visible; } }

            public void RaiseBrowse() { BrowseRequested(this, EventArgs.Empty); }
            public void RaiseRebuild() { RebuildRequested(this, EventArgs.Empty); }
            public void RaiseRestart() { RestartRequested(this, EventArgs.Empty); }
        }

        private sealed class FakeService : ICompanyRecoveryService
        {
            public readonly List<RecoveredStore> Stores = new List<RecoveredStore>();
            public readonly List<string[]> ScannedFolders = new List<string[]>();
            public readonly List<IList<RecoveredStore>> RebuildCalls = new List<IList<RecoveredStore>>();
            public RecoveryResult Rebuilt = new RecoveryResult { Registered = 0, NotRegistered = new List<string>() };

            public Task<RecoveryScanResult> ScanAsync(IEnumerable<string> folders, CancellationToken cancellationToken)
            {
                lock (ScannedFolders) { ScannedFolders.Add(folders.ToArray()); }
                return Task.FromResult(new RecoveryScanResult { Stores = Stores.ToList(), Skipped = new List<string>() });
            }

            public Task<RecoveryResult> RebuildAsync(IEnumerable<RecoveredStore> stores, CancellationToken cancellationToken)
            {
                lock (RebuildCalls) { RebuildCalls.Add(stores.ToList()); }
                return Task.FromResult(Rebuilt);
            }
        }

        private sealed class FakePicker : IFolderPicker
        {
            public string Result;

            public string Pick(string initialPath) { return Result; }
        }
    }
}
