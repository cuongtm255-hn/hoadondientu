using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Common;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Invoices;
using HoaDonDienTu.Application.Portal;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.Domain.Invoices;

namespace HoaDonDienTu.WinForms.Archive
{
    /// <summary>
    /// Lưu bộ lọc, sắp xếp và trang của SCR-05 trong phiên để quay lại từ màn hình khác vẫn thấy đúng chỗ (NFR-USE-004).
    /// Mỗi doanh nghiệp một trạng thái; đổi MST thì bắt đầu lại.
    /// </summary>
    public sealed class ArchiveViewStateStore
    {
        private Guid _companyId;
        private ArchiveFilterInput _filter;
        private ArchiveQuery _query;

        public bool TryGet(CompanyId company, out ArchiveFilterInput filter, out ArchiveQuery query)
        {
            if (_filter != null && _companyId == company.Value)
            {
                filter = _filter;
                query = _query.Clone();
                return true;
            }

            filter = null;
            query = null;
            return false;
        }

        public void Save(CompanyId company, ArchiveFilterInput filter, ArchiveQuery query)
        {
            _companyId = company.Value;
            _filter = filter;
            _query = query.Clone();
        }
    }

    /// <summary>
    /// SCR-05 Kho hóa đơn (DD-07): tra cứu dữ liệu đã lưu của MST đang mở theo chiều, năm, trạng thái, kết quả kiểm tra, tình trạng file và ô tìm nhanh,
    /// kèm tóm tắt tập kết quả. Hoạt động ở mọi chế độ, kể cả ReadOnlyService (chỉ đọc).
    /// </summary>
    public sealed class ArchivePresenter : IDisposable
    {
        private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

        private readonly IArchiveView _view;
        private readonly IInvoiceArchiveService _service;
        private readonly ICompanyContextAccessor _context;
        private readonly ArchiveViewStateStore _store;
        private readonly IUiOperationRunner _runner;
        private readonly IUserErrorPresenter _errors;
        private readonly DateTime _today;
        private ArchiveQuery _query = new ArchiveQuery();
        private InvoiceDirection? _yearsFor;
        private bool _initialized;
        private int _requestSequence;

        public ArchivePresenter(
            IArchiveView view,
            IInvoiceArchiveService service,
            ICompanyContextAccessor context,
            ArchiveViewStateStore store,
            IUiOperationRunner runner,
            IUserErrorPresenter errors,
            DateTime today)
        {
            if (view == null) throw new ArgumentNullException("view");
            if (service == null) throw new ArgumentNullException("service");
            if (context == null) throw new ArgumentNullException("context");
            if (store == null) throw new ArgumentNullException("store");
            if (runner == null) throw new ArgumentNullException("runner");
            if (errors == null) throw new ArgumentNullException("errors");
            _view = view;
            _service = service;
            _context = context;
            _store = store;
            _runner = runner;
            _errors = errors;
            _today = today;
        }

        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            _view.FilterChanged += OnFilterChanged;
            _view.ClearRequested += OnClear;
            _view.CheckFilesRequested += OnCheckFiles;
            _view.PageRequested += OnPage;
            _view.SortRequested += OnSort;

            Run("Mở kho hóa đơn", async token =>
            {
                if (!_context.HasCurrent)
                {
                    _view.ShowMessage("Chưa chọn doanh nghiệp. Chọn doanh nghiệp để xem kho hóa đơn.", false);
                    return;
                }

                ArchiveFilterInput filter;
                ArchiveQuery saved;
                if (_store.TryGet(_context.Current.CompanyId, out filter, out saved))
                {
                    _query = saved;
                    await ShowYearsAsync(filter.Direction, filter.Year, token);
                    _view.ShowFilter(filter);
                }
                else
                {
                    filter = new ArchiveFilterInput { Direction = InvoiceDirection.Purchase };
                    await ShowYearsAsync(filter.Direction, null, token);
                    filter.Year = _view.ReadFilter().Year;
                    _view.ShowFilter(filter);
                }

                await RefreshAsync(token);
            });
        }

        public void Dispose()
        {
            if (!_initialized) return;
            _initialized = false;
            _runner.CancelAll();
            _view.FilterChanged -= OnFilterChanged;
            _view.ClearRequested -= OnClear;
            _view.CheckFilesRequested -= OnCheckFiles;
            _view.PageRequested -= OnPage;
            _view.SortRequested -= OnSort;
        }

