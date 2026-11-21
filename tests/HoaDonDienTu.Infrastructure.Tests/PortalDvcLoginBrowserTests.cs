using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.PublicService;
using HoaDonDienTu.Domain.PublicService;
using HoaDonDienTu.Infrastructure.PublicService;
using Newtonsoft.Json;
using NUnit.Framework;

namespace HoaDonDienTu.Infrastructure.Tests
{
    /// <summary>Đăng nhập DVC thật (DD-DVC-01 TD01-05) với runner giả: không chạm cổng thật; mật khẩu/CAPTCHA là chuỗi "mẫu".</summary>
    [TestFixture]
    [Category("Mock")]
    public class PortalDvcLoginBrowserTests
    {
        private const string Password = "MauMatKhau!\"'</script>é";
        private const string Captcha = "kY7Q2";
        private const string User = "0109999999-001";

        private FakeRunner _runner;
        private PortalDvcLoginBrowser _browser;

        [SetUp]
        public void SetUp()
        {
            _runner = new FakeRunner();
            _browser = new PortalDvcLoginBrowser(_runner);
        }

        private static string Json(object o)
        {
            return JsonConvert.SerializeObject(o);
        }

        [Test]
        public async Task Captcha_DecodesImageBytes_FromLoginPage()
        {
            _runner.Reply = Json(new { http = 200, ct = "image/png", b64 = Convert.ToBase64String(new byte[] { 1, 2, 3, 4 }) });

            byte[] bytes = await _browser.GetCaptchaAsync(CancellationToken.None);

            CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, bytes);
            Assert.AreEqual(PortalDvcLoginBrowser.LoginPath, _runner.Paths[0]);
            StringAssert.Contains("/tthc/login/getCaptcha", _runner.Scripts[0]);
        }

        [TestCase(500, "image/png", "AQID")]
        [TestCase(200, "text/html", "AQID")]
        [TestCase(200, "image/png", "")]
        public void Captcha_NotAnImage_IsAnError(int http, string ct, string b64)
        {
            _runner.Reply = Json(new { http, ct, b64 });

            Assert.ThrowsAsync<InvalidOperationException>(() => _browser.GetCaptchaAsync(CancellationToken.None));
        }

        [TestCase("200")]
        [TestCase("201")]
        public async Task Submit_Success_ReturnsOrganizationName(string status)
        {
            _runner.Reply = Json(new { http = 200, status, desc = (string)null, org = "CÔNG TY MẪU" });

            DvcLoginResult result = await _browser.SubmitAsync(new DvcCredentials(User, Password), Captcha, CancellationToken.None);

            Assert.IsTrue(result.Success);
            Assert.AreEqual("CÔNG TY MẪU", result.OrganizationName);
        }

        [Test]
        public async Task Submit_WrongCaptcha_Status999_IsClassifiedByDesc()
        {
            _runner.Reply = Json(new { http = 200, status = "999", desc = "Mã captcha không đúng, vui lòng thử lại.", org = "" });

            DvcLoginResult result = await _browser.SubmitAsync(new DvcCredentials(User, Password), Captcha, CancellationToken.None);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(DvcLoginFailure.WrongCaptcha, result.Failure);
        }

        [TestCase("Tên đăng nhập hoặc mật khẩu không đúng", DvcLoginFailure.WrongPassword)]
        [TestCase("Tài khoản đã bị khóa", DvcLoginFailure.Locked)]
        [TestCase("Tài khoản hiện đang được sử dụng trên một tab khác", DvcLoginFailure.AnotherTab)]
        [TestCase("Lỗi lạ chưa gặp", DvcLoginFailure.Unknown)]
        [TestCase("", DvcLoginFailure.Unknown)]
        public void Classify_ByPortalMessage(string desc, DvcLoginFailure expected)
        {
            Assert.AreEqual(expected, PortalDvcLoginBrowser.Classify(desc));
        }

        [Test]
        public async Task Submit_UnknownError_KeepsPortalMessage_ForDisplay()
        {
            _runner.Reply = Json(new { http = 200, status = "999", desc = "Lỗi lạ chưa gặp\u0001", org = "" });

            DvcLoginResult result = await _browser.SubmitAsync(new DvcCredentials(User, Password), Captcha, CancellationToken.None);

            Assert.AreEqual(DvcLoginFailure.Unknown, result.Failure);
            Assert.AreEqual("Lỗi lạ chưa gặp", result.PortalMessage);
            StringAssert.Contains("Cổng báo: Lỗi lạ chưa gặp", DvcLoginMessages.For(result));
        }

        [Test]
        public void Submit_HttpError_Throws_AndGarbageReply_Throws()
        {
            _runner.Reply = Json(new { http = 500, status = (string)null, desc = (string)null, org = "" });
            Assert.CatchAsync<InvalidOperationException>(() => _browser.SubmitAsync(new DvcCredentials(User, Password), Captcha, CancellationToken.None));

            _runner.Reply = "không phải JSON";
            Assert.CatchAsync<InvalidOperationException>(() => _browser.SubmitAsync(new DvcCredentials(User, Password), Captcha, CancellationToken.None));
        }

