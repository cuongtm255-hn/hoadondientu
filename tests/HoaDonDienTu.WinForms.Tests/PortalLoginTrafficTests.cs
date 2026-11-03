#if DEBUG
using System;
using System.Collections.Generic;
using System.Text;
using HoaDonDienTu.Application.Authentication;
using HoaDonDienTu.Infrastructure.Development.Mock;
using HoaDonDienTu.WinForms.Login;
using NUnit.Framework;

namespace HoaDonDienTu.WinForms.Tests
{
    /// <summary>Quy tắc của trình duyệt đăng nhập thật (ADR 0007), kiểm trên token và phản hồi GIẢ (MOCK); không khởi tạo Chromium, không gọi cổng Thuế.</summary>
    [TestFixture]
    [Category("MockData")]
    public class PortalLoginTrafficTests
    {
        private const string Base = "https://hoadondientu.gdt.gov.vn";
        private const string AuthPath = "/api/security-taxpayer/authenticate";

        private static PortalLoginTraffic Create()
        {
            return new PortalLoginTraffic(Base, AuthPath);
        }

        [TestCase("http://hoadondientu.gdt.gov.vn")]
        [TestCase("hoadondientu.gdt.gov.vn")]
        [TestCase("")]
        public void Constructor_RejectsABaseUrlThatIsNotHttps(string baseUrl)
        {
            Assert.Throws<ArgumentException>(() => new PortalLoginTraffic(baseUrl, AuthPath));
        }

        [TestCase("")]
        [TestCase("api/x")]
        public void Constructor_RejectsAnAuthenticatePathWithoutASlash(string path)
        {
            Assert.Throws<ArgumentException>(() => new PortalLoginTraffic(Base, path));
        }

        [Test]
        public void LoginUrl_IsThePortalRoot()
        {
            Assert.That(Create().LoginUrl, Is.EqualTo("https://hoadondientu.gdt.gov.vn/"));
            Assert.That(Create().Origin, Is.EqualTo("https://hoadondientu.gdt.gov.vn"));
        }

        // ---- Điều hướng ----

