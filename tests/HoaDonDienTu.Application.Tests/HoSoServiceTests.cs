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
    /// <summary>T-DVC03-01..08 (DD-DVC-03). Nguồn giả, dữ liệu "mẫu"; không chạm cổng thật.</summary>
    [TestFixture]
    [Category("Mock")]
    public class HoSoServiceTests
    {
        private const string Captcha = "kY7Q2";
        private const string TaxCode = "0109999999";
        private const string FileCode = "HS-MAU-0001";

        private FakeSource _source;
        private FakeLogger _logger;
        private DvcSession _dvc;
        private HoSoService _service;
        private HoSoSearchSession _session;

        [SetUp]
        public async Task SetUp()
        {
            _source = new FakeSource();
            _logger = new FakeLogger();
            _dvc = new DvcSession();
            _dvc.SignedIn("u", "Tổ chức mẫu");
            _service = new HoSoService(_source, _dvc, _logger);
            _session = new HoSoSearchSession(_service);
            await _session.SelectSourceAsync(HoSoSource.Dvc, CancellationToken.None);
        }

        private static HoSoQuery Query(string captcha = Captcha, string keyword = "a")
        {
            return new HoSoQuery
            {
                FromDate = new DateTime(2026, 11, 1),
                ToDate = new DateTime(2026, 11, 20),
                CaptchaText = captcha,
                ProcedureName = keyword,
                AuthorizedTaxCode = TaxCode,
                FileCode = FileCode
            };
        }

        [Test]
        public void T_DVC03_01_NotLoggedIn_NeverReachesSource()
        {
            _dvc.Reset();
            var ex = Assert.ThrowsAsync<HoSoServiceException>(() => _service.SearchAsync(Query(), CancellationToken.None));

            Assert.AreEqual(HoSoFailure.NotLoggedIn, ex.Failure);
            Assert.AreEqual(0, _source.SearchCalls.Count);
        }

        [Test]
        public void T_DVC03_01_NotLoggedIn_CaptchaIsNotRequested()
        {
            _dvc.Reset();
            _source.CaptchaCalls = 0;

            Assert.ThrowsAsync<HoSoServiceException>(() => _service.GetCaptchaAsync(HoSoSource.Dvc, CancellationToken.None));
            Assert.AreEqual(0, _source.CaptchaCalls);
        }

        [Test]
        public void T_DVC03_02_EmptyCaptcha_ReversedDates_BadPage_AreBlocked()
        {
            var empty = Query(captcha: "  ");
            var reversed = Query();
            reversed.FromDate = new DateTime(2026, 12, 1);
            var badPage = Query();
            badPage.PageNumber = 0;

            foreach (var q in new[] { empty, reversed, badPage })
            {
                q.PageNumber = q.PageNumber == 0 && q != badPage ? 1 : q.PageNumber;
                var ex = Assert.ThrowsAsync<HoSoServiceException>(() => _service.SearchAsync(q, CancellationToken.None));
                Assert.AreEqual(HoSoFailure.InvalidInput, ex.Failure);
            }
            Assert.AreEqual(0, _source.SearchCalls.Count);
        }

        [Test]
        public async Task T_DVC03_03_AfterEverySubmit_CaptchaIsDroppedAndReloaded_NoAutoRetry()
        {
            int loadsBefore = _source.CaptchaCalls;

            // thành công
            Assert.IsTrue(await _session.SearchAsync(Query(), CancellationToken.None));
            Assert.AreEqual(loadsBefore + 1, _source.CaptchaCalls);

            // sai CAPTCHA
            _source.Throw = new HoSoServiceException(HoSoFailure.WrongCaptcha);
            Assert.IsFalse(await _session.SearchAsync(Query(), CancellationToken.None));
            Assert.AreEqual(loadsBefore + 2, _source.CaptchaCalls);

            // lỗi mạng
            _source.Throw = new InvalidOperationException("boom");
            Assert.IsFalse(await _session.SearchAsync(Query(), CancellationToken.None));
            Assert.AreEqual(loadsBefore + 3, _source.CaptchaCalls);

            Assert.AreEqual(3, _source.SearchCalls.Count, "Mỗi lần bấm Tìm đúng một yêu cầu, không tự gửi lại");
            Assert.IsNotNull(_session.CaptchaImage);
        }

        [Test]
        public async Task T_DVC03_03_Paging_ReusesTheSearchCaptcha_WithoutAskingAgain()
        {
            await _session.SearchAsync(Query(), CancellationToken.None);
            Assert.IsTrue(_session.CanPageWithoutNewCaptcha);

            Assert.IsTrue(await _session.GoToPageAsync(2, CancellationToken.None));

            Assert.AreEqual(Captcha, _source.SearchCalls[1].CaptchaText, "Đổi trang dùng đúng mã của lần tìm (giống trang cổng)");
            Assert.AreEqual(2, _source.SearchCalls[1].PageNumber);
            Assert.IsTrue(_session.CanPageWithoutNewCaptcha, "Vẫn đổi trang tiếp được");
        }

        [Test]
        public async Task T_DVC03_03_NewSearch_DropsOldPagingCaptcha_UntilItSucceeds()
        {
            await _session.SearchAsync(Query(), CancellationToken.None);
            _source.Throw = new HoSoServiceException(HoSoFailure.WrongCaptcha);

            await _session.SearchAsync(Query(captcha: "sai99"), CancellationToken.None);

            Assert.IsFalse(_session.CanPageWithoutNewCaptcha, "Tìm mới thất bại: không giữ mã cũ");
            _source.Throw = null;
            Assert.IsFalse(await _session.GoToPageAsync(2, CancellationToken.None), "Không có mã nào thì không gọi cổng");
            Assert.AreEqual(HoSoFailure.InvalidInput, _session.LastFailure);
            Assert.AreEqual(2, _source.SearchCalls.Count);
        }

        [Test]
        public async Task T_DVC03_03_PagingRejectedByPortal_ForgetsCaptcha_AndAsksForNewOne()
        {
            await _session.SearchAsync(Query(), CancellationToken.None);
            _source.Throw = new HoSoServiceException(HoSoFailure.WrongCaptcha);

            Assert.IsFalse(await _session.GoToPageAsync(2, CancellationToken.None));

            Assert.AreEqual(HoSoFailure.WrongCaptcha, _session.LastFailure);
            Assert.IsFalse(_session.CanPageWithoutNewCaptcha);
            Assert.AreEqual(2, _source.SearchCalls.Count, "Không tự gửi lại");

            _source.Throw = null;
            Assert.IsTrue(await _session.GoToPageAsync(2, "moi99", CancellationToken.None));
            Assert.AreEqual("moi99", _source.SearchCalls[2].CaptchaText);
            Assert.IsTrue(_session.CanPageWithoutNewCaptcha);
        }

        [Test]
        public async Task T_DVC03_03_PagingCaptcha_IsForgottenOnSourceChange_AndOnClear()
        {
            await _session.SearchAsync(Query(), CancellationToken.None);
            await _session.SelectSourceAsync(HoSoSource.ThueDienTu, CancellationToken.None);
            Assert.IsFalse(_session.CanPageWithoutNewCaptcha);

            await _session.SelectSourceAsync(HoSoSource.Dvc, CancellationToken.None);
            await _session.SearchAsync(Query(), CancellationToken.None);
            Assert.IsTrue(_session.CanPageWithoutNewCaptcha);
            _session.ClearPagingCaptcha();
            Assert.IsFalse(_session.CanPageWithoutNewCaptcha);
        }

        [Test]
        public async Task T_DVC03_04_WrongCaptcha_KeepsPreviousPage_AndReportsWrongCaptcha()
        {
            await _session.SearchAsync(Query(), CancellationToken.None);
            HoSoPage before = _session.Page;
            _source.Throw = new HoSoServiceException(HoSoFailure.WrongCaptcha);

            bool ok = await _session.SearchAsync(Query(keyword: "khác"), CancellationToken.None);

            Assert.IsFalse(ok);
            Assert.AreEqual(HoSoFailure.WrongCaptcha, _session.LastFailure);
            Assert.AreSame(before, _session.Page, "Giữ kết quả cũ");
        }

        [Test]
        public async Task T_DVC03_05_EmptyResult_IsValidPageWithMessage_NotFailure()
        {
            _source.Empty = true;

            bool ok = await _session.SearchAsync(Query(), CancellationToken.None);

            Assert.IsTrue(ok);
            Assert.AreEqual(0, _session.Page.Rows.Count);
            Assert.AreEqual("Không có dữ liệu", _session.Page.EmptyMessage);
            Assert.AreEqual(HoSoFailure.None, _session.LastFailure);
        }

        [Test]
        public async Task T_DVC03_06_Expired_MarksSession_StopsCaptchaReload_NoRelogin()
        {
            _source.Throw = new DvcSessionExpiredException();

            bool ok = await _session.SearchAsync(Query(), CancellationToken.None);

            Assert.IsFalse(ok);
            Assert.AreEqual(HoSoFailure.SessionExpired, _session.LastFailure);
            Assert.AreEqual(DvcSessionState.Expired, _dvc.State);
            Assert.IsNull(_session.CaptchaImage);
            Assert.AreEqual(1, _source.SearchCalls.Count);
        }

        [Test]
        public async Task T_DVC03_07_SecondRequestWhileBusy_IsNotSentInParallel()
        {
            var gate = new TaskCompletionSource<HoSoPage>();
            _source.SearchGate = gate;

            Task<bool> first = _session.SearchAsync(Query(), CancellationToken.None);
            bool second = await _session.SearchAsync(Query(keyword: "b"), CancellationToken.None);

            Assert.IsFalse(second);
            Assert.IsTrue(_session.IsBusy);
            Assert.AreEqual(1, _source.SearchCalls.Count);

            gate.SetResult(FakeSource.MakePage(1, 3));
            Assert.IsTrue(await first);
            Assert.IsFalse(_session.IsBusy);
        }

        [Test]
        public async Task T_DVC03_08_Logs_NeverContainCaptchaTaxCodeFileCodeOrContent()
        {
            await _session.SearchAsync(Query(), CancellationToken.None);
            _source.Throw = new InvalidOperationException("lỗi kèm " + Captcha + " " + TaxCode + " " + FileCode + " Nội dung dòng mẫu");
            await _session.SearchAsync(Query(), CancellationToken.None);
            _source.Throw = new HoSoParseException("HTML " + FileCode);
            await _session.SearchAsync(Query(), CancellationToken.None);

            Assert.IsNotEmpty(_logger.Lines);
            foreach (string line in _logger.Lines)
            {
                StringAssert.DoesNotContain(Captcha, line);
                StringAssert.DoesNotContain(TaxCode, line);
                StringAssert.DoesNotContain(FileCode, line);
                StringAssert.DoesNotContain("Nội dung dòng mẫu", line);
                StringAssert.DoesNotContain("Cell-", line);
            }
        }

        [Test]
        public async Task PageChange_UsesAppliedFilters_NotEditedOnes()
        {
            var filters = Query(keyword: "đã áp dụng");
            await _session.SearchAsync(filters, CancellationToken.None);

            filters.ProcedureName = "sửa dở";
            await _session.GoToPageAsync(2, "ma222", CancellationToken.None);

            Assert.AreEqual("đã áp dụng", _source.SearchCalls[1].ProcedureName);
            Assert.AreEqual(2, _source.SearchCalls[1].PageNumber);
        }

        [Test]
        public async Task SelectSource_ClearsResults_AndLoadsCaptchaForNewSource()
        {
            await _session.SearchAsync(Query(), CancellationToken.None);

            await _session.SelectSourceAsync(HoSoSource.ThueDienTu, CancellationToken.None);

            Assert.IsNull(_session.Page);
            Assert.AreEqual(HoSoSource.ThueDienTu, _source.LastCaptchaSource);
            Assert.IsFalse(await _session.GoToPageAsync(2, "x", CancellationToken.None), "Bộ lọc tab cũ không còn");
        }

        [Test]
        public async Task ParseError_BecomesPortalChanged()
        {
            _source.Throw = new HoSoParseException("sai mẫu");

            await _session.SearchAsync(Query(), CancellationToken.None);

            Assert.AreEqual(HoSoFailure.PortalChanged, _session.LastFailure);
        }

        private sealed class FakeSource : IHoSoSource
        {
            public readonly List<HoSoQuery> SearchCalls = new List<HoSoQuery>();
            public int CaptchaCalls;
            public HoSoSource LastCaptchaSource;
            public Exception Throw;
            public bool Empty;
            public TaskCompletionSource<HoSoPage> SearchGate;

            public static HoSoPage MakePage(int page, int pageCount)
            {
                return new HoSoPage(new[] { "Cột A", "Cột B" }, new[] { new HoSoRow(new[] { "Cell-1", "Cell-2" }) }, 25, page, pageCount, null);
            }

            public Task<byte[]> GetCaptchaAsync(HoSoSource source, CancellationToken cancellationToken)
            {
                CaptchaCalls++;
                LastCaptchaSource = source;
                return Task.FromResult(new byte[] { 1, 2, 3 });
            }

            public Task<HoSoPage> SearchAsync(HoSoQuery query, CancellationToken cancellationToken)
            {
                SearchCalls.Add(query);
                if (SearchGate != null) return SearchGate.Task;
                if (Throw != null)
                {
                    Exception ex = Throw;
                    throw ex;
                }
                if (Empty) return Task.FromResult(new HoSoPage(null, null, 0, 1, 0, "Không có dữ liệu"));
                return Task.FromResult(MakePage(query.PageNumber, 3));
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
