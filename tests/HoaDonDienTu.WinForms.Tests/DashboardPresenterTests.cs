using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Dashboard;
using HoaDonDienTu.Application.Invoices;
using HoaDonDienTu.Application.Navigation;
using HoaDonDienTu.Domain.Common;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.Domain.Downloads;
using HoaDonDienTu.Domain.Invoices;
using HoaDonDienTu.WinForms.Archive;
using HoaDonDienTu.WinForms.Dashboard;
using NUnit.Framework;

namespace HoaDonDienTu.WinForms.Tests
{
    [TestFixture]
    public class DashboardPresenterTests : CompanyUiTestBase
    {
        private FakeDashboardService _service;
        private FakeDashboardView _view;
        private DashboardPresenter _presenter;

        [SetUp]
        public void SetUp()
        {
            Context.Current = new CompanyContext(CompanyId.NewId(), TaxCode.Parse("0101234567"), "Công ty", @"C:\Kho\0101234567", null);
            _service = new FakeDashboardService { Snapshot = Snapshot() };
            _view = new FakeDashboardView();
        }

        [TearDown]
        public void TearDown()
        {
            if (_presenter != null) _presenter.Dispose();
        }

        private static DashboardSnapshot Snapshot()
        {
            return new DashboardSnapshot
            {
                TotalInvoices = 24862, PurchaseCount = 18421, SoldCount = 6441, Year = 2026, YearPayment = 18400000000m,
                LastSyncAtUtc = new DateTime(2026, 10, 31, 6, 0, 0, DateTimeKind.Utc), LastSyncState = DownloadSessionState.Completed, LastSyncInvoices = 128,
                Active = new List<DashboardSession>(), Attention = new List<AttentionItem>(), Recent = new List<RecentInvoice>()
            };
        }

        private DashboardPresenter Open()
        {
            _presenter = new DashboardPresenter(_view, _service, Context, Entitlement, Nav, Runner, Errors);
            _presenter.Initialize();
            return _presenter;
        }

        private static bool WaitFor(Func<bool> condition)
        {
            return UiWait.Until(condition);
        }

        [Test]
        public void Opens_WithCompactNumbersAndSyncText()
        {
            Open();

            Assert.That(WaitFor(() => _view.Result != null), Is.True);
            var r = _view.Result;
            Assert.That(r.TotalInvoices, Is.EqualTo("24.862"));
            Assert.That(r.TotalSub, Is.EqualTo("Mua vào 18.421 · Bán ra 6.441"));
            Assert.That(r.YearCaption, Is.EqualTo("TỔNG THANH TOÁN NĂM 2026"));
            Assert.That(r.YearPayment, Is.EqualTo("18,4 tỷ"));
            Assert.That(r.SyncValue, Is.EqualTo(new DateTime(2026, 10, 31, 6, 0, 0, DateTimeKind.Utc).ToLocalTime().ToString("dd/MM/yyyy")));
            Assert.That(r.SyncSub, Is.EqualTo("Hoàn tất · 128 hóa đơn"));
            Assert.That(r.AttentionValue, Is.EqualTo("0"));
            Assert.That(r.AttentionSub, Is.EqualTo("Không có việc cần xử lý"));
            Assert.That(r.HasActiveSession, Is.False);
        }

        [Test]
        public void NeverSynced_ShowsAPlaceholder()
        {
            _service.Snapshot.LastSyncAtUtc = null;
            _service.Snapshot.LastSyncState = null;

            Open();

            Assert.That(WaitFor(() => _view.Result != null), Is.True);
            Assert.That(_view.Result.SyncValue, Is.EqualTo("—"));
            Assert.That(_view.Result.SyncSub, Is.EqualTo("Chưa có lần đồng bộ nào"));
        }

        [Test]
        public void NoCompany_ShowsAHelpfulMessage_AndLoadsNothing()
        {
            Context.Current = null;

            Open();

            Assert.That(WaitFor(() => _view.Message != null), Is.True);
            Assert.That(_view.Message, Does.Contain("Chưa chọn doanh nghiệp"));
            Assert.That(_view.Result, Is.Null);
            Assert.That(_service.Calls, Is.EqualTo(0));
        }

