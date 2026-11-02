using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Common;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Invoices;
using HoaDonDienTu.Application.Navigation;
using HoaDonDienTu.Domain.Invoices;
using HoaDonDienTu.WinForms.Common;

namespace HoaDonDienTu.WinForms.Detail
{
    /// <summary>
    /// SCR-07 Xem chứng từ (DD-08): hiển thị XML đã lưu dạng cây và nội dung thô, chỉ đọc, đổi được phiên bản và lưu bản sao.
    /// XML gốc không bao giờ bị sửa. Tab PDF và nút In chờ quyết định G-03/G-09 nên chưa hoạt động.
    /// </summary>
    public sealed class InvoiceViewerPresenter : IDisposable
    {
        private readonly IInvoiceViewerView _view;
        private readonly IInvoiceDetailService _service;
        private readonly ICompanyContextAccessor _context;
        private readonly INavigationService _navigation;
        private readonly ISaveFilePicker _picker;
        private readonly IUiOperationRunner _runner;
        private readonly IUserErrorPresenter _errors;
        private readonly Guid _invoiceId;
        private readonly int _initialVersion;
        private bool _initialized;
        private bool _disposed;
        private InvoiceDetail _detail;
        private InvoiceXmlView _xml;
        private int _loadSequence;

        public InvoiceViewerPresenter(
            IInvoiceViewerView view,
            IInvoiceDetailService service,
            ICompanyContextAccessor context,
            INavigationService navigation,
            ISaveFilePicker picker,
            IUiOperationRunner runner,
            IUserErrorPresenter errors,
            Guid invoiceId,
            int versionNo)
        {
            if (view == null) throw new ArgumentNullException("view");
            if (service == null) throw new ArgumentNullException("service");
            if (context == null) throw new ArgumentNullException("context");
            if (navigation == null) throw new ArgumentNullException("navigation");
            if (picker == null) throw new ArgumentNullException("picker");
            if (runner == null) throw new ArgumentNullException("runner");
            if (errors == null) throw new ArgumentNullException("errors");
            _view = view;
            _service = service;
            _context = context;
            _navigation = navigation;
            _picker = picker;
            _runner = runner;
            _errors = errors;
            _invoiceId = invoiceId;
            _initialVersion = versionNo;
        }

        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            _view.CloseRequested += OnClose;
            _view.SaveCopyRequested += OnSaveCopy;
            _view.VersionSelected += OnVersionSelected;

            Run("Nạp trình xem", async token =>
            {
                if (!_context.HasCurrent)
                {
                    _view.ShowMessage("Chưa chọn doanh nghiệp.", true);
                    return;
                }

                var detail = await _service.GetAsync(_invoiceId, token);
                if (_disposed) return;
                if (detail == null)
                {
                    _view.ShowMessage("Hóa đơn này không còn trong kho của doanh nghiệp đang mở.", true);
                    return;
                }

                _detail = detail;
                await LoadVersionAsync(_initialVersion, token);
            });
        }

        public void Dispose()
        {
            if (!_initialized) return;
            _initialized = false;
            _disposed = true;
            _view.CloseRequested -= OnClose;
            _view.SaveCopyRequested -= OnSaveCopy;
            _view.VersionSelected -= OnVersionSelected;
        }

        // ---- Sự kiện ----

        private void OnClose(object sender, EventArgs e)
        {
            Run("Đóng trình xem", token => _navigation.NavigateAsync(ScreenId.InvoiceDetail, new InvoiceDetailRequest(_invoiceId), token));
        }

        private void OnVersionSelected(object sender, VersionRequestedEventArgs e)
        {
            Run("Đổi phiên bản XML", token => LoadVersionAsync(e.VersionNo, token));
        }

        private void OnSaveCopy(object sender, EventArgs e)
        {
            var xml = _xml;
            var detail = _detail;
            if (xml == null || xml.Bytes == null || detail == null) return;

            Run("Lưu bản sao XML", token =>
            {
                var name = SuggestedName(detail.Invoice, xml.VersionNo);
                var path = _picker.Pick(name);
                if (path == null) return Task.FromResult(0);

                // Hộp thoại lưu đã hỏi xác nhận nếu trùng tên; ở đây chỉ ghi đúng byte nguyên văn, không biến đổi.
                File.WriteAllBytes(path, xml.Bytes);
                _view.ShowMessage("Đã lưu bản sao XML: " + path, false);
                return Task.FromResult(0);
            });
        }

        // ---- Nạp và hiển thị ----

        private async Task LoadVersionAsync(int versionNo, CancellationToken token)
        {
            var sequence = Interlocked.Increment(ref _loadSequence);
            var xml = await _service.OpenXmlAsync(_invoiceId, versionNo, token);
            if (_disposed || sequence != _loadSequence) return;

            _xml = xml;
            _view.ShowResult(Build(_detail, xml));
            _view.ShowMessage(xml.Problem ?? string.Empty, xml.Problem != null);
        }

        public static InvoiceViewerViewModel Build(InvoiceDetail detail, InvoiceXmlView xml)
        {
            var i = detail.Invoice;
            return new InvoiceViewerViewModel
            {
                Title = "Xem chứng từ",
                Subtitle = "Hóa đơn " + i.InvoiceNumber + " · " + i.InvoiceSymbol + " · Phiên bản XML v" + xml.VersionNo,
                ShaText = "SHA-256: " + Shorten(xml.Sha256),
                Versions = detail.Versions.Select(v => new ViewerVersionOption(v.VersionNo, "v" + v.VersionNo + (v.IsCurrent ? " (hiện hành)" : string.Empty))).ToList(),
                SelectedVersion = xml.VersionNo,
                Tree = xml.Tree == null ? null : ToNode(xml.Tree),
                RawText = xml.RawText,
                CanSaveCopy = xml.Bytes != null
            };
        }

        private static ViewerNode ToNode(XmlTreeNode node)
        {
            var label = node.Name + (string.IsNullOrEmpty(node.Attributes) ? string.Empty : " [" + node.Attributes + "]");
            var result = new ViewerNode(node.Children.Count == 0 && node.Value != null ? label + ": " + node.Value.Trim() : label);
            foreach (var child in node.Children) result.Children.Add(ToNode(child));
            return result;
        }

        public static string SuggestedName(NormalizedInvoice invoice, int versionNo)
        {
            var name = invoice.InvoiceSymbol + "_" + invoice.InvoiceNumber + "_v" + versionNo + ".xml";
            foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name;
        }

        private static string Shorten(string sha)
        {
            return string.IsNullOrEmpty(sha) || sha.Length <= 10 ? sha : sha.Substring(0, 4) + "…" + sha.Substring(sha.Length - 4);
        }

        private async void Run(string name, Func<CancellationToken, Task> operation)
        {
            await _runner.RunAsync(name, operation, state => { if (state.Error != null) _errors.Show(state.Error); });
        }
    }
}
