using System;
using System.Collections.Generic;

namespace HoaDonDienTu.WinForms.Detail
{
    public sealed class ViewerNode
    {
        public ViewerNode(string text)
        {
            Text = text;
            Children = new List<ViewerNode>();
        }

        public string Text { get; private set; }
        public IList<ViewerNode> Children { get; private set; }
    }

    public sealed class ViewerVersionOption
    {
        public ViewerVersionOption(int versionNo, string label)
        {
            VersionNo = versionNo;
            Label = label;
        }

        public int VersionNo { get; private set; }
        public string Label { get; private set; }

        public override string ToString()
        {
            return Label;
        }
    }

    public sealed class InvoiceViewerViewModel
    {
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public string ShaText { get; set; }
        public IList<ViewerVersionOption> Versions { get; set; }
        public int SelectedVersion { get; set; }

        /// <summary>null khi XML không đọc được (vẫn có thể có nội dung thô).</summary>
        public ViewerNode Tree { get; set; }

        /// <summary>null khi không đọc được file.</summary>
        public string RawText { get; set; }

        public bool CanSaveCopy { get; set; }
    }

    /// <summary>Nội dung tab PDF: các trang xem trước (PNG) hoặc lý do không dựng được.</summary>
    public sealed class InvoicePdfViewModel
    {
        /// <summary>Ảnh PNG từng trang (khoảng 96 dpi); rỗng khi chưa dựng được.</summary>
        public IList<byte[]> Pages { get; set; }

        /// <summary>Thông báo thay cho trang xem trước: đang dựng, không dựng được và vì sao, hoặc chưa hỗ trợ.</summary>
        public string Notice { get; set; }

        public bool IsError { get; set; }

        /// <summary>Cho phép In và Lưu PDF (đã có bản thể hiện hợp lệ).</summary>
        public bool CanPrintOrSave { get; set; }
    }

    public interface IInvoiceViewerView
    {
        event EventHandler CloseRequested;
        event EventHandler SaveCopyRequested;
        event EventHandler SavePdfRequested;
        event EventHandler PrintRequested;
        event EventHandler<VersionRequestedEventArgs> VersionSelected;

        void ShowResult(InvoiceViewerViewModel model);

        /// <summary>Cập nhật tab PDF; có thể gọi sau <see cref="ShowResult"/> khi bản thể hiện dựng xong.</summary>
        void ShowPdf(InvoicePdfViewModel model);

        /// <summary>Cảnh báo cụ thể (file mất/hỏng/sai hash) hoặc kết quả lưu bản sao.</summary>
        void ShowMessage(string text, bool isError);
    }
}