        [TestCase("https://hoadondientu.gdt.gov.vn/", true)]
        [TestCase("https://hoadondientu.gdt.gov.vn/api/x?a=1", true)]
        [TestCase("HTTPS://HOADONDIENTU.GDT.GOV.VN/", true)]
        [TestCase("https://static.hoadondientu.gdt.gov.vn/app.js", true)]
        [TestCase("about:blank", true)]
        [TestCase("http://hoadondientu.gdt.gov.vn/", false)]
        [TestCase("https://hoadondientu.gdt.gov.vn.evil.example/", false)]
        [TestCase("https://evilhoadondientu.gdt.gov.vn/", false)]
        [TestCase("https://example.com/hoadondientu.gdt.gov.vn", false)]
        [TestCase("https://gdt.gov.vn/", false)]
        [TestCase("javascript:alert(1)", false)]
        [TestCase("file:///C:/Windows/win.ini", false)]
        [TestCase("data:text/html,x", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void Navigation_IsLimitedToThePortalHostOverHttps(string url, bool allowed)
        {
            Assert.That(Create().IsNavigationAllowed(url), Is.EqualTo(allowed), url);
        }

        // ---- Nhận ra lời gọi đăng nhập ----

        [TestCase("POST", "https://hoadondientu.gdt.gov.vn/api/security-taxpayer/authenticate", true)]
        [TestCase("post", "https://hoadondientu.gdt.gov.vn/api/security-taxpayer/authenticate?x=1", true)]
        [TestCase("POST", "https://hoadondientu.gdt.gov.vn/API/security-taxpayer/Authenticate", true)]
        [TestCase("GET", "https://hoadondientu.gdt.gov.vn/api/security-taxpayer/authenticate", false)]
        [TestCase("POST", "https://hoadondientu.gdt.gov.vn/api/security-taxpayer/profile", false)]
        [TestCase("POST", "https://other.example/api/security-taxpayer/authenticate", false)]
        [TestCase("POST", "not a url", false)]
        public void OnlyThePostToTheAuthenticatePath_IsTheLoginCall(string method, string url, bool expected)
        {
            Assert.That(Create().IsAuthenticateRequest(method, url), Is.EqualTo(expected));
        }

        // ---- Phản hồi đăng nhập ----

        private static void Feed(PortalLoginTraffic.ResponseBuffer buffer, string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            // Chia thành nhiều mảnh như Chromium giao dữ liệu.
            for (var i = 0; i < bytes.Length; i += 7) buffer.Append(bytes, i, Math.Min(7, bytes.Length - i));
        }

        [Test]
        public void Response_WithAToken_BuildsTheSessionMaterial_UsingTheSubjectAsUsername()
        {
            var token = MockInvoiceDataset.TokenFor("0109213133");
            var buffer = new PortalLoginTraffic.ResponseBuffer();
            Feed(buffer, "{\"token\":\"" + token + "\"}");
            Assert.That(buffer.HasContent, Is.True);

            PortalAuthCapture capture;
            var ok = buffer.TryBuild(new Dictionary<string, string> { { "jwt", token } }, "Mozilla/5.0 (Mock)", out capture);

            Assert.That(ok, Is.True);
            Assert.That(capture.Token, Is.EqualTo(token));
            Assert.That(capture.Username, Is.EqualTo("0109213133"), "tên đăng nhập lấy từ claim sub, không đọc thân request");
            Assert.That(capture.UserAgent, Is.EqualTo("Mozilla/5.0 (Mock)"));
            Assert.That(capture.Cookies.ContainsKey("jwt"), Is.True);
            Assert.That(capture.ToString(), Does.Not.Contain(token), "ToString che bí mật");
        }

        [Test]
        public void Response_IsClearedAfterItWasRead()
        {
            var buffer = new PortalLoginTraffic.ResponseBuffer();
            Feed(buffer, "{\"token\":\"" + MockInvoiceDataset.TokenFor("0109213133") + "\"}");
            PortalAuthCapture capture;
            buffer.TryBuild(null, "ua", out capture);

            Assert.That(buffer.HasContent, Is.False);
            Assert.That(buffer.TryBuild(null, "ua", out capture), Is.False);
        }

        [TestCase("")]
        [TestCase("{}")]
        [TestCase("{\"message\":\"Sai mã captcha\"}")]
        [TestCase("<html>403</html>")]
        public void Response_WithoutAToken_IsNotALogin(string body)
        {
            var buffer = new PortalLoginTraffic.ResponseBuffer();
            Feed(buffer, body);

            PortalAuthCapture capture;
            Assert.That(buffer.TryBuild(null, "ua", out capture), Is.False);
            Assert.That(capture, Is.Null);
        }

        [Test]
        public void Response_LargerThanTheLimit_IsDiscarded_NotBuffered()
        {
            var buffer = new PortalLoginTraffic.ResponseBuffer();
            var chunk = new byte[16 * 1024];
            for (var i = 0; i < 5; i++) buffer.Append(chunk, 0, chunk.Length);

            Assert.That(buffer.HasContent, Is.False);
            PortalAuthCapture capture;
            Assert.That(buffer.TryBuild(null, "ua", out capture), Is.False);
        }

        // ---- Script mở sẵn form đăng nhập ----

        [Test]
        public void PageScript_OnlyDrivesTheUi_NeverReadsInputsOrTouchesTheNetwork()
        {
            var script = PortalLoginPageScript.Source;

            foreach (var forbidden in new[] { "password", "cvalue", ".value", "fetch(", "XMLHttpRequest", "sendBeacon", "localStorage", "sessionStorage", "document.cookie", "WebSocket", "eval(" })
            {
                Assert.That(script, Does.Not.Contain(forbidden), forbidden);
            }
        }

        [Test]
        public void PageScript_ClosesTheNoticeOpensTheLoginFormAndFocusesTheUsernameField()
        {
            var script = PortalLoginPageScript.Source;

            Assert.That(script, Does.Contain(".ant-modal-close"));
            Assert.That(script, Does.Contain(".home-header-menu-item span"));
            Assert.That(script, Does.Contain("Đăng nhập"), "nhãn \"Đăng nhập\" viết bằng escape Unicode để không phụ thuộc mã hóa file");
            Assert.That(script, Does.Contain("getElementById('username')"));
            Assert.That(script, Does.Contain("__hddtLoginAssist"), "chạy một lần cho mỗi trang");
        }

        [Test]
        public void Clear_DropsWhatWasCollected()
        {
            var buffer = new PortalLoginTraffic.ResponseBuffer();
            Feed(buffer, "{\"token\":\"abc\"}");

            buffer.Clear();

            Assert.That(buffer.HasContent, Is.False);
        }
    }
}
#endif
