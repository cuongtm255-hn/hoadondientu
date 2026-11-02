#if DEBUG
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using HoaDonDienTu.Application.Authentication;
using HoaDonDienTu.Application.Portal;
using HoaDonDienTu.Domain.Invoices;
using Newtonsoft.Json.Linq;

namespace HoaDonDienTu.Infrastructure.Development.Mock
{
    public sealed class MockInvoiceRecord
    {
        public InvoiceDirection Direction { get; set; }
        public PortalInvoiceDto Dto { get; set; }

        /// <summary>Hóa đơn ủy nhiệm (<c>unhiem</c> = 1).</summary>
        public bool Proxy { get; set; }

        /// <summary>Dòng hàng hóa dịch vụ (<c>hdhhdvu</c>) trả về ở API chi tiết.</summary>
        public IList<MockLine> Lines { get; set; }
    }

    public sealed class MockLine
    {
        public string Name { get; set; }
        public string Unit { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Amount { get; set; }
        public string TaxRate { get; set; }
    }

    /// <summary>
    /// Dữ liệu hóa đơn giả có hình dạng giống phản hồi thật của cổng Thuế (tên trường <c>nbmst, khmshdon, khhdon, shdon, tdlap, tthai, ttxly…</c>
    /// theo docs/api-samples/sample-api-web.md), xác định theo MST + seed, phủ 2025-01…2026-12 cho cả mua vào và bán ra.
    /// Có sẵn các tình huống: tháng nhiều trang (mua vào 2026-07: 120 hóa đơn), tháng rỗng (bán ra 2026-03), tháng một hóa đơn (bán ra 2026-02),
    /// người mua là cá nhân chỉ có CCCD, hóa đơn ủy nhiệm, đủ các trạng thái và kết quả kiểm tra, ngoại tệ.
    /// Toàn bộ là dữ liệu bịa, KHÔNG phải dữ liệu cổng Thuế.
    /// </summary>
    public sealed class MockInvoiceDataset
    {
        private static readonly string[] CounterpartyTaxCodes =
        {
            "0100100001", "0100100002", "0100100003", "0100100004", "0100100005", "0100100006"
        };

        private static readonly string[] CounterpartyNames =
        {
            "Công ty TNHH Alpha", "Công ty CP Beta", "Công ty TNHH Gamma", "Công ty CP Delta", "Hộ kinh doanh Epsilon", "Chi nhánh Zeta"
        };

        private readonly List<MockInvoiceRecord> _records = new List<MockInvoiceRecord>();

        public MockInvoiceDataset(string taxCode, int seed = 1)
        {
            if (string.IsNullOrEmpty(taxCode)) throw new ArgumentException("taxCode");
            TaxCode = taxCode;
            Generate(seed);
        }

        public string TaxCode { get; private set; }

        public IList<MockInvoiceRecord> All
        {
            get { return _records.AsReadOnly(); }
        }

        /// <summary>JWT không chữ ký giống token thật: <c>sub</c> là MST, hết hạn sau 24 giờ.</summary>
        public static string TokenFor(string taxCode)
        {
            var now = DateTime.UtcNow;
            return PortalToken.BuildUnsigned(taxCode, now, now.AddHours(24));
        }

        /// <summary>Token đã hết hạn từ một giờ trước, dùng để thử luồng hết phiên.</summary>
        public static string ExpiredTokenFor(string taxCode)
        {
            var now = DateTime.UtcNow;
            return PortalToken.BuildUnsigned(taxCode, now.AddHours(-25), now.AddHours(-1));
        }

        /// <summary>Tìm theo khóa tổng hợp mà cổng dùng cho xem chi tiết và xuất XML.</summary>
        public MockInvoiceRecord Find(string sellerTaxCode, string invoiceSymbol, string invoiceNumber, string formSymbol)
        {
            return _records.FirstOrDefault(r =>
                r.Dto.SellerTaxCode == sellerTaxCode
                && r.Dto.InvoiceSymbol == invoiceSymbol
                && Number(r.Dto.InvoiceNumber) == Number(invoiceNumber)
                && r.Dto.FormSymbol == formSymbol);
        }

        /// <summary>Số hóa đơn của cổng là số nguyên (<c>"shdon":40</c>) nên so sánh theo giá trị số.</summary>
        private static string Number(string text)
        {
            long value;
            return long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) ? value.ToString(CultureInfo.InvariantCulture) : text;
        }

        /// <summary>Hóa đơn lập trong khoảng ngày (gồm cả hai đầu), mới nhất trước như sắp xếp <c>tdlap:desc</c> của cổng.</summary>
        public IList<MockInvoiceRecord> Query(InvoiceDirection direction, DateTime from, DateTime to)
        {
            return _records
                .Where(r => r.Direction == direction && r.Dto.IssueDate >= from.Date && r.Dto.IssueDate <= to.Date)
                .OrderByDescending(r => r.Dto.IssueDate)
                .ThenByDescending(r => long.Parse(r.Dto.InvoiceNumber, CultureInfo.InvariantCulture))
                .ToList();
        }