        // ---- Sự kiện ----

        private void OnFilterChanged(object sender, EventArgs e)
        {
            Run("Lọc kho hóa đơn", async token =>
            {
                var filter = _view.ReadFilter();
                if (_yearsFor != filter.Direction)
                {
                    // Đổi chiều: danh sách năm và trạng thái chọn năm phải tính lại cho chiều mới.
                    await ShowYearsAsync(filter.Direction, null, token);
                }

                _query.PageIndex = 0;
                await RefreshAsync(token);
            });
        }

        private void OnClear(object sender, EventArgs e)
        {
            Run("Xóa lọc", async token =>
            {
                var direction = _view.ReadFilter().Direction;
                await ShowYearsAsync(direction, null, token);
                var year = _view.ReadFilter().Year;
                _view.ShowFilter(new ArchiveFilterInput { Direction = direction, Year = year });
                _query.PageIndex = 0;
                await RefreshAsync(token);
            });
        }

        private void OnCheckFiles(object sender, EventArgs e)
        {
            Run("Kiểm tra file", async token =>
            {
                var page = await QueryAsync(_query, token);
                var problems = page == null ? 0 : page.Summary.ProblemCount;
                await RefreshAsync(token);
                _view.ShowMessage(
                    problems == 0 ? "Đã kiểm tra: mọi file XML đều còn trên đĩa." : "Đã kiểm tra: " + problems + " hóa đơn thiếu file XML. Lọc theo \"Lỗi/thiếu file\" để xem.",
                    problems > 0);
            });
        }

        private void OnPage(object sender, PageRequestedEventArgs e)
        {
            Run("Chuyển trang", async token =>
            {
                _query.PageIndex = Math.Max(0, _query.PageIndex + e.Delta);
                await RefreshAsync(token);
            });
        }

        private void OnSort(object sender, SortRequestedEventArgs e)
        {
            Run("Sắp xếp", async token =>
            {
                if (_query.SortKey == e.Key)
                {
                    _query.SortDescending = !_query.SortDescending;
                }
                else
                {
                    _query.SortKey = e.Key;
                    _query.SortDescending = e.Key == ArchiveSortKey.IssueDate || e.Key == ArchiveSortKey.TotalPayment;
                }

                _query.PageIndex = 0;
                await RefreshAsync(token);
            });
        }

        // ---- Tra cứu và hiển thị ----

        private async Task ShowYearsAsync(InvoiceDirection direction, int? keep, CancellationToken token)
        {
            var years = await _service.YearsAsync(direction, token);
            _yearsFor = direction;
            var selected = keep.HasValue && (keep.Value == 0 || years.Contains(keep.Value)) ? keep.Value : (years.Count > 0 ? years[0] : 0);
            _view.ShowYears(years, selected);
        }

        private async Task RefreshAsync(CancellationToken token)
        {
            var filter = _view.ReadFilter();
            Apply(filter);
            var page = await QueryAsync(_query, token);
            if (page == null) return;

            // Trang bị kéo về trang cuối khi dữ liệu ít đi (ví dụ sau khi lọc): giữ chỉ số thật để nút Trước/Sau đúng.
            _query.PageIndex = page.PageIndex;
            _store.Save(_context.Current.CompanyId, filter, _query);
            _view.ShowResult(Build(page, filter));
            _view.ShowMessage(string.Empty, false);
        }

        private async Task<ArchivePage> QueryAsync(ArchiveQuery query, CancellationToken token)
        {
            if (!_context.HasCurrent)
            {
                _view.ShowMessage("Chưa chọn doanh nghiệp.", true);
                return null;
            }

            var sequence = Interlocked.Increment(ref _requestSequence);
            var page = await _service.QueryAsync(query, token);

            // Gõ nhanh thì nhiều truy vấn chồng nhau: chỉ kết quả của lần gần nhất được hiển thị.
            return sequence == _requestSequence ? page : null;
        }

