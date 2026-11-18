using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Application.PublicService;
using HoaDonDienTu.Domain.PublicService;
using NUnit.Framework;

namespace HoaDonDienTu.Application.Tests
{
    /// <summary>T-DVC02-06..09 (DD-DVC-02). Nguồn giả, dữ liệu "mẫu"; không chạm cổng thật.</summary>
    [TestFixture]
    [Category("Mock")]
    public class TthcServiceTests
    {
        private FakeSource _source;
        private FakeLogger _logger;
        private DvcSession _dvcSession;
        private TthcService _service;
        private TthcSearchSession _session;

        [SetUp]
        public void SetUp()
        {
            _source = new FakeSource();
            _logger = new FakeLogger();
            _dvcSession = new DvcSession();
            _service = new TthcService(_source, _dvcSession, _logger);
            _session = new TthcSearchSession(_service);
        }

        [TestCase("1.010339", true)]
        [TestCase("1", true)]
        [TestCase("", false)]
        [TestCase(null, false)]
        [TestCase("1.0103a9", false)]
        [TestCase("1.010339?x=1", false)]
        [TestCase("../admin", false)]
        [TestCase("1..2", false)]
        public void T_DVC02_06_CodeWhitelist(string code, bool valid)
        {
            Assert.AreEqual(valid, TthcService.IsValidCode(code));
        }

        [Test]
        public void T_DVC02_06_InvalidCode_NeverReachesSource()
        {
            var ex = Assert.Throws<TthcServiceException>(() => _service.GetDetailAsync("x/../y", CancellationToken.None));

            Assert.AreEqual(TthcFailure.InvalidInput, ex.Failure);
            Assert.AreEqual(0, _source.DetailCalls);
        }

        [TestCase(0)]
        [TestCase(-3)]
        [TestCase(1000000)]
        public void T_DVC02_06_PageOutOfRange_NeverReachesSource(int page)
        {
            var ex = Assert.Throws<TthcServiceException>(() => _service.SearchAsync(new TthcQuery { PageNumber = page }, CancellationToken.None));

            Assert.AreEqual(TthcFailure.InvalidInput, ex.Failure);
            Assert.AreEqual(0, _source.SearchCalls.Count);
        }

        [Test]
        public async Task T_DVC02_07_GoToPage_UsesAppliedFilters_NotEditedOnes()
        {
            var filters = new TthcQuery { Keyword = "hóa đơn", Level = "L1" };
            await _session.SearchAsync(filters, CancellationToken.None);

            // Người dùng sửa ô nhập sau khi tìm nhưng chưa bấm Tìm: đổi trang không được dùng giá trị mới.
            filters.Keyword = "khác hẳn";
            await _session.GoToPageAsync(2, CancellationToken.None);

            Assert.AreEqual(2, _source.SearchCalls.Count);
            Assert.AreEqual("hóa đơn", _source.SearchCalls[1].Keyword);
            Assert.AreEqual("L1", _source.SearchCalls[1].Level);
            Assert.AreEqual(2, _source.SearchCalls[1].PageNumber);
            Assert.AreEqual(2, _session.Page.PageNumber);
        }

        [Test]
        public async Task T_DVC02_07_NewSearch_ResetsToPage1()
        {
            await _session.SearchAsync(new TthcQuery { Keyword = "a" }, CancellationToken.None);
            await _session.GoToPageAsync(3, CancellationToken.None);
            await _session.SearchAsync(new TthcQuery { Keyword = "b", PageNumber = 9 }, CancellationToken.None);

            Assert.AreEqual(1, _source.SearchCalls[2].PageNumber);
            Assert.AreEqual("b", _source.SearchCalls[2].Keyword);
        }

        [Test]
        public async Task GoToPage_BeforeAnySearch_OrOutOfRange_IsIgnored()
        {
            Assert.IsFalse(await _session.GoToPageAsync(1, CancellationToken.None));

            await _session.SearchAsync(new TthcQuery(), CancellationToken.None);
            Assert.IsFalse(await _session.GoToPageAsync(0, CancellationToken.None));
            Assert.IsFalse(await _session.GoToPageAsync(_session.Page.PageCount + 1, CancellationToken.None));
            Assert.AreEqual(1, _source.SearchCalls.Count);
        }

        [Test]
        public async Task T_DVC02_08_SecondRequestWhileBusy_IsNotSentInParallel()
        {
            var gate = new TaskCompletionSource<TthcPage>();
            _source.SearchGate = gate;

            Task<bool> first = _session.SearchAsync(new TthcQuery { Keyword = "a" }, CancellationToken.None);
            bool second = await _session.SearchAsync(new TthcQuery { Keyword = "b" }, CancellationToken.None);

            Assert.IsFalse(second);
            Assert.IsTrue(_session.IsBusy);
            Assert.AreEqual(1, _source.SearchCalls.Count);

            gate.SetResult(FakeSource.MakePage(1, 3));
            Assert.IsTrue(await first);
            Assert.IsFalse(_session.IsBusy);
        }

