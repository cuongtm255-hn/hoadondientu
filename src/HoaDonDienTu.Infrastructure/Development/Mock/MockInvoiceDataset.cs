#if DEBUG
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using HoaDonDienTu.Application.Portal;
using HoaDonDienTu.Domain.Invoices;

namespace HoaDonDienTu.Infrastructure.Development.Mock
{
    public sealed class MockInvoiceRecord
    {
        public InvoiceDirection Direction { get; set; }
        public PortalInvoiceDto Dto { get; set; }
    }

    /// <summary>
    /// Dữ liệu hóa đơn giả, xác định (cùng MST + seed cho cùng kết quả), phủ 2025-01…2026-12 cho cả mua vào và bán ra.
    /// Có sẵn các tình huống: tháng nhiều trang (mua vào 2026-07: 120 hóa đơn), tháng rỗng (bán ra 2026-03),
    /// tháng một hóa đơn (bán ra 2026-02), số hóa đơn có số 0 đầu, ngoại tệ, trạng thái/kết quả kiểm tra khác nhau,
    /// trường tùy chọn để trống. Toàn bộ là dữ liệu bịa, KHÔNG phải dữ liệu cổng Thuế.
    /// </summary>
    public sealed class MockInvoiceDataset
    {
        public const string TokenPrefix = "mock-token-";

        private static readonly string[] CounterpartyTaxCodes =
        {
            "0100100001", "0100100002", "0100100003", "0100100004", "0100100005", "0100100006-001"
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

        public static string TokenFor(string taxCode)
        {
            return TokenPrefix + taxCode;
        }

        public MockInvoiceRecord Find(string portalInvoiceId)
        {
            return _records.FirstOrDefault(r => r.Dto.PortalInvoiceId == portalInvoiceId);
        }

        public IList<MockInvoiceRecord> Query(InvoiceDirection direction, DateTime from, DateTime to)
        {
            return _records
                .Where(r => r.Direction == direction && r.Dto.IssueDate >= from.Date && r.Dto.IssueDate <= to.Date)
                .OrderBy(r => r.Dto.IssueDate)
                .ThenBy(r => r.Dto.PortalInvoiceId, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>XML hóa đơn giả theo cấu trúc giống hóa đơn điện tử (đủ để DD-06/DD-08 thử đọc, băm và hiển thị).</summary>
        public static byte[] BuildXml(MockInvoiceRecord record)
        {
            var d = record.Dto;
            var doc = new XDocument(
                new XDeclaration("1.0", "UTF-8", null),
                new XComment(" DỮ LIỆU GIẢ - không phải hóa đơn thật "),
                new XElement("HDon",
                    new XElement("DLHDon",
                        new XElement("TTChung",
                            new XElement("PBan", "2.0.0"),
                            new XElement("THDon", "Hóa đơn giá trị gia tăng"),
                            new XElement("KHMSHDon", d.FormSymbol),
                            new XElement("KHHDon", d.InvoiceSymbol),
                            new XElement("SHDon", d.InvoiceNumber),
                            new XElement("NLap", d.IssueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                            new XElement("DVTTe", d.Currency),
                            new XElement("TGia", (d.ExchangeRate ?? 1m).ToString(CultureInfo.InvariantCulture))),
                        new XElement("NDHDon",
                            new XElement("NBan", new XElement("Ten", d.SellerName), new XElement("MST", d.SellerTaxCode)),
                            new XElement("NMua",
                                new XElement("Ten", d.BuyerName),
                                new XElement("MST", d.BuyerTaxCode),
                                new XElement("DChi", d.BuyerAddress)),
                            new XElement("TToan",
                                new XElement("TgTCThue", Money(d.TotalBeforeTax)),
                                new XElement("TgTThue", Money(d.TotalTax)),
                                new XElement("TgTTTBSo", Money(d.TotalPayment)))))));

            using (var stream = new System.IO.MemoryStream())
            {
                var settings = new System.Xml.XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true };
                using (var writer = System.Xml.XmlWriter.Create(stream, settings)) doc.Save(writer);
                return stream.ToArray();
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

            var before = foreign ? rnd.Next(10, 500) * 10m : rnd.Next(1, 500) * 100000m;
            var rate = index % 5 == 0 ? 0.08m : 0.10m;
            var tax = Math.Round(before * rate, foreign ? 2 : 0);
            decimal? discount = index % 4 == 0 ? Math.Round(before * 0.05m, foreign ? 2 : 0) : (decimal?)null;
            decimal? fee = index % 11 == 0 ? 15000m : (decimal?)null;
            var payment = before + tax - (discount ?? 0m) + (fee ?? 0m);

            var dto = new PortalInvoiceDto
            {
                PortalInvoiceId = (direction == InvoiceDirection.Purchase ? "pur-" : "sold-") + TaxCode + "-" + year + month.ToString("D2") + "-" + index.ToString("D4"),
                FormSymbol = "1",
                InvoiceSymbol = "C" + (year % 100).ToString("D2") + "TAA",
                InvoiceNumber = sequence.ToString("D7"),
                IssueDate = new DateTime(year, month, day),
                SellerTaxCode = direction == InvoiceDirection.Purchase ? CounterpartyTaxCodes[partner] : TaxCode,
                SellerName = direction == InvoiceDirection.Purchase ? CounterpartyNames[partner] : ownName,
                BuyerTaxCode = direction == InvoiceDirection.Purchase ? TaxCode : CounterpartyTaxCodes[partner],
                BuyerName = direction == InvoiceDirection.Purchase ? ownName : CounterpartyNames[partner],
                BuyerAddress = "Số " + (index + 10) + " đường Mẫu, quận " + (month % 12 + 1) + ", Hà Nội",
                TotalBeforeTax = before,
                TotalTax = tax,
                TotalDiscount = discount,
                TotalFee = fee,
                TotalPayment = payment,
                Currency = foreign ? "USD" : "VND",
                ExchangeRate = foreign ? 25000m : (decimal?)null,
                StatusRaw = StatusFor(index),
                CheckResultRaw = index % 13 == 0 ? "1" : "0"
            };

            return new MockInvoiceRecord { Direction = direction, Dto = dto };
        }

        private static string StatusFor(int index)
        {
            if (index % 19 == 0) return "6";
            if (index % 7 == 0) return "2";
            if (index % 9 == 0) return "3";
            return "1";
        }
    }
}
#endif
