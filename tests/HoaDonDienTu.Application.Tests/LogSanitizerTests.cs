using HoaDonDienTu.Application.Common;
using NUnit.Framework;

namespace HoaDonDienTu.Application.Tests
{
    [TestFixture]
    public class LogSanitizerTests
    {
        [TestCase("Authorization: Bearer abc123.def456", "abc123")]
        [TestCase("authorization=Basic dXNlcjpwYXNz", "dXNlcjpwYXNz")]
        [TestCase("sent header Authorization: Bearer topsecret, retry", "topsecret")]
        [TestCase("Cookie: sid=XYZ789; other=1", "XYZ789")]
        [TestCase("Set-Cookie: session=SECRETSESSION; Path=/", "SECRETSESSION")]
        [TestCase("token=SuperSecretToken", "SuperSecretToken")]
        [TestCase("password: hunter2", "hunter2")]
        [TestCase("captcha=8A5K2 submitted", "8A5K2")]
        [TestCase("{\"password\":\"hunter2\",\"user\":\"a\"}", "hunter2")]
        [TestCase("{\"access_token\": \"AAA.BBB\"}", "AAA.BBB")]
        [TestCase("licenseKey=ABCD-EFGH-IJKL-MNOP", "ABCD-EFGH-IJKL-MNOP")]
        [TestCase("jwt eyJhbGciOi.eyJzdWIiOiIx.sigpart here", "eyJhbGciOi")]
        [TestCase("CCCD 012345678901 của người dùng", "012345678901")]
        public void Sanitize_RemovesSecret(string input, string secret)
        {
            var output = LogSanitizer.Sanitize(input);

            Assert.That(output, Does.Not.Contain(secret));
            Assert.That(output, Does.Contain(LogSanitizer.Mask));
        }

        [Test]
        public void Sanitize_Xml_Masked()
        {
            var output = LogSanitizer.Sanitize("Lỗi parse <?xml version=\"1.0\"?><HDon><NBan>Công ty ABC</NBan></HDon>");

            Assert.That(output, Does.Not.Contain("Công ty ABC"));
            Assert.That(output, Does.Contain(LogSanitizer.XmlMask));
            Assert.That(output, Does.StartWith("Lỗi parse"));
        }

        [TestCase("Token chỉ lưu trong bộ nhớ")]
        [TestCase("Đang xử lý tháng 07/2026 trang 4")]
        [TestCase("MST 0101234567 hoàn tất 128 hóa đơn")]
        [TestCase("Mã tham chiếu a1b2c3d4")]
        public void Sanitize_KeepsHarmlessText(string input)
        {
            Assert.That(LogSanitizer.Sanitize(input), Is.EqualTo(input));
        }

        [Test]
        public void Sanitize_NullOrEmpty_Passthrough()
        {
            Assert.That(LogSanitizer.Sanitize(null), Is.Null);
            Assert.That(LogSanitizer.Sanitize(string.Empty), Is.EqualTo(string.Empty));
        }

        [TestCase("0101234567", "010****567")]
        [TestCase("0101234567-001", "010********001")]
        [TestCase("123", "***")]
        public void MaskTaxCode_KeepsEnds(string input, string expected)
        {
            Assert.That(LogSanitizer.MaskTaxCode(input), Is.EqualTo(expected));
        }
    }
}
