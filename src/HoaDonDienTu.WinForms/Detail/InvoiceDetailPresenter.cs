using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Common;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Invoices;
using HoaDonDienTu.Application.Navigation;
using HoaDonDienTu.Application.Portal;
using HoaDonDienTu.Domain.Invoices;
using HoaDonDienTu.WinForms.Archive;

namespace HoaDonDienTu.WinForms.Detail
{
    /// <summary>
    /// SCR-06 Chi tiết hóa đơn (DD-08): metadata, file chứng từ, lịch sử phiên bản và nguồn tải của một hóa đơn trong kho.
    /// Chỉ đọc. "Quay lại" về Kho hóa đơn giữ nguyên bộ lọc (trạng thái được SCR-05 nhớ theo doanh nghiệp).
    /// </summary>
    public sealed class InvoiceDetailPresenter : IDisposable
    {
        private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

        private readonly IInvoiceDetailView _view;
        private readonly IInvoiceDetailService _service;
        private readonly ICompanyContextAccessor _context;
        private readonly INavigationService _navigation;
        private readonly IUiOperationRunner _runner;
        private readonly IUserErrorPresenter _errors;
        private readonly Guid _invoiceId;
        private bool _initialized;
        private bool _disposed;
        private InvoiceDetail _detail;

        public InvoiceDetailPresenter(
            IInvoiceDetailView view,
            IInvoiceDetailService service,
            ICompanyContextAccessor context,
            INavigationService navigation,
            IUiOperationRunner runner,
            IUserErrorPresenter errors,
            Guid invoiceId)
        {
            if (view == null) throw new ArgumentNullException("view");
            if (service == null) throw new ArgumentNullException("service");
            if (context == null) throw new ArgumentNullException("context");
            if (navigation == null) throw new ArgumentNullException("navigation");
            if (runner == null) throw new ArgumentNullException("runner");
            if (errors == null) throw new ArgumentNullException("errors");
            _view = view;
            _service = service;
            _context = context;
            _navigation = navigation;
            _runner = runner;
            _errors = errors;
            _invoiceId = invoiceId;
        }

        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            _view.BackRequested += OnBack;
            _view.ViewCurrentRequested += OnViewCurrent;

            Run("Nạp chi tiết hóa đơn", async token =>
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
                    _view.ShowMessage("Hóa đơn này không còn trong kho của doanh nghiệp đang mở. Hãy quay lại Kho hóa đơn.", true);
                    return;
                }