        [Test]
        public void ActiveSession_ShowsProgress_AndOpensItsProgressScreen()
        {
            var id = Guid.NewGuid();
            _service.Snapshot.Active.Add(new DashboardSession
            {
                SessionId = id, Direction = InvoiceDirection.Purchase, PeriodText = "Năm 2026", State = DownloadSessionState.Running,
                SegmentCount = 12, CompletedSegments = 7, InvoiceCount = 1846, ErrorCount = 1
            });
            Open();
            Assert.That(WaitFor(() => _view.Result != null), Is.True);
            var r = _view.Result;

            Assert.That(r.HasActiveSession, Is.True);
            Assert.That(r.SessionTitle, Is.EqualTo("Hóa đơn mua vào · Năm 2026"));
            Assert.That(r.SessionBadge, Is.EqualTo("Đang tải"));
            Assert.That(r.ProgressPercent, Is.EqualTo(58));
            Assert.That(r.ProgressText, Is.EqualTo("7/12 đoạn"));
            Assert.That(r.SessionStats, Is.EqualTo("1.846 hóa đơn · 1 lỗi"));

            _view.RaiseActiveSession();

            Assert.That(WaitFor(() => Nav.Targets.Count == 1), Is.True);
            Assert.That(Nav.Targets[0], Is.EqualTo(ScreenId.DownloadProgress));
            Assert.That(Nav.Parameters[0], Is.EqualTo(id));
        }

        [Test]
        public void MoreThanOneActiveSession_MentionsTheOthers()
        {
            _service.Snapshot.Active.Add(new DashboardSession { SessionId = Guid.NewGuid(), PeriodText = "Q1/2026", State = DownloadSessionState.Running, SegmentCount = 3 });
            _service.Snapshot.Active.Add(new DashboardSession { SessionId = Guid.NewGuid(), PeriodText = "Q2/2026", State = DownloadSessionState.PausedByUser, SegmentCount = 3 });

            Open();

            Assert.That(WaitFor(() => _view.Result != null), Is.True);
            Assert.That(_view.Result.SessionDetail, Is.EqualTo("Còn 1 phiên khác chưa kết thúc"));
        }

        [Test]
        public void Attention_ListsMissingFilesAndErrorSessions_WithCounts()
        {
            var session = Guid.NewGuid();
            _service.Snapshot.MissingFileCount = 2;
            _service.Snapshot.ErrorSessionCount = 1;
            _service.Snapshot.AttentionCount = 3;
            _service.Snapshot.Attention.Add(new AttentionItem { Kind = AttentionKind.MissingFiles, Count = 2, Direction = InvoiceDirection.Sold });
            _service.Snapshot.Attention.Add(new AttentionItem { Kind = AttentionKind.SessionNeedsAttention, SessionId = session, PeriodText = "Tháng 05/2026", State = DownloadSessionState.CompletedWithErrors });

            Open();

            Assert.That(WaitFor(() => _view.Result != null), Is.True);
            var r = _view.Result;
            Assert.That(r.AttentionValue, Is.EqualTo("3"));
            Assert.That(r.AttentionSub, Is.EqualTo("2 XML thiếu · 1 phiên lỗi"));
            Assert.That(r.Attention[0].Badge, Is.EqualTo("File thiếu"));
            Assert.That(r.Attention[0].Text, Is.EqualTo("2 XML không còn trong thư mục kho"));
            Assert.That(r.Attention[0].ActionText, Is.EqualTo("Kiểm tra"));
            Assert.That(r.Attention[1].Badge, Is.EqualTo("Phiên lỗi"));
            Assert.That(r.Attention[1].Text, Is.EqualTo("Tháng 05/2026 cần thử lại"));
        }

