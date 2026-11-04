using System;
using System.Collections.Generic;

namespace HoaDonDienTu.WinForms.Detail
{
    /// <summary>Mở SCR-06 cho một hóa đơn trong kho (tham số điều hướng).</summary>
    public sealed class InvoiceDetailRequest
    {
        public InvoiceDetailRequest(Guid invoiceId)
        {
            InvoiceId = invoiceId;
        }

        public Guid InvoiceId { get; private set; }
    }

    /// <summary>Mở SCR-07 để xem một phiên bản XML của hóa đơn (tham số điều hướng).</summary>
    public sealed class InvoiceViewerRequest
    {
        public InvoiceViewerRequest(Guid invoiceId, int versionNo)
        {
            InvoiceId = invoiceId;
            VersionNo = versionNo;
        }

        public Guid InvoiceId { get; private set; }
        public int VersionNo { get; private set; }
    }

    public sealed class DetailField
    {
        public DetailField(string label, string value, bool emphasized = false)
        {
            Label = label;
            Value = value;
            Emphasized = emphasized;
        }

        public string Label { get; private set; }
        public string Value { get; private set; }
        public bool Emphasized { get; private set; }
    }

    public sealed class DetailVersionRow
    {
        public int VersionNo { get; set; }
        public string Name { get; set; }
        public string Date { get; set; }
        public string Session { get; set; }
        public string Status { get; set; }
        public bool Problem { get; set; }
    }

    public sealed class InvoiceDetailViewModel
    {
        public string Title { get; set; }
        public string Badge { get; set; }
        public string Subtitle { get; set; }
        public IList<DetailField> Info { get; set; }
        public IList<DetailField> Seller { get; set; }
        public IList<DetailField> Buyer { get; set; }
        public IList<DetailField> Values { get; set; }

        public string XmlTitle { get; set; }
        public string XmlText { get; set; }
        public string XmlSub { get; set; }
        public bool XmlProblem { get; set; }
        public bool CanViewXml { get; set; }

        public string PdfText { get; set; }
        public string PdfSub { get; set; }

        public IList<DetailVersionRow> Versions { get; set; }
        public IList<DetailField> Source { get; set; }
    }

    public sealed class VersionRequestedEventArgs : EventArgs
    {
        public VersionRequestedEventArgs(int versionNo)
        {
            VersionNo = versionNo;
        }

        public int VersionNo { get; private set; }
    }

    public interface IInvoiceDetailView
    {
        event EventHandler BackRequested;

        /// <summary>Mở phiên bản hiện hành.</summary>
        event EventHandler ViewCurrentRequested;

        /// <summary>Xuất gói ZIP chứa XML hiện hành của hóa đơn này.</summary>
        event EventHandler ExportZipRequested;

        /// <summary>Bật hoặc khóa nút xuất theo quyền của chế độ ứng dụng.</summary>
        void ShowExportAllowed(bool allowed);

        void ShowResult(InvoiceDetailViewModel model);
        void ShowMessage(string text, bool isError);
    }
}
