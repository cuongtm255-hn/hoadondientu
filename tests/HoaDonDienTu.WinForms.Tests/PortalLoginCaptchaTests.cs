using System;
using System.Drawing;
using HoaDonDienTu.WinForms.Login;
using NUnit.Framework;

namespace HoaDonDienTu.WinForms.Tests
{
    /// <summary>Ảnh CAPTCHA SVG và các đoạn script đăng nhập. Dữ liệu GIẢ (ảnh tự dựng cùng dạng với ảnh thật của cổng), không gọi cổng Thuế.</summary>
    [TestFixture]
    [Category("MockData")]
    public class PortalLoginCaptchaTests
    {
        private static readonly string LS = ((char)0x2028).ToString();
        private static readonly string PS = ((char)0x2029).ToString();

        // Cùng dạng với phản hồi thật (api-samples mục 0): một đường nhiễu chỉ có nét, các ký tự là path tô màu gồm M/L/Q/C/Z.
        private const string SampleSvg =
            "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"200\" height=\"40\" viewBox=\"0,0,200,40\">"
            + "<path d=\"M20 24 C87 35,108 30,195 30\" stroke=\"#222\" fill=\"none\"/>"
            + "<path fill=\"#222\" d=\"M10 5L50 5L50 35L10 35ZM20 12L20 28L40 28L40 12Z\"/>"
            + "<path fill=\"#333\" d=\"M60 5L100 5Q120 20 100 35L60 35Z\"/>"
            + "</svg>";

        [Test]
        public void Render_DrawsFilledGlyphs_OnAWhiteBackground()
        {
            using (var bitmap = CaptchaSvgRenderer.Render(SampleSvg, 300, 60))
            {
                Assert.That(bitmap.Width, Is.EqualTo(300));
                Assert.That(bitmap.Height, Is.EqualTo(60));
                // Giữa nét dày của ký tự thứ nhất (x 10..20 trên ảnh 200 rộng → 15..30 trên ảnh 300) phải là màu tối, góc ảnh vẫn trắng.
                var ink = bitmap.GetPixel(22, 30);
                Assert.That(ink.R, Is.LessThan(100), "ký tự phải được tô màu tối");
                Assert.That(bitmap.GetPixel(299, 59).ToArgb(), Is.EqualTo(Color.White.ToArgb()));
                // Lỗ rỗng giữa chữ "O" (x 20..40 → 30..60) vẫn trắng: không tô nhầm vùng lõm (nonzero fill + hai chiều vẽ ngược).
                Assert.That(bitmap.GetPixel(45, 30).R, Is.GreaterThan(200), "phần lõm của ký tự không bị tô đặc");
            }
        }

        [Test]
        public void Render_ReadsTheRootSize_ToScale()
        {
            var svg = SampleSvg.Replace("width=\"200\" height=\"40\"", "width=\"400\" height=\"80\"");
            using (var bitmap = CaptchaSvgRenderer.Render(svg, 300, 60))
            {
                // Cùng hình nhưng canvas gấp đôi: vùng x 22 của ảnh 300 nay thuộc phần trắng (ký tự chỉ chiếm nửa trái trước đó).
                Assert.That(bitmap.GetPixel(22, 30).R, Is.GreaterThan(200));
            }
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("not svg at all")]
        [TestCase("<svg width=\"200\" height=\"40\"></svg>")]
        [TestCase("<svg width=\"200\" height=\"40\"><path fill=\"#222\" d=\"M1 1A5 5 0 0 1 9 9Z\"/></svg>")]
        [TestCase("<svg width=\"200\" height=\"40\"><path fill=\"red\" d=\"M1 1L5 5Z\"/></svg>")]
        [TestCase("<svg width=\"200\" height=\"40\"><path fill=\"#222\" d=\"M1\"/></svg>")]
        [TestCase("<svg width=\"0\" height=\"40\"><path fill=\"#222\" d=\"M1 1L5 5Z\"/></svg>")]
        public void Render_RejectsAnythingItCannotDrawFaithfully(string svg)
        {
            Assert.Throws<FormatException>(() => CaptchaSvgRenderer.Render(svg, 300, 60));
        }

        [Test]
        public void Render_RejectsAHugeImage()
        {
            var huge = "<svg width=\"200\" height=\"40\">" + new string('x', CaptchaSvgRenderer.MaxSvgLength) + "</svg>";

            Assert.Throws<FormatException>(() => CaptchaSvgRenderer.Render(huge, 300, 60));
        }

        [Test]
        public void Quote_EscapesEverythingThatCouldBreakOutOfAJavaScriptString()
        {
            var tricky = "a\"b\\c\nd\re</script>"+LS+""+PS+"'\t";

            var quoted = PortalLoginScripts.Quote(tricky);

            Assert.That(quoted, Does.StartWith("\"").And.EndWith("\""));
            Assert.That(quoted, Does.Not.Contain("\n").And.Not.Contain("\r").And.Not.Contain(""+LS+"").And.Not.Contain(""+PS+""));
            Assert.That(quoted, Does.Not.Contain("</script"), "dấu < bị thoát để không đóng thẻ script");
            Assert.That(quoted, Does.Contain("\\\"").And.Contain("\\\\").And.Contain("\\u2028").And.Contain("\\u2029"));
            Assert.That(PortalLoginScripts.Quote(null), Is.EqualTo("null"));
        }

        [Test]
        public void AuthenticateRequest_CarriesTheFourFields_ToTheAuthenticatePath_AndKeepsTheTokenOutOfThePage()
        {
            var script = PortalLoginScripts.AuthenticateRequest("/api/security-taxpayer/authenticate", "rid-1", "0101234567", "mật \"khẩu\"", "AbC", "key-1");

            Assert.That(script, Does.Contain("fetch(\"/api/security-taxpayer/authenticate\""));
            Assert.That(script, Does.Contain("method: 'POST'"));
            Assert.That(script, Does.Contain("username: \"0101234567\"").And.Contain("cvalue: \"AbC\"").And.Contain("ckey: \"key-1\""));
            Assert.That(script, Does.Contain("password: \"mật \\\"khẩu\\\"\""));
            Assert.That(script, Does.Contain("'request-id': \"rid-1\"").And.Contain("'end-point': '/'").And.Contain("'content-type': 'application/json'"));
            Assert.That(script, Does.Contain("return r.text();"), "thân phản hồi được đọc hết để bộ lọc nhận đủ byte");
            Assert.That(script, Does.Not.Contain(".token"), "token không được lưu vào biến nào của trang");
        }

        [Test]
        public void CaptchaRequest_IsAGetWithoutAuthorization()
        {
            var script = PortalLoginScripts.CaptchaRequest("/api/captcha", "rid-2");

            Assert.That(script, Does.Contain("fetch(\"/api/captcha\"").And.Contain("method: 'GET'"));
            Assert.That(script, Does.Not.Contain("authorization").And.Not.Contain("content-type"));
            Assert.That(script, Does.Contain("j.key").And.Contain("j.content"));
        }

        [Test]
        public void Traffic_ExposesTheCaptchaAndAuthenticatePaths()
        {
            var traffic = new PortalLoginTraffic("https://hoadondientu.gdt.gov.vn", "/api/security-taxpayer/authenticate");

            Assert.That(traffic.CaptchaPath, Is.EqualTo("/api/captcha"));
            Assert.That(traffic.AuthenticatePath, Is.EqualTo("/api/security-taxpayer/authenticate"));
            Assert.Throws<ArgumentException>(() => new PortalLoginTraffic("https://hoadondientu.gdt.gov.vn", "/a", "captcha"));
        }
    }
}