        [Test]
        public void MissingFilesAttention_OpensTheArchiveFilteredToProblemsInTheRightDirection()
        {
            _service.Snapshot.Attention.Add(new AttentionItem { Kind = AttentionKind.MissingFiles, Count = 2, Direction = InvoiceDirection.Sold });
            Open();
            Assert.That(WaitFor(() => _view.Result != null), Is.True);

            _view.RaiseAttention(0);

            Assert.That(WaitFor(() => Nav.Targets.Count == 1), Is.True);
            Assert.That(Nav.Targets[0], Is.EqualTo(ScreenId.InvoiceArchive));
            var request = (ArchiveNavigationRequest)Nav.Parameters[0];
            Assert.That(request.Direction, Is.EqualTo(InvoiceDirection.Sold));
            Assert.That(request.FileFilter, Is.EqualTo(FileIntegrityFilter.Problems));
        }

        [Test]
        public void SessionAttention_OpensThatSession_AndAFailedOneSaysItCannotContinue()
        {
            var session = Guid.NewGuid();
            _service.Snapshot.Attention.Add(new AttentionItem { Kind = AttentionKind.SessionNeedsAttention, SessionId = session, PeriodText = "Q3/2026", State = DownloadSessionState.Failed });
            Open();
            Assert.That(WaitFor(() => _view.Result != null), Is.True);
            Assert.That(_view.Result.Attention[0].Text, Is.EqualTo("Q3/2026 không thể tiếp tục"));

            _view.RaiseAttention(0);

            Assert.That(WaitFor(() => Nav.Targets.Count == 1), Is.True);
            Assert.That(Nav.Targets[0], Is.EqualTo(ScreenId.DownloadProgress));
            Assert.That(Nav.Parameters[0], Is.EqualTo(session));
        }

        [Test]
        public void AttentionIndexOutOfRange_IsIgnored()
        {
            Open();
            Assert.That(WaitFor(() => _view.Result != null), Is.True);

            _view.RaiseAttention(5);
            _view.RaiseAttention(-1);

            Thread.Sleep(80);
            Assert.That(Nav.Targets, Is.Empty);
        }

        [Test]
        public void Recent_ShowsPartnerStatusAndAmount()
        {
            _service.Snapshot.Recent.Add(new RecentInvoice
            {
                IssueDate = new DateTime(2026, 10, 31), Direction = InvoiceDirection.Purchase, CounterpartyName = "Công ty ABC", CounterpartyId = "0109988776",
                InvoiceNumber = "0000128", FileState = ArchiveFileState.Ok, TotalPayment = 33000000m
            });
            _service.Snapshot.Recent.Add(new RecentInvoice
            {
                IssueDate = new DateTime(2026, 10, 30), Direction = InvoiceDirection.Sold, CounterpartyName = "Công ty DEF", CounterpartyId = "0105566778",
                InvoiceNumber = "0000451", FileState = ArchiveFileState.MultiVersion, TotalPayment = 18250000m
            });
            _service.Snapshot.Recent.Add(new RecentInvoice
            {
                IssueDate = new DateTime(2026, 10, 29), Direction = InvoiceDirection.Sold, CounterpartyName = "Khách lẻ", CounterpartyId = null,
                InvoiceNumber = "0000450", FileState = ArchiveFileState.Missing, TotalPayment = null
            });

            Open();

            Assert.That(WaitFor(() => _view.Result != null), Is.True);
            var rows = _view.Result.Recent;
            Assert.That(rows[0].Partner, Is.EqualTo("Công ty ABC · 0109988776"));
            Assert.That(rows[0].Status, Is.EqualTo("Đã lưu XML"));
            Assert.That(rows[0].Payment, Is.EqualTo("33.000.000"));
            Assert.That(rows[1].Status, Is.EqualTo("Có phiên bản mới"));
            Assert.That(rows[1].Direction, Is.EqualTo("Bán ra"));
            Assert.That(rows[2].Partner, Is.EqualTo("Khách lẻ"));
            Assert.That(rows[2].Status, Is.EqualTo("Thiếu XML"));
            Assert.That(rows[2].Tone, Is.EqualTo(DashboardTone.Bad));
            Assert.That(rows[2].Payment, Is.Empty);
        }

