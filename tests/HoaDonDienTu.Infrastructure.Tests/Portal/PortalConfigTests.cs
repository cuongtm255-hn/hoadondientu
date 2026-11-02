using System;
using System.IO;
using System.Linq;
using HoaDonDienTu.Infrastructure.Portal.Config;
using NUnit.Framework;

namespace HoaDonDienTu.Infrastructure.Tests.Portal
{
    [TestFixture]
    public class PortalConfigTests
    {
        [Test]
        public void Default_IsValid_AndPointsAtTheRealPortalPaths()
        {
            var config = PortalApiConfigDefaults.Create();

            Assert.That(PortalApiConfigValidator.Validate(config), Is.Empty);
            Assert.That(config.BaseUrl, Is.EqualTo("https://hoadondientu.gdt.gov.vn"));
            Assert.That(config.Auth.AuthenticatePath, Is.EqualTo("/api/security-taxpayer/authenticate"));
            Assert.That(config.Search.Sold.Path, Is.EqualTo("/api/query/invoices/sold"));
            Assert.That(config.Detail.Endpoint.Path, Is.EqualTo("/api/query/invoices/detail"));
            Assert.That(config.Download.Endpoint.Path, Is.EqualTo("/api/query/invoices/export-xml"));
            Assert.That(config.ExportExcel.Endpoint.Path, Is.EqualTo("/api/query/invoices/export-excel"));
            Assert.That(config.Search.Response.ItemsPath, Is.EqualTo("datas"));
            Assert.That(config.Search.Response.TotalPath, Is.EqualTo("total"));
            Assert.That(config.Search.Response.NextCursorPath, Is.EqualTo("state"));
            Assert.That(config.Search.Pagination.CursorParam, Is.EqualTo("state"));
        }

        [Test]
        public void Default_StaysUnverified_AndNamesWhatIsStillAnAssumption()
        {
            var config = PortalApiConfigDefaults.Create();

            Assert.That(config.Verified, Is.False, "Mua vào và phân trang bằng state chưa có mẫu thật");
            Assert.That(config.Notes, Does.Contain("mua vào").And.Contain("state"));
        }

        [Test]
        public void SoldFilterFields_MatchTheDocumentedParameters()
        {
            var map = PortalApiConfigDefaults.Create().Search.SoldFilterFields;

            Assert.That(map["CounterpartyTaxCode"], Is.EqualTo("nmmst"));
            Assert.That(map["BuyerIdNumber"], Is.EqualTo("nmcmnd"));
            Assert.That(map["StatusCode"], Is.EqualTo("tthai"));
            Assert.That(map["CheckResultCode"], Is.EqualTo("ttxly"));
            Assert.That(map["FormSymbol"], Is.EqualTo("khmshdon"));
            Assert.That(map["InvoiceNumber"], Is.EqualTo("shdon"));
            Assert.That(map["InvoiceSymbol"], Is.EqualTo("khhdon"));
            Assert.That(map["ProxyInvoice"], Is.EqualTo("unhiem"));
        }

        [Test]
        public void PurchaseFilterFields_UseSellerTaxCode_AndBuyerIdNumber()
        {
            var map = PortalApiConfigDefaults.Create().Search.PurchaseFilterFields;

            Assert.That(map["CounterpartyTaxCode"], Is.EqualTo("nbmst"));
            Assert.That(map["BuyerIdNumber"], Is.EqualTo("nmcmnd"));
        }

        [Test]
        public void KeyParameters_AreTheCompositeKey_ForDetailAndExport()
        {
            var config = PortalApiConfigDefaults.Create();
            var expected = new[] { "nbmst", "khhdon", "shdon", "khmshdon" };

            Assert.That(config.Detail.Parameters.Keys, Is.EquivalentTo(expected));
            Assert.That(config.Download.Parameters.Keys, Is.EquivalentTo(expected));
            Assert.That(config.Download.ResponseMode, Is.EqualTo("Zip"));
            Assert.That(config.Download.ZipEntryName, Is.EqualTo("invoice.xml"));
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
            Assert.That(parsed.Search.Response.Fields["IssueDate"], Is.EqualTo("tdlap"));
        }

