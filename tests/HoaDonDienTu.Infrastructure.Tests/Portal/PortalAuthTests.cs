using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Application.Authentication;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.Infrastructure.Portal;
using NUnit.Framework;

namespace HoaDonDienTu.Infrastructure.Tests.Portal
{
    /// <summary>
    /// Đăng nhập vẫn qua WebView: trang đăng nhập của cổng gọi <c>POST /api/security-taxpayer/authenticate</c>, ứng dụng ghi lại phản hồi
    /// <c>{"token": "..."}</c> cùng cookie, đọc MST từ JWT và không bao giờ chạm tới mật khẩu hay CAPTCHA.
    /// </summary>
    [TestFixture]
    public class PortalAuthTests
    {
        private static readonly DateTime Now = new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);

        // ---- JWT ----

        [Test]
        public void Token_ParsesSubjectIssuedAndExpiry()
        {
            var jwt = PortalToken.BuildUnsigned("0109213133", Now, Now.AddHours(24));

            PortalToken token;
            Assert.That(PortalToken.TryParse(jwt, out token), Is.True);

            Assert.That(token.Subject, Is.EqualTo("0109213133"));
            Assert.That(token.IssuedAtUtc, Is.EqualTo(Now));
            Assert.That(token.ExpiresAtUtc, Is.EqualTo(Now.AddHours(24)));
            Assert.That(token.IsExpired(Now.AddHours(23)), Is.False);
            Assert.That(token.IsExpired(Now.AddHours(24)), Is.True);
        }

        [Test]
        public void Token_ParsesARealShapedJwt_WithHs512HeaderAndBase64UrlPayload()
        {
            // Cấu trúc đúng như token cổng cấp: {"sub":"<MST>","type":2,"exp":...,"iat":...}, ký HS512.
            var header = Url("{\"alg\":\"HS512\"}");
            var payload = Url("{\"sub\":\"0109213133\",\"type\":2,\"exp\":1793633177,\"iat\":1793546777}");

            PortalToken token;
            Assert.That(PortalToken.TryParse(header + "." + payload + ".c2lnbmF0dXJl", out token), Is.True);

            Assert.That(token.Subject, Is.EqualTo("0109213133"));
            Assert.That(token.ExpiresAtUtc, Is.EqualTo(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(1793633177)));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("no-dots")]
        [TestCase("a.!!!.c")]
        public void Token_Garbage_IsNotParsed(string value)
        {
            PortalToken token;
            Assert.That(PortalToken.TryParse(value, out token), Is.False);
        }

        [Test]
        public void Token_WithoutSubject_IsNotParsed()
        {
            PortalToken token;
            Assert.That(PortalToken.TryParse("x." + Url("{\"exp\":1}") + ".y", out token), Is.False);
        }

        [Test]
        public void Material_ExposesTokenClaims_ButNeverPrintsTheToken()
        {
            var jwt = PortalToken.BuildUnsigned("0109213133", Now, Now.AddHours(1));
            var material = new PortalSessionMaterial(new Dictionary<string, string> { { "jwt", jwt } }, null, jwt, "UA");

            Assert.That(material.Token.Subject, Is.EqualTo("0109213133"));
            Assert.That(material.UserAgent, Is.EqualTo("UA"));
            Assert.That(material.ToString(), Does.Not.Contain(jwt));
            Assert.That(new PortalSessionMaterial(null, null, "opaque").Token, Is.Null);
        }

        // ---- Ghi lại cuộc gọi đăng nhập ----

        [Test]
        public void Capture_TakesTokenFromResponse_AndUsernameFromRequest_WithoutTouchingPassword()
        {
            var jwt = PortalToken.BuildUnsigned("0109213133", Now, Now.AddHours(24));
            const string request = "{\"username\":\"0109213133\",\"password\":\"P@ssw0rd-SECRET\",\"cvalue\":\"ABC123\",\"ckey\":\"0123456789abcdef\"}";
            var response = "{\"token\":\"" + jwt + "\"}";

            PortalAuthCapture capture;
            Assert.That(PortalAuthCapture.TryCreate(request, response, new Dictionary<string, string> { { "jwt", jwt }, { "JSESSIONID", "S" } }, "Mozilla/5.0", out capture), Is.True);

            Assert.That(capture.Username, Is.EqualTo("0109213133"));
            Assert.That(capture.Token, Is.EqualTo(jwt));
            var text = capture.ToString();
            Assert.That(text, Does.Not.Contain("SECRET").And.Not.Contain("P@ssw0rd").And.Not.Contain("ABC123").And.Not.Contain(jwt));
            Assert.That(text, Does.Contain("0109213133"));
        }