        [Test]
        public async Task T_DVC02_08_Failure_KeepsPreviousPage_AndFilters()
        {
            await _session.SearchAsync(new TthcQuery { Keyword = "a" }, CancellationToken.None);
            _source.Throw = new InvalidOperationException("boom");

            bool ok = await _session.GoToPageAsync(2, CancellationToken.None);

            Assert.IsFalse(ok);
            Assert.AreEqual(TthcFailure.Network, _session.LastFailure);
            Assert.AreEqual(1, _session.Page.PageNumber, "Giữ trang cũ");

            _source.Throw = null;
            await _session.GoToPageAsync(2, CancellationToken.None);
            Assert.AreEqual("a", _source.SearchCalls[_source.SearchCalls.Count - 1].Keyword, "Bộ lọc đã áp dụng vẫn giữ");
        }

        [Test]
        public async Task T_DVC02_08_EmptyResult_IsReturnedNotFailure()
        {
            _source.Empty = true;

            bool ok = await _session.SearchAsync(new TthcQuery { Keyword = "khongco" }, CancellationToken.None);

            Assert.IsTrue(ok);
            Assert.AreEqual(0, _session.Page.Items.Count);
            Assert.AreEqual(TthcFailure.None, _session.LastFailure);
        }

        [Test]
        public async Task ParseError_BecomesPortalChanged_AndSessionExpired_MarksDvcSession()
        {
            _source.Throw = new TthcParseException("sai mẫu");
            await _session.SearchAsync(new TthcQuery(), CancellationToken.None);
            Assert.AreEqual(TthcFailure.PortalChanged, _session.LastFailure);

            _dvcSession.SignedIn("u", "Tổ chức mẫu");
            _source.Throw = new DvcSessionExpiredException();
            await _session.SearchAsync(new TthcQuery(), CancellationToken.None);
            Assert.AreEqual(TthcFailure.SessionExpired, _session.LastFailure);
            Assert.AreEqual(DvcSessionState.Expired, _dvcSession.State);
        }

        [Test]
        public async Task OpenDetail_ThenClose_KeepsList()
        {
            await _session.SearchAsync(new TthcQuery { Keyword = "a" }, CancellationToken.None);

            Assert.IsTrue(await _session.OpenDetailAsync("1.010339", CancellationToken.None));
            Assert.AreEqual("1.010339", _session.Detail.Code);

            _session.CloseDetail();
            Assert.IsNull(_session.Detail);
            Assert.IsNotNull(_session.Page);
        }

        [Test]
        public async Task LoadOptions_Failure_FallsBackToEmpty()
        {
            _source.Throw = new InvalidOperationException("boom");

            await _session.LoadOptionsAsync(CancellationToken.None);

            Assert.AreSame(TthcFilterOptions.Empty, _session.Options);
        }

        [Test]
        public async Task T_DVC02_09_Logs_NeverContainKeywordOrContent()
        {
            const string keyword = "TuKhoaRiengTu";
            await _session.SearchAsync(new TthcQuery { Keyword = keyword }, CancellationToken.None);
            _source.Throw = new InvalidOperationException("lỗi kèm " + keyword + " và nội dung chi tiết");
            await _session.SearchAsync(new TthcQuery { Keyword = keyword }, CancellationToken.None);
            await _session.OpenDetailAsync("1.010339", CancellationToken.None);

            Assert.IsNotEmpty(_logger.Lines);
            foreach (string line in _logger.Lines)
            {
                StringAssert.DoesNotContain(keyword, line);
                StringAssert.DoesNotContain("nội dung chi tiết", line);
            }
        }

        private sealed class FakeSource : ITthcSource
        {
            public readonly List<TthcQuery> SearchCalls = new List<TthcQuery>();
            public int DetailCalls;
            public Exception Throw;
            public bool Empty;
            public TaskCompletionSource<TthcPage> SearchGate;

            public static TthcPage MakePage(int page, int pageCount)
            {
                return new TthcPage(new[] { new TthcSummary("1.000001", "Thủ tục (mẫu)", "Toàn trình", "Cơ quan (mẫu)") }, 30, 28, page, pageCount);
            }

            public Task<TthcFilterOptions> GetFilterOptionsAsync(CancellationToken cancellationToken)
            {
                if (Throw != null) throw Throw;
                return Task.FromResult(TthcFilterOptions.Empty);
            }

            public Task<TthcPage> SearchAsync(TthcQuery query, CancellationToken cancellationToken)
            {
                SearchCalls.Add(query);
                if (SearchGate != null) return SearchGate.Task;
                if (Throw != null) throw Throw;
                if (Empty) return Task.FromResult(new TthcPage(null, 0, 0, 1, 0));
                return Task.FromResult(MakePage(query.PageNumber, 3));
            }

            public Task<TthcDetail> GetDetailAsync(string code, CancellationToken cancellationToken)
            {
                DetailCalls++;
                if (Throw != null) throw Throw;
                return Task.FromResult(new TthcDetail(code, "Thủ tục (mẫu)", null));
            }
        }

        private sealed class FakeLogger : IApplicationLogger
        {
            public readonly List<string> Lines = new List<string>();
            public void Info(string message) { Lines.Add(message); }
            public void Warning(string message) { Lines.Add(message); }
            public void Error(string message, Exception exception) { Lines.Add(message); }
        }
    }
}