        /// <summary>Một phần tử của <c>datas</c> trong phản hồi tra cứu, tên trường đúng như cổng thật.</summary>
        public static JObject ToSearchJson(MockInvoiceRecord record)
        {
            var d = record.Dto;
            var item = new JObject
            {
                { "nbmst", d.SellerTaxCode },
                { "khmshdon", int.Parse(d.FormSymbol, CultureInfo.InvariantCulture) },
                { "khhdon", d.InvoiceSymbol },
                { "shdon", long.Parse(d.InvoiceNumber, CultureInfo.InvariantCulture) },
                { "cqt", "0155" },
                { "cttkhac", new JArray() },
                { "dvtte", d.Currency },
                { "hdon", "01" },
                { "hthdon", 1 },
                { "htttoan", 9 },
                { "id", d.PortalInvoiceId },
                { "mhdon", MockCode(d.PortalInvoiceId) },
                { "nbdchi", "Địa chỉ người bán mẫu" },
                { "nbten", d.SellerName },
                { "nmdchi", d.BuyerAddress },
                { "nmmst", d.BuyerTaxCode == null ? JValue.CreateNull() : new JValue(d.BuyerTaxCode) },
                { "nmcmnd", d.BuyerIdNumber == null ? JValue.CreateNull() : new JValue(d.BuyerIdNumber) },
                { "nmten", d.BuyerName },
                { "tchat", 1 },
                { "tdlap", ToPortalUtc(d.IssueDate) },
                { "tgia", d.ExchangeRate ?? 1m },
                { "tgtcthue", d.TotalBeforeTax ?? 0m },
                { "tgtthue", d.TotalTax ?? 0m },
                { "tgtttbso", d.TotalPayment ?? 0m },
                { "thdon", "Hóa đơn GTGT" },
                { "tthai", int.Parse(d.StatusRaw, CultureInfo.InvariantCulture) },
                { "ttxly", int.Parse(d.CheckResultRaw, CultureInfo.InvariantCulture) },
                { "ttcktmai", d.TotalDiscount.HasValue ? new JValue(d.TotalDiscount.Value) : JValue.CreateNull() },
                { "tgtphi", d.TotalFee.HasValue ? new JValue(d.TotalFee.Value) : JValue.CreateNull() },
                { "unhiem", record.Proxy ? new JValue(1) : JValue.CreateNull() },
                { "hdonLquans", JValue.CreateNull() }
            };
            return item;
        }

        /// <summary>Phản hồi của API chi tiết: như phần tử tra cứu cộng danh sách hàng hóa <c>hdhhdvu</c>.</summary>
        public static JObject ToDetailJson(MockInvoiceRecord record)
        {
            var json = ToSearchJson(record);
            var lines = new JArray();
            var stt = 0;
            foreach (var line in record.Lines)
            {
                stt++;
                lines.Add(new JObject
                {
                    { "id", Guid.NewGuid().ToString() },
                    { "idhdon", record.Dto.PortalInvoiceId },
                    { "stt", stt },
                    { "ten", line.Name },
                    { "dvtinh", line.Unit },
                    { "sluong", line.Quantity },
                    { "dgia", line.UnitPrice },
                    { "thtien", line.Amount },
                    { "ltsuat", line.TaxRate },
                    { "tsuat", decimal.Parse(line.TaxRate.TrimEnd('%'), CultureInfo.InvariantCulture) / 100m }
                });
            }

            json["hdhhdvu"] = lines;
            return json;
        }