        [Test]
        public void LoadOrDefault_MissingFile_ReturnsDefault()
        {
            var config = PortalApiConfigLoader.LoadOrDefault(Path.Combine(Path.GetTempPath(), "does-not-exist-" + Guid.NewGuid() + ".json"));

            Assert.That(config.SchemaVersion, Is.EqualTo(PortalApiConfigValidator.CurrentSchemaVersion));
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
        public void Parse_OldSchemaVersion_IsRejectedWithAClearMessage()
        {
            var json = PortalApiConfigLoader.ToJson(PortalApiConfigDefaults.Create()).Replace("\"schemaVersion\": 2", "\"schemaVersion\": 1");

            var ex = Assert.Throws<PortalApiConfigException>(() => PortalApiConfigLoader.Parse(json));

            Assert.That(ex.Message, Does.Contain("schemaVersion"));
        }

        [Test]
        public void SampleFileInDocs_IsTheDefaultConfiguration()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            string path = null;
            while (dir != null && path == null)
            {
                var candidate = Path.Combine(dir.FullName, "docs", "api-samples", "portal-api.config.sample.json");
                if (File.Exists(candidate)) path = candidate;
                dir = dir.Parent;
            }

            if (path == null) Assert.Ignore("Không thấy file mẫu cạnh thư mục test");

            var fromFile = PortalApiConfigLoader.Parse(File.ReadAllText(path));

            Assert.That(PortalApiConfigLoader.ToJson(fromFile), Is.EqualTo(PortalApiConfigLoader.ToJson(PortalApiConfigDefaults.Create())),
                "Cập nhật docs/api-samples/portal-api.config.sample.json khi đổi cấu hình mặc định");
        }

        [Test]
        public void Validator_ReportsEveryProblem()
        {
            var c = PortalApiConfigDefaults.Create();
            c.BaseUrl = "http://insecure.test";
            c.Search.Response.Fields.Remove("InvoiceNumber");
            c.Search.Response.Fields["Bogus"] = "x";
            c.Search.SoldFilterFields["Nope"] = "x";
            c.Search.Response.NextCursorPath = null;
            c.Search.Purchase.Method = "POST";
            c.Search.Rsql.Param = null;
            c.Download.ResponseMode = "Zip";
            c.Download.ZipEntryName = null;
            c.Download.Parameters["x"] = "Nothing";
            c.Auth.AuthenticatePath = "no-slash";
            c.Errors.StatusKinds[418] = "Teapot";

            var text = string.Join("\n", PortalApiConfigValidator.Validate(c));

            Assert.That(text, Does.Contain("https"));
            Assert.That(text, Does.Contain("InvoiceNumber"));
            Assert.That(text, Does.Contain("Bogus"));
            Assert.That(text, Does.Contain("Nope"));
            Assert.That(text, Does.Contain("nextCursorPath"));
            Assert.That(text, Does.Contain("method phải là GET"));
            Assert.That(text, Does.Contain("rsql.param"));
            Assert.That(text, Does.Contain("zipEntryName"));
            Assert.That(text, Does.Contain("Nothing"));
            Assert.That(text, Does.Contain("authenticatePath"));
            Assert.That(text, Does.Contain("Teapot"));
        }

        [Test]
        public void Validator_RequiresTheWholeCompositeKeyInTheResponse()
        {
            foreach (var field in new[] { "SellerTaxCode", "FormSymbol", "InvoiceSymbol", "InvoiceNumber" })
            {
                var c = PortalApiConfigDefaults.Create();
                c.Search.Response.Fields.Remove(field);

                Assert.That(PortalApiConfigValidator.Validate(c).Any(e => e.Contains(field)), Is.True, field);
            }
        }

        [Test]
        public void Validator_SoldAndPurchaseMustNotShareAPath()
        {
            var c = PortalApiConfigDefaults.Create();
            c.Search.Purchase.Path = c.Search.Sold.Path;

            Assert.That(PortalApiConfigValidator.Validate(c).Any(e => e.Contains("trùng")), Is.True);
        }
    }
}
