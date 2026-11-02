using System;
using System.IO;
using HoaDonDienTu.Domain.Invoices;
using HoaDonDienTu.Infrastructure.Portal.Config;
using NUnit.Framework;

namespace HoaDonDienTu.Infrastructure.Tests.Portal
{
    [TestFixture]
    public class CapabilitiesAndProbeTests
    {
        [Test]
        public void Capabilities_FollowConfiguredFilterFields_PerDirection()
        {
            var config = PortalApiConfigDefaults.Create();
            config.Search.SoldFilterFields.Remove("ProxyInvoice");
            config.Search.SoldFilterFields["FormSymbol"] = " ";
            var caps = new PortalApiConfigCapabilities(config);

            Assert.That(caps.SupportsFilter(InvoiceDirection.Sold, "CounterpartyTaxCode"), Is.True);
            Assert.That(caps.SupportsFilter(InvoiceDirection.Sold, "InvoiceNumber"), Is.True);
            Assert.That(caps.SupportsFilter(InvoiceDirection.Sold, "ProxyInvoice"), Is.False, "Không có trong cấu hình");
            Assert.That(caps.SupportsFilter(InvoiceDirection.Sold, "FormSymbol"), Is.False, "Tên trường rỗng nghĩa là không hỗ trợ");
            Assert.That(caps.SupportsFilter(InvoiceDirection.Sold, "Unknown"), Is.False);
            Assert.That(caps.SupportsFilter(InvoiceDirection.Purchase, "ProxyInvoice"), Is.True, "Mua vào có bộ trường riêng");
        }

        [Test]
        public void Capabilities_BuyerIdNumber_ForBothDirections()
        {
            var caps = new PortalApiConfigCapabilities(PortalApiConfigDefaults.Create());

            Assert.That(caps.SupportsFilter(InvoiceDirection.Sold, "BuyerIdNumber"), Is.True);
            Assert.That(caps.SupportsFilter(InvoiceDirection.Purchase, "BuyerIdNumber"), Is.True, "Form mua vào cũng có CCCD người mua (mẫu thật 02/11/2026)");
        }

        [Test]
        public void Probe_WritableFolder_Ok_AndLeavesNothingBehind()
        {
            var dir = Path.Combine(Path.GetTempPath(), "hddt-probe-" + Guid.NewGuid().ToString("N"));
            try
            {
                var result = new FileSystemStorageProbe().Check(dir);

                Assert.That(result.Writable, Is.True);
                Assert.That(Directory.GetFiles(dir), Is.Empty);
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        [Test]
        public void Probe_ReadOnlyFile_InPlaceOfFolder_Fails()
        {
            var file = Path.Combine(Path.GetTempPath(), "hddt-probe-file-" + Guid.NewGuid().ToString("N"));
            File.WriteAllText(file, "x");
            try
            {
                var result = new FileSystemStorageProbe().Check(Path.Combine(file, "sub"));

                Assert.That(result.Writable, Is.False);
                Assert.That(result.Reason, Is.Not.Null.And.Not.Empty);
            }
            finally
            {
                File.Delete(file);
            }
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("C:\\data\\bad|name")]
        public void Probe_InvalidPaths_FailWithoutThrowing(string path)
        {
            var result = new FileSystemStorageProbe().Check(path);

            Assert.That(result.Writable, Is.False);
        }
    }
}