        private void Apply(ArchiveFilterInput f)
        {
            _query.Direction = f.Direction;
            _query.QuickSearch = Clean(f.QuickSearch);
            _query.StatusCode = Clean(f.StatusCode);
            _query.CheckResultCode = Clean(f.CheckResultCode);
            _query.FileFilter = f.FileFilter;
            if (f.Year > 0)
            {
                _query.IssueFrom = new DateTime(f.Year, 1, 1);
                _query.IssueTo = new DateTime(f.Year, 12, 31);
            }
            else
            {
                _query.IssueFrom = null;
                _query.IssueTo = null;
            }
        }

        private ArchiveViewModel Build(ArchivePage page, ArchiveFilterInput filter)
        {
            var purchase = filter.Direction == InvoiceDirection.Purchase;
            var first = page.TotalCount == 0 ? 0 : page.PageIndex * page.PageSize + 1;
            var last = page.TotalCount == 0 ? 0 : first + page.Rows.Count - 1;
            var scope = purchase ? "mua vào" : "bán ra";
            var year = filter.Year > 0 ? " năm " + filter.Year : string.Empty;

            return new ArchiveViewModel
            {
                CounterpartyIdCaption = purchase ? "MST người bán" : "MST người mua",
                CounterpartyNameCaption = purchase ? "Tên người bán" : "Tên người mua",
                ResultCount = page.TotalCount.ToString("N0", Vi),
                ResultSub = "Hóa đơn " + scope + year + (page.Summary.ProblemCount > 0 ? " · " + page.Summary.ProblemCount + " thiếu file" : string.Empty),
                TotalBeforeTax = Compact(page.Summary.TotalBeforeTax),
                TotalTax = Compact(page.Summary.TotalTax),
                TotalPayment = Compact(page.Summary.TotalPayment),
                FooterText = page.TotalCount == 0 ? "Không có hóa đơn nào khớp." : "Hiển thị " + first.ToString("N0", Vi) + "–" + last.ToString("N0", Vi) + " / " + page.TotalCount.ToString("N0", Vi),
                PageText = "Trang " + (page.PageIndex + 1) + "/" + page.PageCount,
                CanPrevious = page.PageIndex > 0,
                CanNext = page.PageIndex < page.PageCount - 1,
                SortKey = _query.SortKey,
                SortDescending = _query.SortDescending,
                Rows = page.Rows.Select(r => Row(r, purchase)).ToList()
            };
        }

        private static ArchiveRowViewModel Row(ArchiveRow row, bool purchase)
        {
            var i = row.Invoice;
            return new ArchiveRowViewModel
            {
                Number = row.Number.ToString(CultureInfo.InvariantCulture),
                IssueDate = i.IssueDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                CounterpartyId = i.CounterpartyIdentifier ?? string.Empty,
                CounterpartyName = (purchase ? i.SellerName : i.BuyerName) ?? string.Empty,
                FormSymbol = i.FormSymbol ?? string.Empty,
                InvoiceSymbol = i.InvoiceSymbol ?? string.Empty,
                InvoiceNumber = i.InvoiceNumber ?? string.Empty,
                Status = PortalOptions.StatusLabel(i.StatusRaw),
                FileState = row.FileState,
                FileText = FileText(row),
                BeforeTax = Money(i.TotalBeforeTax),
                Tax = Money(i.TotalTax),
                Payment = Money(i.TotalPayment)
            };
        }

        public static string FileText(ArchiveRow row)
        {
            switch (row.FileState)
            {
                case ArchiveFileState.Missing: return "Thiếu XML";
                case ArchiveFileState.MultiVersion: return row.VersionCount + " phiên bản";
                default: return "XML OK";
            }
        }

        public static string Money(decimal? value)
        {
            return value.HasValue ? value.Value.ToString("N0", Vi) : string.Empty;
        }

        /// <summary>Số lớn rút gọn theo kiểu thẻ tóm tắt của wireframe: "12,8 tỷ", "3,5 triệu".</summary>
        public static string Compact(decimal value)
        {
            var abs = Math.Abs(value);
            if (abs >= 1000000000m) return (value / 1000000000m).ToString("0.##", Vi) + " tỷ";
            if (abs >= 1000000m) return (value / 1000000m).ToString("0.##", Vi) + " triệu";
            return value.ToString("N0", Vi);
        }

        private static string Clean(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private async void Run(string name, Func<CancellationToken, Task> operation)
        {
            await _runner.RunAsync(name, operation, state => { if (state.Error != null) _errors.Show(state.Error); });
        }
    }
}
