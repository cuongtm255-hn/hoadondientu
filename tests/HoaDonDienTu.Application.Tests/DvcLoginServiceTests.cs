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
    /// <summary>T-DVC01-xx (DD-DVC-01). Trình duyệt giả, không chạm cổng thật; dữ liệu "mẫu", không phải thông tin thật.</summary>
    [TestFixture]
    [Category("Mock")]
    public class DvcLoginServiceTests
    {
        private const string Password = "MauMatKhau!123";
        private const string Captcha = "mAu7K";

        private FakeBrowser _browser;
        private FakeLogger _logger;
        private FakeNames _names;
        private DvcSession _session;
        private DvcLoginService _service;

        [SetUp]
        public void SetUp()
        {
            _browser = new FakeBrowser();
            _logger = new FakeLogger();
            _names = new FakeNames();
            _session = new DvcSession();
            _service = new DvcLoginService(_browser, _session, _logger, _names);
        }

        [Test]
        public async Task T_DVC01_01_Success_SetsSessionAndOrganization()
        {
            _browser.Result = DvcLoginResult.Ok("CÔNG TY MẪU (mô phỏng)");

            var result = await _service.LoginAsync("0100000000", "0100000000-001", Password, Captcha, CancellationToken.None);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(DvcSessionState.LoggedIn, _session.State);
            Assert.AreEqual("CÔNG TY MẪU (mô phỏng)", _session.OrganizationName);
            Assert.AreEqual("0100000000-001", _session.UserName);
        }

        [TestCase(DvcLoginFailure.WrongCaptcha)]
        [TestCase(DvcLoginFailure.WrongPassword)]
        [TestCase(DvcLoginFailure.Locked)]
        public async Task T_DVC01_02_Failure_KeepsSessionLoggedOut_AndRemembersNothing(DvcLoginFailure failure)
        {
            _browser.Result = DvcLoginResult.Fail(failure);

            var result = await _service.LoginAsync("0100000000", "u", Password, Captcha, CancellationToken.None);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(failure, result.Failure);
            Assert.AreEqual(DvcSessionState.NotLoggedIn, _session.State);
            Assert.AreEqual(0, _names.Saved.Count);
        }

        [TestCase("", "pw", "cap")]
        [TestCase("user", "", "cap")]
        [TestCase("user", "pw", "")]
        [TestCase("  ", "pw", "cap")]
        public async Task MissingInput_IsRejected_WithoutCallingBrowser(string user, string password, string captcha)
        {
            var result = await _service.LoginAsync("0100000000", user, password, captcha, CancellationToken.None);

            Assert.AreEqual(DvcLoginFailure.MissingInput, result.Failure);
            Assert.AreEqual(0, _browser.SubmitCalls);
        }

        [Test]
        public async Task T_DVC01_03_Logs_NeverContainPasswordOrCaptcha_InAnyScenario()
        {
            _browser.Result = DvcLoginResult.Ok("Tổ chức mẫu");
            await _service.LoginAsync("0100000000", "u1", Password, Captcha, CancellationToken.None);

            _browser.Result = DvcLoginResult.Fail(DvcLoginFailure.WrongPassword);
            await _service.LoginAsync("0100000000", "u1", Password, Captcha, CancellationToken.None);

            // Ngoại lệ mà thông điệp chứa dữ liệu nhập: nhật ký chỉ được ghi loại lỗi.
            _browser.Throw = new InvalidOperationException("lỗi kèm " + Password + " và " + Captcha);
            await _service.LoginAsync("0100000000", "u1", Password, Captcha, CancellationToken.None);

            Assert.IsNotEmpty(_logger.Lines);
            foreach (string line in _logger.Lines)
            {
                StringAssert.DoesNotContain(Password, line);
                StringAssert.DoesNotContain(Captcha, line);
            }
        }

        [Test]
        public void Credentials_ToString_DoesNotExposeValues()
        {
            string text = new DvcCredentials("u1", Password).ToString();

            StringAssert.DoesNotContain(Password, text);
            StringAssert.DoesNotContain("u1", text);
        }

        [Test]
        public async Task BrowserException_BecomesNetworkFailure_NotThrown()
        {
            _browser.Throw = new InvalidOperationException("boom");

            var result = await _service.LoginAsync("0100000000", "u1", Password, Captcha, CancellationToken.None);

            Assert.AreEqual(DvcLoginFailure.Network, result.Failure);
            Assert.AreEqual(DvcSessionState.NotLoggedIn, _session.State);
        }

        [Test]
        public void Cancellation_Propagates()
        {
            _browser.Throw = new OperationCanceledException();

            Assert.ThrowsAsync<OperationCanceledException>(
                () => _service.LoginAsync("0100000000", "u1", Password, Captcha, CancellationToken.None));
        }

        [Test]
        public void T_DVC01_04_UserName_DefaultsToTaxCode_ThenRemembered()
        {
            Assert.AreEqual("0100000000", _service.SuggestUserName("0100000000"));

            _names.Set("0100000000", "0100000000-001");

            Assert.AreEqual("0100000000-001", _service.SuggestUserName("0100000000"));
        }

        [Test]
        public async Task T_DVC01_04_Success_RemembersUserName_ButStoreHasNoPasswordApi()
        {
            _browser.Result = DvcLoginResult.Ok("Tổ chức mẫu");

            await _service.LoginAsync("0100000000", "0100000000-001", Password, Captcha, CancellationToken.None);

            Assert.AreEqual("0100000000-001", _names.Get("0100000000"));
            foreach (var value in _names.Saved.Values) StringAssert.DoesNotContain(Password, value);
        }

        [Test]
        public async Task T_DVC01_05_AnotherTab_MarksSession_AndDoesNotRetry()
        {
            _browser.Result = DvcLoginResult.Fail(DvcLoginFailure.AnotherTab);

            await _service.LoginAsync("0100000000", "u1", Password, Captcha, CancellationToken.None);

            Assert.AreEqual(DvcSessionState.TakenByOtherTab, _session.State);
            Assert.AreEqual(1, _browser.SubmitCalls, "Không tự đăng nhập lại");
        }

        [Test]
        public void T_DVC01_05_Expired_RaisesStateChanged_OnlyOnChange()
        {
            _session.SignedIn("u1", "Tổ chức mẫu");
            int changes = 0;
            _session.StateChanged += (s, e) => changes++;

            _session.MarkExpired();
            _session.MarkExpired();

            Assert.AreEqual(DvcSessionState.Expired, _session.State);
            Assert.AreEqual(1, changes);
        }

        [Test]
        public async Task T_DVC01_06_Logout_ResetsSession_EvenWhenBrowserFails()
        {
            _session.SignedIn("u1", "Tổ chức mẫu");
            _browser.LogoutThrows = true;

            await _service.LogoutAsync(CancellationToken.None);

            Assert.AreEqual(DvcSessionState.NotLoggedIn, _session.State);
            Assert.IsNull(_session.OrganizationName);
        }

        [Test]
        public async Task RefreshCaptcha_ReturnsBrowserImage()
        {
            _browser.Image = new byte[] { 1, 2, 3 };

            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, await _service.RefreshCaptchaAsync(CancellationToken.None));
        }

        private sealed class FakeBrowser : IDvcLoginBrowser
        {
            public DvcLoginResult Result = DvcLoginResult.Ok("Tổ chức mẫu");
            public Exception Throw;
            public bool LogoutThrows;
            public byte[] Image = new byte[0];
            public int SubmitCalls;

            public Task<byte[]> GetCaptchaAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult(Image);
            }

            public Task<DvcLoginResult> SubmitAsync(DvcCredentials credentials, string captcha, CancellationToken cancellationToken)
            {
                SubmitCalls++;
                if (Throw != null) throw Throw;
                return Task.FromResult(Result);
            }

            public Task LogoutAsync(CancellationToken cancellationToken)
            {
                if (LogoutThrows) throw new InvalidOperationException("boom");
                return Task.FromResult(0);
            }
        }

        private sealed class FakeLogger : IApplicationLogger
        {
            public readonly List<string> Lines = new List<string>();
            public void Info(string message) { Lines.Add(message); }
            public void Warning(string message) { Lines.Add(message); }
            public void Error(string message, Exception exception) { Lines.Add(message); }
        }

        private sealed class FakeNames : IDvcUserNameStore
        {
            public readonly Dictionary<string, string> Saved = new Dictionary<string, string>();
            public string Get(string taxCode) { string v; return Saved.TryGetValue(taxCode, out v) ? v : null; }
            public void Set(string taxCode, string userName) { Saved[taxCode] = userName; }
        }
    }
}