                _detail = detail;
                _view.ShowResult(Build(detail));
                _view.ShowMessage(string.Empty, false);
            });
        }

        public void Dispose()
        {
            if (!_initialized) return;
            _initialized = false;
            _disposed = true;
            _view.BackRequested -= OnBack;
            _view.ViewCurrentRequested -= OnViewCurrent;
        }

        private void OnBack(object sender, EventArgs e)
        {
            Run("Quay lại kho hóa đơn", token => _navigation.NavigateAsync(ScreenId.InvoiceArchive, null, token));
        }

        private void OnViewCurrent(object sender, EventArgs e)
        {
            var detail = _detail;
            if (detail == null || detail.Current == null) return;
            OpenViewer(detail.Current.VersionNo);
        }

        private void OpenViewer(int versionNo)
        {
            Run("Mở trình xem", token => _navigation.NavigateAsync(ScreenId.InvoiceViewer, new InvoiceViewerRequest(_invoiceId, versionNo), token));
        }

        // ---- Dựng view model ----

        public static InvoiceDetailViewModel Build(InvoiceDetail d)
        {
            var i = d.Invoice;
            var purchase = i.Direction == InvoiceDirection.Purchase;
            var current = d.Current;
            var currentOk = current != null && current.FileState == VersionFileState.Ok;

            var model = new InvoiceDetailViewModel
            {
                Title = "Chi tiết hóa đơn " + i.InvoiceNumber,
                Badge = string.IsNullOrEmpty(i.CheckResultRaw) ? string.Empty : PortalOptions.CheckResultLabel(i.CheckResultRaw),
                Subtitle = (purchase ? "Mua vào" : "Bán ra") + " · Ký hiệu " + i.InvoiceSymbol + " · Ngày lập " + Date(i.IssueDate),
                Info = new List<DetailField>
                {
                    new DetailField("Ký hiệu mẫu số", i.FormSymbol),
                    new DetailField("Ký hiệu hóa đơn", i.InvoiceSymbol),
                    new DetailField("Số hóa đơn", i.InvoiceNumber),
                    new DetailField("Ngày lập", Date(i.IssueDate)),
                    new DetailField("Trạng thái", PortalOptions.StatusLabel(i.StatusRaw)),
                    new DetailField("Kết quả kiểm tra", string.IsNullOrEmpty(i.CheckResultRaw) ? "—" : PortalOptions.CheckResultLabel(i.CheckResultRaw)),
                    new DetailField("Mã hóa đơn trên cổng", i.PortalInvoiceId ?? "—")
                },
                Seller = new List<DetailField>
                {
                    new DetailField("MST người bán", i.SellerTaxCode),
                    new DetailField("Tên người bán", i.SellerName)
                },
                Buyer = BuyerFields(i),
                Values = new List<DetailField>
                {
                    new DetailField("Tổng tiền chưa thuế", Money(i.TotalBeforeTax, i.Currency)),
                    new DetailField("Tổng tiền thuế", Money(i.TotalTax, i.Currency)),
                    new DetailField("Chiết khấu thương mại / phí", Money((i.TotalDiscount ?? 0m) + (i.TotalFee ?? 0m), i.Currency)),
                    new DetailField("Tổng tiền thanh toán", Money(i.TotalPayment, i.Currency), true)
                },
                XmlTitle = "XML",
                CanViewXml = currentOk,
                PdfText = currentOk ? "Dựng từ XML" : "Cần file XML",
                PdfSub = currentOk ? "Xem, in và lưu PDF ở màn Xem chứng từ" : "Chưa có file XML hiện hành để dựng PDF",
                Versions = d.Versions.Select(VersionRow).ToList(),
                Source = SourceFields(d)
            };

            if (i.ExchangeRate.HasValue && !string.IsNullOrEmpty(i.Currency) && i.Currency != "VND")
            {
                model.Values.Add(new DetailField("Tỷ giá", i.ExchangeRate.Value.ToString("0.####", Vi)));
            }

            if (current == null)
            {
                model.XmlText = "Không có phiên bản hiện hành";
                model.XmlSub = "Hóa đơn chưa có file XML";
                model.XmlProblem = true;
            }
            else if (!currentOk)
            {
                model.XmlText = "Thiếu file";
                model.XmlSub = "File XML không còn trong thư mục kho";
                model.XmlProblem = true;
            }
            else
            {
                model.XmlText = DateTimeText(current.CreatedAtUtc);
                model.XmlSub = "SHA-256: " + Shorten(current.Sha256) + (d.Versions.Count > 1 ? " · " + d.Versions.Count + " phiên bản" : string.Empty);
            }

            return model;
        }

        private static IList<DetailField> BuyerFields(NormalizedInvoice i)
        {
            var list = new List<DetailField>();
            var id = !string.IsNullOrEmpty(i.BuyerTaxCode) ? i.BuyerTaxCode : i.BuyerIdNumber;
            list.Add(new DetailField(!string.IsNullOrEmpty(i.BuyerTaxCode) || string.IsNullOrEmpty(i.BuyerIdNumber) ? "MST người mua" : "CCCD người mua", string.IsNullOrEmpty(id) ? "—" : id));
            list.Add(new DetailField("Tên người mua", string.IsNullOrEmpty(i.BuyerName) ? "—" : i.BuyerName));
            list.Add(new DetailField("Địa chỉ", string.IsNullOrEmpty(i.BuyerAddress) ? "—" : i.BuyerAddress));
            return list;
        }

        private static DetailVersionRow VersionRow(InvoiceVersionDetail v)
        {
            return new DetailVersionRow
            {
                VersionNo = v.VersionNo,
                Name = "v" + v.VersionNo,
                Date = DateTimeText(v.CreatedAtUtc),
                Session = v.SessionCode == null ? "Không rõ phiên" : "Phiên " + v.SessionCode,
                Status = v.FileState == VersionFileState.Missing ? "Thiếu file" : v.IsCurrent ? "Hiện hành" : "Bản cũ",
                Problem = v.FileState == VersionFileState.Missing
            };
        }

        private static IList<DetailField> SourceFields(InvoiceDetail d)
        {
            var list = new List<DetailField>();
            var source = d.Current ?? d.Versions.FirstOrDefault();
            if (source != null && source.SessionCode != null)
            {
                list.Add(new DetailField("Phiên", source.SessionCode));
                list.Add(new DetailField("Kỳ tải", source.SessionPeriod));
            }
            else
            {
                list.Add(new DetailField("Phiên", "Không còn thông tin phiên"));
            }

            list.Add(new DetailField("Lần thấy đầu", DateTimeText(d.FirstSeenAtUtc)));
            list.Add(new DetailField("Lần thấy cuối", DateTimeText(d.LastSeenAtUtc)));
            return list;
        }

        private static string Date(DateTime value)
        {
            return value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        }

        private static string DateTimeText(DateTime utc)
        {
            return utc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
        }

        private static string Money(decimal? value, string currency)
        {
            if (!value.HasValue) return "—";
            return value.Value.ToString("N0", Vi) + " " + (string.IsNullOrEmpty(currency) ? "VND" : currency);
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