        /// <summary>
        /// XML hóa đơn giả theo cấu trúc của file <c>invoice.xml</c> mà API xuất XML của cổng trả về
        /// (<c>HDon/DLHDon/TTChung|NDHDon</c>, <c>MCCQT</c>, <c>DSCKS</c>); chữ ký được thay bằng phần tử trống.
        /// </summary>
        public static byte[] BuildXml(MockInvoiceRecord record)
        {
            var d = record.Dto;
            var doc = new XDocument(
                new XDeclaration("1.0", "UTF-8", null),
                new XComment(" DỮ LIỆU GIẢ - không phải hóa đơn thật "),
                new XElement("HDon",
                    new XElement("DLHDon", new XAttribute("Id", "DuLieuKy"),
                        new XElement("TTChung",
                            new XElement("PBan", "2.1.0"),
                            new XElement("THDon", "Hóa đơn GTGT"),
                            new XElement("KHMSHDon", d.FormSymbol),
                            new XElement("KHHDon", d.InvoiceSymbol),
                            new XElement("SHDon", Number(d.InvoiceNumber)),
                            new XElement("NLap", d.IssueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                            new XElement("HDCTTChinh", "0"),
                            new XElement("DVTTe", d.Currency),
                            new XElement("TGia", (d.ExchangeRate ?? 1m).ToString(CultureInfo.InvariantCulture)),
                            new XElement("HTTToan", "TM, CK"),
                            new XElement("MSTTCGP", "0100684378")),
                        new XElement("NDHDon",
                            new XElement("NBan", new XElement("Ten", d.SellerName), new XElement("MST", d.SellerTaxCode), new XElement("DChi", "Địa chỉ người bán mẫu")),
                            new XElement("NMua",
                                new XElement("Ten", d.BuyerName),
                                new XElement("MST", d.BuyerTaxCode ?? d.BuyerIdNumber),
                                new XElement("DChi", d.BuyerAddress)),
                            new XElement("DSHHDVu", record.Lines.Select((l, i) =>
                                new XElement("HHDVu",
                                    new XElement("TChat", "1"),
                                    new XElement("STT", i + 1),
                                    new XElement("THHDVu", l.Name),
                                    new XElement("DVTinh", l.Unit),
                                    new XElement("SLuong", Money(l.Quantity)),
                                    new XElement("DGia", Money(l.UnitPrice)),
                                    new XElement("TSuat", l.TaxRate),
                                    new XElement("ThTien", Money(l.Amount))))),
                            new XElement("TToan",
                                new XElement("TgTCThue", Money(d.TotalBeforeTax)),
                                new XElement("TgTThue", Money(d.TotalTax)),
                                new XElement("TgTTTBSo", Money(d.TotalPayment))))),
                    new XElement("MCCQT", new XAttribute("Id", "Id-mock"), MockCode(d.PortalInvoiceId)),
                    new XElement("DSCKS", new XElement("NBan"), new XElement("CQT"))));

            using (var stream = new System.IO.MemoryStream())
            {
                var settings = new System.Xml.XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = false };
                using (var writer = System.Xml.XmlWriter.Create(stream, settings)) doc.Save(writer);
                return stream.ToArray();
            }
        }

        /// <summary>
        /// File zip giống phản hồi xuất XML của cổng: <c>invoice.xml</c> kèm trang xem <c>invoice.html</c> và <c>details.js</c>.
        /// Engine tải chỉ được lấy đúng <c>invoice.xml</c>.
        /// </summary>
        public static byte[] BuildExportZip(MockInvoiceRecord record, byte[] xmlOverride = null)
        {
            using (var stream = new System.IO.MemoryStream())
            {
                using (var zip = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create, true))
                {
                    Add(zip, "invoice.xml", xmlOverride ?? BuildXml(record));
                    Add(zip, "invoice.html", Encoding.UTF8.GetBytes("<html><body>MOCK VIEWER</body></html>"));
                    Add(zip, "details.js", Encoding.UTF8.GetBytes("// mock"));
                }

                return stream.ToArray();
            }
        }

        private static void Add(System.IO.Compression.ZipArchive zip, string name, byte[] content)
        {
            var entry = zip.CreateEntry(name);
            using (var s = entry.Open()) s.Write(content, 0, content.Length);
        }

        /// <summary>Cổng trả <c>tdlap</c> là thời điểm UTC của 0 giờ ngày lập theo giờ Việt Nam (UTC+7).</summary>
        public static string ToPortalUtc(DateTime issueDate)
        {
            return issueDate.Date.AddHours(-7).ToString("yyyy-MM-ddTHH:mm:ss'Z'", CultureInfo.InvariantCulture);
        }

        private static string MockCode(string id)
        {
            using (var md5 = MD5.Create())
            {
                return BitConverter.ToString(md5.ComputeHash(Encoding.UTF8.GetBytes(id))).Replace("-", string.Empty);
            }
        }

        private static string Money(decimal? value)
        {
            return (value ?? 0m).ToString(CultureInfo.InvariantCulture);
        }

        private void Generate(int seed)
        {
            foreach (var direction in new[] { InvoiceDirection.Purchase, InvoiceDirection.Sold })
            {
                for (var year = 2025; year <= 2026; year++)
                {
                    var sequence = 0;
                    for (var month = 1; month <= 12; month++)
                    {
                        var count = CountFor(direction, year, month);
                        var rnd = new Random(seed * 100003 + year * 131 + month * 17 + (direction == InvoiceDirection.Purchase ? 1 : 2));
                        for (var i = 1; i <= count; i++)
                        {
                            sequence++;
                            _records.Add(MakeRecord(direction, year, month, i, sequence, rnd));
                        }
                    }
                }
            }
        }