        [Test]
        public void Capture_Material_CarriesTokenCookiesAndUserAgentForLaterApiCalls()
        {
            var jwt = PortalToken.BuildUnsigned("0109213133", Now, Now.AddHours(24));
            PortalAuthCapture capture;
            PortalAuthCapture.TryCreate(null, "{\"token\":\"" + jwt + "\"}",
                new Dictionary<string, string> { { "jwt", jwt }, { "JSESSIONID", "S1" }, { "TS0114b13e", "waf" } }, "Mozilla/5.0 X", out capture);

            var material = capture.ToMaterial();

            Assert.That(material.BearerToken, Is.EqualTo(jwt));
            Assert.That(material.Cookies.Keys, Is.EquivalentTo(new[] { "jwt", "JSESSIONID", "TS0114b13e" }));
            Assert.That(material.UserAgent, Is.EqualTo("Mozilla/5.0 X"));
        }

        [Test]
        public void Capture_WithoutRequestBody_FallsBackToTheTokenSubject()
        {
            var jwt = PortalToken.BuildUnsigned("0109213133", Now, Now.AddHours(1));
            PortalAuthCapture capture;

            PortalAuthCapture.TryCreate(null, "{\"token\":\"" + jwt + "\"}", null, null, out capture);

            Assert.That(capture.Username, Is.EqualTo("0109213133"));
            Assert.That(capture.Cookies, Is.Empty);
        }

        [TestCase("{\"message\":\"Sai mã captcha\"}")]
        [TestCase("")]
        [TestCase(null)]
        [TestCase("{\"token\":\"\"}")]
        public void Capture_FailedLogin_HasNoToken_SoNoSessionIsCreated(string response)
        {
            PortalAuthCapture capture;

            Assert.That(PortalAuthCapture.TryCreate("{\"username\":\"0109213133\"}", response, null, null, out capture), Is.False);
            Assert.That(capture, Is.Null);
        }

        // ---- Xác minh MST bằng token ----

        [Test]
        public void Verify_MatchingTaxCode_Succeeds_AndKeepsTheMaterial()
        {
            var material = Material("0109213133", Now.AddHours(1));

            var result = Verify("0109213133", material);

            Assert.That(result.Outcome, Is.EqualTo(PortalLoginOutcome.Success));
            Assert.That(result.Session.VerifiedTaxCode.Value, Is.EqualTo("0109213133"));
            Assert.That(result.Session.Material, Is.SameAs(material));
        }

        [Test]
        public void Verify_OtherTaxCode_IsMismatch_FR_AUTH_011()
        {
            var result = Verify("0109213133", Material("0109999999", Now.AddHours(1)));

            Assert.That(result.Outcome, Is.EqualTo(PortalLoginOutcome.TaxCodeMismatch));
            Assert.That(result.Session, Is.Null);
        }

        [Test]
        public void Verify_ExpiredToken_IsInvalidSession()
        {
            var result = Verify("0109213133", Material("0109213133", Now.AddMinutes(-1)));

            Assert.That(result.Outcome, Is.EqualTo(PortalLoginOutcome.InvalidSession));
        }

        [Test]
        public void Verify_UnreadableToken_IsInvalidSession()
        {
            var result = Verify("0109213133", new PortalSessionMaterial(null, null, "opaque-token"));

            Assert.That(result.Outcome, Is.EqualTo(PortalLoginOutcome.InvalidSession));
        }

        [Test]
        public void Verify_SubjectThatIsNotATaxCode_IsInvalidSession()
        {
            var result = Verify("0109213133", Material("not-a-mst", Now.AddHours(1)));

            Assert.That(result.Outcome, Is.EqualTo(PortalLoginOutcome.InvalidSession));
        }

        [Test]
        public void Verify_NeedsNoNetwork_AndNeverLeaksTheToken()
        {
            var material = Material("0109213133", Now.AddHours(1));

            var result = Verify("0109213133", material);

            Assert.That(result.Message, Is.Null);
            Assert.That(result.Session.ToString(), Does.Not.Contain(material.BearerToken));
        }

        private static PortalSessionMaterial Material(string subject, DateTime expires)
        {
            return new PortalSessionMaterial(null, null, PortalToken.BuildUnsigned(subject, Now.AddHours(-2), expires));
        }

        private static PortalLoginResult Verify(string companyTaxCode, PortalSessionMaterial material)
        {
            var company = new CompanyContext(CompanyId.NewId(), TaxCode.Parse(companyTaxCode), "Công ty", @"C:\data", null);
            return new PortalAuthenticationService(new FixedClock(Now)).VerifyAsync(company, material, CancellationToken.None).GetAwaiter().GetResult();
        }

        private static string Url(string json)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        private sealed class FixedClock : IClock
        {
            public FixedClock(DateTime now)
            {
                UtcNow = now;
            }

            public DateTime UtcNow { get; private set; }
        }
    }
}