        [Test]
        public async Task Submit_Script_CarriesFormFields_Base64PasswordInPage_AndLiteralEscaping()
        {
            _runner.Reply = Json(new { http = 200, status = "200", desc = (string)null, org = "x" });

            await _browser.SubmitAsync(new DvcCredentials(User, Password), Captcha, CancellationToken.None);

            string script = _runner.Scripts[0];
            Assert.AreEqual(PortalDvcLoginBrowser.LoginPath, _runner.Paths[0]);
            StringAssert.Contains("/tthc/loginLDAP", script);
            StringAssert.Contains("application/x-www-form-urlencoded", script);
            StringAssert.Contains("b.set('tenDN',\"" + User + "\")", script);
            StringAssert.Contains("b.set('doiTuong',\"DN\")", script);
            StringAssert.Contains("b.set('captcha',\"" + Captcha + "\")", script);
            // Mật khẩu được mã hóa base64 UTF-8 NGAY TRONG TRANG như trang cổng, và đi vào script dạng literal JSON đã thoát.
            StringAssert.Contains("btoa(unescape(encodeURIComponent(", script);
            StringAssert.DoesNotContain("</script", script);
            StringAssert.DoesNotContain("\n", script);
            StringAssert.DoesNotContain("\r", script);
        }

        [Test]
        public async Task Submit_Script_UsesOnlyWhitelistedUrls_AndNoStorageOrCookieAccess()
        {
            _runner.Reply = Json(new { http = 200, status = "200", desc = (string)null, org = "x" });
            await _browser.SubmitAsync(new DvcCredentials(User, Password), Captcha, CancellationToken.None);
            _runner.Reply = Json(new { http = 200, ct = "image/png", b64 = "AQID" });
            await _browser.GetCaptchaAsync(CancellationToken.None);
            _runner.Reply = Json(new { http = 200 });
            await _browser.LogoutAsync(CancellationToken.None);

            foreach (string script in _runner.Scripts)
            {
                foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(script, @"fetch\((""[^""]*"")"))
                {
                    string url = JsonConvert.DeserializeObject<string>(m.Groups[1].Value);
                    Assert.IsTrue(
                        url == "/tthc/loginLDAP" || url == "/tthc/home" || url == "/tthc/logout" || url == "/tthc/login/getCaptcha",
                        "Địa chỉ ngoài danh sách trắng: " + url);
                }
                StringAssert.DoesNotContain("document.cookie", script);
                StringAssert.DoesNotContain("localStorage", script);
                StringAssert.DoesNotContain("eval(", script);
            }
        }

        [Test]
        public void Submit_HttpError_CarriesTheStatusCode()
        {
            _runner.Reply = Json(new { http = 403, status = (string)null, desc = (string)null, org = "" });

            var ex = Assert.ThrowsAsync<DvcPortalHttpException>(() => _browser.SubmitAsync(new DvcCredentials(User, Password), Captcha, CancellationToken.None));

            Assert.AreEqual(403, ex.HttpStatus);
            StringAssert.DoesNotContain(Password, ex.Message);
            StringAssert.DoesNotContain(Captcha, ex.Message);
        }

        [Test]
        public async Task Submit_Script_SendsCsrfHeaderFromPageMeta_LikeTheRestOfThePortal()
        {
            _runner.Reply = Json(new { http = 200, status = "200", desc = (string)null, org = "x" });

            await _browser.SubmitAsync(new DvcCredentials(User, Password), Captcha, CancellationToken.None);

            StringAssert.Contains("csrf-header", _runner.Scripts[0]);
            StringAssert.Contains("csrf-token", _runner.Scripts[0]);
        }

        [Test]
        public async Task Logout_RunsOnHomePage_WithCsrfHeaderFromMeta()
        {
            _runner.Reply = Json(new { http = 200 });

            await _browser.LogoutAsync(CancellationToken.None);

            Assert.AreEqual(PortalDvcLoginBrowser.HomePath, _runner.Paths[0]);
            StringAssert.Contains("/tthc/logout", _runner.Scripts[0]);
            StringAssert.Contains("csrf-header", _runner.Scripts[0]);
            StringAssert.Contains("csrf-token", _runner.Scripts[0]);
        }

        [Test]
        public async Task Requests_AreSerial()
        {
            _runner.Reply = Json(new { http = 200, ct = "image/png", b64 = "AQID" });
            _runner.DelayMs = 50;

            var tasks = new List<Task>();
            for (int i = 0; i < 4; i++) tasks.Add(_browser.GetCaptchaAsync(CancellationToken.None));
            await Task.WhenAll(tasks);

            Assert.AreEqual(1, _runner.MaxConcurrent);
        }

        [Test]
        public void Sanitize_StripsControlChars_AndCapsLength()
        {
            Assert.AreEqual("abc", PortalDvcLoginBrowser.Sanitize("a\u0001b\nc"));
            Assert.AreEqual(200, PortalDvcLoginBrowser.Sanitize(new string('x', 500)).Length);
            Assert.AreEqual(string.Empty, PortalDvcLoginBrowser.Sanitize(null));
        }

        private sealed class FakeRunner : IDvcPageRunner
        {
            public readonly List<string> Scripts = new List<string>();
            public readonly List<string> Paths = new List<string>();
            public string Reply = string.Empty;
            public int DelayMs;
            public int MaxConcurrent;
            private int _current;

            public async Task<string> RunOnPageAsync(string pagePath, string script, CancellationToken cancellationToken)
            {
                int now = Interlocked.Increment(ref _current);
                lock (Scripts) { if (now > MaxConcurrent) MaxConcurrent = now; Paths.Add(pagePath); Scripts.Add(script); }
                try
                {
                    if (DelayMs > 0) await Task.Delay(DelayMs, cancellationToken);
                    return Reply;
                }
                finally
                {
                    Interlocked.Decrement(ref _current);
                }
            }
        }
    }
}
