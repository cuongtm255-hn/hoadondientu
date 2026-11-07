using System;
using System.Collections.Generic;
using HoaDonDienTu.Domain.Invoices;

namespace HoaDonDienTu.Application.Portal
{
    /// <summary>
    /// Bộ lọc tra cứu theo form "Tra cứu hóa đơn" của cổng Thuế (spec.xlsx, sample-api-web.md mục 2). Mọi trường để trống nghĩa là
    /// "Tất cả" và không được gửi lên cổng. Khoảng ngày lập luôn bắt buộc nên nằm ở <see cref="InvoiceSearchRequest"/>.
    /// </summary>
    public sealed class InvoiceSearchFilter
    {
        /// <summary>Bán ra: MST người mua (<c>nmmst</c>). Mua vào: MST người bán (<c>nbmst</c>).</summary>
        public string CounterpartyTaxCode { get; set; }

        /// <summary>CCCD người mua (<c>nmcmnd</c>); có ở cả tra cứu bán ra và mua vào.</summary>
        public string BuyerIdNumber { get; set; }

        /// <summary>Mã trạng thái hóa đơn <c>tthai</c> "1".."6" (xem <see cref="PortalOptions.InvoiceStatuses"/>); rỗng là Tất cả.</summary>
        public string StatusCode { get; set; }

        /// <summary>
        /// Mã kết quả kiểm tra <c>ttxly</c>. Bán ra: "0".."8" (<see cref="PortalOptions.CheckResults"/>), rỗng là Tất cả.
        /// Mua vào: "5", "6", "8"; cổng bắt buộc có giá trị nên rỗng nghĩa là tải lần lượt cả ba (<see cref="PortalOptions.CheckResultPasses"/>).
        /// </summary>
        public string CheckResultCode { get; set; }

        /// <summary>Số hóa đơn (<c>shdon</c>), giữ dạng text.</summary>
        public string InvoiceNumber { get; set; }

        /// <summary>Ký hiệu mẫu số hóa đơn <c>khmshdon</c> "1".."9" (xem <see cref="PortalOptions.FormSymbols"/>).</summary>
        public string FormSymbol { get; set; }

        /// <summary>Ký hiệu hóa đơn <c>khhdon</c>, ví dụ C26TBN.</summary>
        public string InvoiceSymbol { get; set; }

        /// <summary>Hóa đơn ủy nhiệm (<c>unhiem</c>); chỉ gửi khi true, giống checkbox của cổng.</summary>
        public bool? ProxyInvoice { get; set; }

        public InvoiceSearchFilter Clone()
        {
            return (InvoiceSearchFilter)MemberwiseClone();
        }
    }

    public sealed class InvoiceSearchRequest
    {
        public InvoiceSearchRequest(InvoiceDirection direction, DateTime from, DateTime to)
        {
            if (to.Date < from.Date) throw new ArgumentException("Ngày kết thúc nhỏ hơn ngày bắt đầu.");
            Direction = direction;
            From = from.Date;
            To = to.Date;
            Filter = new InvoiceSearchFilter();
        }

        public InvoiceDirection Direction { get; private set; }

        /// <summary>Ngày bắt đầu (gồm). Cổng Thuế giới hạn một tháng mỗi lần; việc chia đoạn do DD-04 đảm nhiệm.</summary>
        public DateTime From { get; private set; }

        /// <summary>Ngày kết thúc (gồm).</summary>
        public DateTime To { get; private set; }

        public InvoiceSearchFilter Filter { get; set; }

        /// <summary>Null cho trang đầu; các trang sau dùng đúng giá trị <see cref="InvoicePage.NextCursor"/> trả về.</summary>
        public string Cursor { get; set; }

        /// <summary>Kích thước trang mong muốn; null dùng giá trị cấu hình. Cổng có thể giới hạn nhỏ hơn.</summary>
        public int? PageSize { get; set; }
    }

