using HoaDonDienTu.Infrastructure.Portal.Config;
using NUnit.Framework;

namespace HoaDonDienTu.Infrastructure.Tests.Portal
{
    /// <summary>P6-03: baseUrl của portal-api.json chỉ được trỏ tới cổng Thuế, để file cấu hình bị thay không đưa đăng nhập/token sang máy chủ lạ.</summary>
    [TestFixture]
    public class PortalHostPolicyTests
    {
        [TestCase("https://hoadondientu.gdt.gov.vn")]
        [TestCase("https://hoadondientu.gdt.gov.vn/")]
        [TestCase("https://HOADONDIENTU.GDT.GOV.VN")]
        [TestCase("https://gdt.gov.vn")]
        [TestCase("https://api.hoadondientu.gdt.gov.vn")]
        [TestCase("https://hoadondientu.gdt.gov.vn:443")]
        public void TheTaxPortalHosts_AreTrusted(string url)
        {
            Assert.That(PortalHostPolicy.IsTrusted(url), Is.True, url);
        }

        [TestCase("https://evil.example")]
        [TestCase("https://gdt.gov.vn.evil.com")]
        [TestCase("https://evilgdt.gov.vn")]
        [TestCase("https://hoadondientu.gdt.gov.vn.evil.com")]
        [TestCase("https://evil.com/hoadondientu.gdt.gov.vn")]
        [TestCase("https://hoadondientu.gdt.gov.vn@evil.com")]
        [TestCase("https://user:pass@hoadondientu.gdt.gov.vn")]
        [TestCase("https://hoadondientu.gdt.gov.vn:8443")]
        [TestCase("http://hoadondientu.gdt.gov.vn")]
        [TestCase("ftp://hoadondientu.gdt.gov.vn")]
        [TestCase("localhost")]
        [TestCase("not a url")]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase(null)]
        public void AnythingElse_IsNotTrusted(string url)
        {
            Assert.That(PortalHostPolicy.IsTrusted(url), Is.False, url);
        }

        [Test]
        public void TheDefaultConfig_PointsAtATrustedHost()
        {
            Assert.That(PortalHostPolicy.IsTrusted(PortalApiConfigDefaults.Create().BaseUrl), Is.True);
        }
    }
}
