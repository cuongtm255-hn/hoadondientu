#if DEBUG
using System;
using System.IO;
using System.Linq;
using HoaDonDienTu.Infrastructure.Portal.Config;
using NUnit.Framework;

namespace HoaDonDienTu.Infrastructure.Tests.Portal
{
    [TestFixture]
    public class PortalApiConfigTests
    {
        [Test]
        public void Default_IsValid_ButMarkedUnverified()
        {
            var config = PortalApiConfigDefaults.Create();

            Assert.That(PortalApiConfigValidator.Validate(config), Is.Empty);
            Assert.That(config.Verified, Is.False, "Cấu hình nháp chưa đối chiếu cổng thật");
            Assert.That(config.BaseUrl, Does.StartWith("https://"));
        }

        [Test]
        public void AllVariants_AreValid()
        {
            foreach (var name in Variants.All) Assert.DoesNotThrow(() => Variants.Create(name), name);
        }

        [Test]
        public void JsonRoundTrip_PreservesEverything_IncludingDictionaryKeyCase()
        {
            var original = PortalApiConfigDefaults.Create();

            var json = PortalApiConfigLoader.ToJson(original);
            var parsed = PortalApiConfigLoader.Parse(json);

            Assert.That(PortalApiConfigLoader.ToJson(parsed), Is.EqualTo(json));
            Assert.That(json, Does.Contain("\"PortalInvoiceId\""), "Khóa dictionary (tên trường DTO) giữ nguyên hoa/thường");
            Assert.That(json, Does.Contain("\"baseUrl\""), "Thuộc tính dùng camelCase");
            Assert.That(parsed.Search.Response.Fields["IssueDate"], Is.EqualTo("issueDate"));
        }

        [Test]
        public void LoadOrDefault_MissingFile_ReturnsDefault()
        {
            var config = PortalApiConfigLoader.LoadOrDefault(Path.Combine(Path.GetTempPath(), "does-not-exist-" + Guid.NewGuid() + ".json"));

            Assert.That(config.Verified, Is.False);
            Assert.That(config.Search.Pagination.Mode, Is.EqualTo("Cursor"));
        }

        [Test]
        public void LoadOrDefault_ReadsFile()
        {
            var path = Path.Combine(Path.GetTempPath(), "portal-api-" + Guid.NewGuid() + ".json");
            try
            {
                var config = PortalApiConfigDefaults.Create();
                config.BaseUrl = "https://example.test";
                config.Verified = true;
                File.WriteAllText(path, PortalApiConfigLoader.ToJson(config));

                var loaded = PortalApiConfigLoader.LoadOrDefault(path);

                Assert.That(loaded.BaseUrl, Is.EqualTo("https://example.test"));
                Assert.That(loaded.Verified, Is.True);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public void Parse_InvalidJson_ThrowsConfigException()
        {
            Assert.Throws<PortalApiConfigException>(() => PortalApiConfigLoader.Parse("{ not json"));
        }

        [Test]
        public void Parse_UnknownProperty_IsRejected_SoTyposAreNotSilentlyIgnored()
        {
            var json = PortalApiConfigLoader.ToJson(PortalApiConfigDefaults.Create()).Replace("\"baseUrl\"", "\"baseUrll\"");

            var ex = Assert.Throws<PortalApiConfigException>(() => PortalApiConfigLoader.Parse(json));

            Assert.That(ex.Message, Does.Contain("baseUrll"));
        }

        [Test]
        public void Validator_ReportsEveryProblem()
        {
            var c = PortalApiConfigDefaults.Create();
            c.BaseUrl = "http://insecure.test";
            c.Search.Response.Fields.Remove("InvoiceNumber");
            c.Search.Response.Fields["Bogus"] = "x";
            c.Search.FilterParameters["Nope"] = "x";
            c.Search.Pagination.Mode = "Cursor";
            c.Search.Response.NextCursorPath = null;
            c.Search.Purchase.Method = "DELETE";
            c.Download.Endpoint.Path = "/api/v1/invoices/xml";
            c.Errors.StatusKinds[418] = "Teapot";

            var errors = PortalApiConfigValidator.Validate(c);
            var text = string.Join("\n", errors);

            Assert.That(text, Does.Contain("https"));
            Assert.That(text, Does.Contain("InvoiceNumber"));
            Assert.That(text, Does.Contain("Bogus"));
            Assert.That(text, Does.Contain("Nope"));
            Assert.That(text, Does.Contain("nextCursorPath"));
            Assert.That(text, Does.Contain("GET hoặc POST"));
            Assert.That(text, Does.Contain("{id}"));
            Assert.That(text, Does.Contain("Teapot"));
        }

        [Test]
        public void Validator_BodyLocation_RequiresPost()
        {
            var c = PortalApiConfigDefaults.Create();
            c.Search.ParameterLocation = "Body";

            Assert.That(PortalApiConfigValidator.Validate(c).Any(e => e.Contains("POST")), Is.True);
        }

        [Test]
        public void Validator_JsonBase64_RequiresContentPath()
        {
            var c = PortalApiConfigDefaults.Create();
            c.Download.ResponseMode = "JsonBase64";
            c.Download.ContentPath = null;

            Assert.That(PortalApiConfigValidator.Validate(c).Any(e => e.Contains("contentPath")), Is.True);
        }
    }
}

#endif