    /// <summary>Hóa đơn như cổng Thuế trả về, chưa chuẩn hóa (DD-06 chuẩn hóa thành NormalizedInvoice).</summary>
    public sealed class PortalInvoiceDto
    {
        public string PortalInvoiceId { get; set; }
        public string FormSymbol { get; set; }
        public string InvoiceSymbol { get; set; }

        /// <summary>Giữ dạng text để bảo toàn số 0 đầu.</summary>
        public string InvoiceNumber { get; set; }

        public DateTime IssueDate { get; set; }
        public string SellerTaxCode { get; set; }
        public string SellerName { get; set; }
        public string BuyerTaxCode { get; set; }

        /// <summary>CCCD người mua khi người mua là cá nhân (<c>nmcmnd</c>); cổng điền vào cột MST người mua trên bảng kê.</summary>
        public string BuyerIdNumber { get; set; }

        public string BuyerName { get; set; }
        public string BuyerAddress { get; set; }
        public decimal? TotalBeforeTax { get; set; }
        public decimal? TotalTax { get; set; }
        public decimal? TotalDiscount { get; set; }
        public decimal? TotalFee { get; set; }
        public decimal? TotalPayment { get; set; }
        public string Currency { get; set; }
        public decimal? ExchangeRate { get; set; }

        /// <summary>Giá trị trạng thái nguồn, giữ nguyên để hiển thị kèm bản chuẩn hóa.</summary>
        public string StatusRaw { get; set; }

        public string CheckResultRaw { get; set; }
    }

    public sealed class InvoicePage
    {
        public InvoicePage(IList<PortalInvoiceDto> items, int? totalCount, string nextCursor)
        {
            Items = new List<PortalInvoiceDto>(items ?? new List<PortalInvoiceDto>()).AsReadOnly();
            TotalCount = totalCount;
            NextCursor = nextCursor;
        }

        public IList<PortalInvoiceDto> Items { get; private set; }

        /// <summary>Tổng số bản ghi của đoạn nếu cổng báo; có thể null.</summary>
        public int? TotalCount { get; private set; }

        /// <summary>Null/rỗng nghĩa là đã hết dữ liệu.</summary>
        public string NextCursor { get; private set; }

        public bool HasMore
        {
            get { return !string.IsNullOrEmpty(NextCursor); }
        }
    }

    /// <summary>Tham chiếu một hóa đơn đã tra cứu để tải XML.</summary>
    public sealed class PortalInvoiceRef
    {
        public PortalInvoiceRef(InvoiceDirection direction, PortalInvoiceDto invoice)
        {
            if (invoice == null) throw new ArgumentNullException("invoice");
            Direction = direction;
            PortalInvoiceId = invoice.PortalInvoiceId;
            SellerTaxCode = invoice.SellerTaxCode;
            FormSymbol = invoice.FormSymbol;
            InvoiceSymbol = invoice.InvoiceSymbol;
            InvoiceNumber = invoice.InvoiceNumber;
            IssueDate = invoice.IssueDate;
        }

        public InvoiceDirection Direction { get; private set; }
        public string PortalInvoiceId { get; private set; }
        public string SellerTaxCode { get; private set; }
        public string FormSymbol { get; private set; }
        public string InvoiceSymbol { get; private set; }
        public string InvoiceNumber { get; private set; }
        public DateTime IssueDate { get; private set; }
    }

    public sealed class InvoiceXmlPayload
    {
        public InvoiceXmlPayload(byte[] content, string suggestedFileName, byte[] portalPackage = null)
        {
            if (content == null) throw new ArgumentNullException("content");
            Content = content;
            SuggestedFileName = suggestedFileName;
            PortalPackage = portalPackage;
        }

        public byte[] Content { get; private set; }

        /// <summary>Nguyên file zip cổng trả về (invoice.xml cùng trang xem HTML, script, ảnh); null khi cổng chỉ trả XML. Lưu cạnh XML để xem và in đúng như cổng.</summary>
        public byte[] PortalPackage { get; private set; }
        public string SuggestedFileName { get; private set; }
    }
}
