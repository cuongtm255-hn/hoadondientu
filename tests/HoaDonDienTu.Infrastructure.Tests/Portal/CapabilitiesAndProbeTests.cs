using System;
using System.IO;
using HoaDonDienTu.Infrastructure.Portal.Config;
using NUnit.Framework;

namespace HoaDonDienTu.Infrastructure.Tests.Portal
{
    [TestFixture]
    public class CapabilitiesAndProbeTests
    {
        [Test]
        public void Capabilities_FollowConfiguredFilterParameters()
        {
            var config = PortalApiConfigDefaults.Create();
            config.Search.FilterParameters.Remove("ProxyInvoice");
            config.Search.FilterParameters["FormSymbol"] = " ";
            var caps = new PortalApiConfigCapabilities(config);

            Assert.That(caps.SupportsFilter("CounterpartyTaxCode"), Is.True);
            Assert.That(caps.SupportsFilter("InvoiceNumber"), Is.True);
            Assert.That(caps.SupportsFilter("ProxyInvoice"), Is.False, "Không có trong cấu hình");
            Assert.That(caps.SupportsFilter("FormSymbol"), Is.False, "Tên tham số rỗng nghĩa là không hỗ trợ");
            Assert.That(caps.SupportsFilter("Unknown"), Is.False);
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