        private static int CountFor(InvoiceDirection direction, int year, int month)
        {
            if (direction == InvoiceDirection.Purchase && year == 2026 && month == 7) return 120;
            if (direction == InvoiceDirection.Sold && year == 2026 && month == 3) return 0;
            if (direction == InvoiceDirection.Sold && year == 2026 && month == 2) return 1;
            return 4 + (year * 7 + month * 3 + (direction == InvoiceDirection.Sold ? 5 : 0)) % 9;
        }

        private MockInvoiceRecord MakeRecord(InvoiceDirection direction, int year, int month, int index, int sequence, Random rnd)
        {
            var days = DateTime.DaysInMonth(year, month);
            var day = 1 + (index * 7) % days;
            var partner = (index + month) % CounterpartyTaxCodes.Length;
            var ownName = "Công ty mẫu " + TaxCode;
            var foreign = index % 17 == 0;

            // Bán ra cho người mua là cá nhân: cổng chỉ có CCCD, không có MST người mua.
            var individualBuyer = direction == InvoiceDirection.Sold && index % 6 == 0;

            var before = foreign ? rnd.Next(10, 500) * 10m : rnd.Next(1, 500) * 100000m;
            var rateText = index % 5 == 0 ? "8%" : "10%";
            var tax = Math.Round(before * (rateText == "8%" ? 0.08m : 0.10m), foreign ? 2 : 0);
            decimal? discount = index % 4 == 0 ? Math.Round(before * 0.05m, foreign ? 2 : 0) : (decimal?)null;
            decimal? fee = index % 11 == 0 ? 15000m : (decimal?)null;
            var payment = before + tax - (discount ?? 0m) + (fee ?? 0m);

            var id = Deterministic((direction == InvoiceDirection.Purchase ? "pur|" : "sold|") + TaxCode + "|" + year + "|" + month + "|" + index);
            var dto = new PortalInvoiceDto
            {
                PortalInvoiceId = id,
                FormSymbol = "1",
                InvoiceSymbol = "C" + (year % 100).ToString("D2") + "TAA",
                InvoiceNumber = sequence.ToString(CultureInfo.InvariantCulture),
                IssueDate = new DateTime(year, month, day),
                SellerTaxCode = direction == InvoiceDirection.Purchase ? CounterpartyTaxCodes[partner] : TaxCode,
                SellerName = direction == InvoiceDirection.Purchase ? CounterpartyNames[partner] : ownName,
                BuyerTaxCode = direction == InvoiceDirection.Purchase ? TaxCode : (individualBuyer ? null : CounterpartyTaxCodes[partner]),
                BuyerIdNumber = individualBuyer ? "0364024" + (10000 + index).ToString(CultureInfo.InvariantCulture) : null,
                BuyerName = direction == InvoiceDirection.Purchase ? ownName : (individualBuyer ? "Nguyễn Văn Cá Nhân " + index : CounterpartyNames[partner]),
                BuyerAddress = "Số " + (index + 10) + " đường Mẫu, quận " + (month % 12 + 1) + ", Hà Nội",
                TotalBeforeTax = before,
                TotalTax = tax,
                TotalDiscount = discount,
                TotalFee = fee,
                TotalPayment = payment,
                Currency = foreign ? "USD" : "VND",
                ExchangeRate = foreign ? 25000m : 1m,
                StatusRaw = StatusFor(index),
                CheckResultRaw = CheckResultFor(index)
            };

            return new MockInvoiceRecord
            {
                Direction = direction,
                Dto = dto,
                Proxy = index % 10 == 0,
                Lines = new List<MockLine>
                {
                    new MockLine { Name = "Hàng hóa mẫu " + index, Unit = "cái", Quantity = 1m, UnitPrice = before, Amount = before, TaxRate = rateText }
                }
            };
        }

        /// <summary>Phần lớn hóa đơn mới; rải rác thay thế, điều chỉnh, đã bị thay thế/điều chỉnh và hủy.</summary>
        private static string StatusFor(int index)
        {
            if (index % 19 == 0) return "6";
            if (index % 15 == 0) return "5";
            if (index % 13 == 0) return "4";
            if (index % 9 == 0) return "3";
            if (index % 7 == 0) return "2";
            return "1";
        }

        /// <summary>Phần lớn đã cấp mã (5); rải rác các kết quả kiểm tra khác (0–8).</summary>
        private static string CheckResultFor(int index)
        {
            if (index % 13 == 0) return "1";
            if (index % 11 == 0) return "0";
            if (index % 8 == 0) return "6";
            return "5";
        }

        private static string Deterministic(string key)
        {
            using (var md5 = MD5.Create())
            {
                return new Guid(md5.ComputeHash(Encoding.UTF8.GetBytes(key))).ToString();
            }
        }
    }
}
#endif