        [Test]
        public void OpenArchiveAndNewDownload_Navigate()
        {
            Open();
            Assert.That(WaitFor(() => _view.Result != null), Is.True);

            _view.RaiseOpenArchive();
            Assert.That(WaitFor(() => Nav.Targets.Count == 1), Is.True);
            _view.RaiseNewDownload();
            Assert.That(WaitFor(() => Nav.Targets.Count == 2), Is.True);

            Assert.That(Nav.Targets, Is.EqualTo(new[] { ScreenId.InvoiceArchive, ScreenId.Download }));
            Assert.That(Nav.Parameters[0], Is.Null, "Mở Kho hóa đơn không kèm bộ lọc");
        }

        [Test]
        public void ReadOnlyServiceMode_LocksDownload_AndSaysWhy()
        {
            Entitlement.Value = AppMode.ReadOnlyService;

            Open();

            Assert.That(WaitFor(() => _view.Mode != null), Is.True);
            Assert.That(_view.Mode.CanDownload, Is.False);
            Assert.That(_view.Mode.Note, Is.EqualTo("Chỉ đọc – cần gia hạn licence"));
        }

        [TestCase(AppMode.Standard)]
        [TestCase(AppMode.Service)]
        public void FullModes_AllowDownload_WithoutANote(AppMode mode)
        {
            Entitlement.Value = mode;

            Open();

            Assert.That(WaitFor(() => _view.Mode != null), Is.True);
            Assert.That(_view.Mode.CanDownload, Is.True);
            Assert.That(_view.Mode.Note, Is.Null);
        }

        [Test]
        public void ServiceError_IsShownToTheUser_NotThrown()
        {
            _service.Fail = true;

            Open();

            Assert.That(WaitFor(() => Errors.Shown.Count > 0), Is.True);
            Assert.That(_view.Result, Is.Null);
        }

        private sealed class FakeDashboardService : IDashboardService
        {
            public DashboardSnapshot Snapshot;
            public bool Fail;
            public int Calls;

            public Task<DashboardSnapshot> GetAsync(CancellationToken cancellationToken)
            {
                Calls++;
                if (Fail) throw new InvalidOperationException("boom");
                return Task.FromResult(Snapshot);
            }
        }

        private sealed class ModeCall
        {
            public bool CanDownload;
            public string Note;
        }

        private sealed class FakeDashboardView : IDashboardView
        {
            private readonly object _gate = new object();
            private DashboardViewModel _result;
            private string _message;
            private ModeCall _mode;

            public event EventHandler NewDownloadRequested;
            public event EventHandler ActiveSessionRequested;
            public event EventHandler OpenArchiveRequested;
            public event EventHandler<AttentionRequestedEventArgs> AttentionRequested;

            public DashboardViewModel Result { get { lock (_gate) { return _result; } } }
            public string Message { get { lock (_gate) { return _message; } } }
            public ModeCall Mode { get { lock (_gate) { return _mode; } } }

            public void ShowResult(DashboardViewModel model)
            {
                lock (_gate) { _result = model; }
            }

            public void ShowMode(bool canDownload, string note)
            {
                lock (_gate) { _mode = new ModeCall { CanDownload = canDownload, Note = note }; }
            }

            public void ShowMessage(string text, bool isError)
            {
                lock (_gate) { _message = text; }
            }

            public void RaiseNewDownload() { NewDownloadRequested(this, EventArgs.Empty); }
            public void RaiseActiveSession() { ActiveSessionRequested(this, EventArgs.Empty); }
            public void RaiseOpenArchive() { OpenArchiveRequested(this, EventArgs.Empty); }
            public void RaiseAttention(int index) { AttentionRequested(this, new AttentionRequestedEventArgs(index)); }
        }
    }
}
