using System;
using HoaDonDienTu.Application.Portal;
using HoaDonDienTu.Domain.Invoices;

namespace HoaDonDienTu.Application.Invoices
{
    /// <summary>
    /// Chuyển <see cref="PortalInvoiceDto"/> (đã khớp mẫu thật) thành <see cref="NormalizedInvoice"/>. Mọi thay đổi cấu trúc API xử lý ở adapter
    /// nên lớp này chỉ ánh xạ và làm sạch khoảng trắng (NFR-MNT-002).
    /// </summary>
    public sealed class InvoiceNormalizer : IInvoiceNormalizer
    {
        public NormalizedInvoice Normalize(PortalInvoiceDto dto, InvoiceDirection direction)
        {
            if (dto == null) throw new ArgumentNullException("dto");

            return new NormalizedInvoice
            {
                Direction = direction,
                PortalInvoiceId = Text(dto.PortalInvoiceId),
                FormSymbol = Text(dto.FormSymbol),
                InvoiceSymbol = Text(dto.InvoiceSymbol),
                InvoiceNumber = Text(dto.InvoiceNumber),
                IssueDate = dto.IssueDate.Date,
                SellerTaxCode = Text(dto.SellerTaxCode),
                SellerName = Text(dto.SellerName),
                BuyerTaxCode = Text(dto.BuyerTaxCode),
                BuyerIdNumber = Text(dto.BuyerIdNumber),
                BuyerName = Text(dto.BuyerName),
                BuyerAddress = Text(dto.BuyerAddress),
                TotalBeforeTax = dto.TotalBeforeTax,
                TotalTax = dto.TotalTax,
                TotalDiscount = dto.TotalDiscount,
                TotalFee = dto.TotalFee,
                TotalPayment = dto.TotalPayment,
                Currency = Text(dto.Currency),
                ExchangeRate = dto.ExchangeRate,
                StatusRaw = Text(dto.StatusRaw),
                Status = InvoiceCodes.StatusFrom(dto.StatusRaw),
                CheckResultRaw = Text(dto.CheckResultRaw),
                CheckResult = InvoiceCodes.CheckResultFrom(dto.CheckResultRaw)
            };
        }

        private static string Text(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
