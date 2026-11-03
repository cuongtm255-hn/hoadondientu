using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Common;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Exports;
using HoaDonDienTu.Application.Licensing;
using HoaDonDienTu.Domain.Licensing;
using HoaDonDienTu.WinForms.Common;
using HoaDonDienTu.Application.Invoices;
using HoaDonDienTu.Application.Navigation;
using HoaDonDienTu.Application.Portal;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.Domain.Invoices;
using HoaDonDienTu.WinForms.Detail;

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

        private Guid _columnsCompany;
        private IList<string> _hidden = new List<string>();

        /// <summary>Cột đang ẩn của doanh nghiệp; rỗng nếu chưa từng chỉnh.</summary>
        public IList<string> HiddenColumns(CompanyId company)
        {
            return _columnsCompany == company.Value ? new List<string>(_hidden) : new List<string>();
        }

        public void SaveHiddenColumns(CompanyId company, IList<string> hidden)
        {
            _columnsCompany = company.Value;
            _hidden = new List<string>(hidden);
        }

        public void Save(CompanyId company, ArchiveFilterInput filter, ArchiveQuery query)
        {
            _companyId = company.Value;
            _filter = filter;
            _query = query.Clone();
        }
    }

    /// <summary>Phần phụ thuộc cho "Xuất Excel/ZIP" của SCR-05; không truyền thì hai nút xuất bị khóa.</summary>
    public sealed class ArchiveExportContext
    {
        public ArchiveExportContext(IInvoiceExportService service, IExportSavePicker picker, IConfirmationService confirmation, IEntitlementService entitlement)
        {
            if (service == null) throw new ArgumentNullException("service");
            if (picker == null) throw new ArgumentNullException("picker");
            if (confirmation == null) throw new ArgumentNullException("confirmation");
            if (entitlement == null) throw new ArgumentNullException("entitlement");
            Service = service;
            Picker = picker;
            Confirmation = confirmation;
            Entitlement = entitlement;
        }

        public IInvoiceExportService Service { get; private set; }
        public IExportSavePicker Picker { get; private set; }
        public IConfirmationService Confirmation { get; private set; }
        public IEntitlementService Entitlement { get; private set; }
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
        private readonly INavigationService _navigation;
        private IList<Guid> _selection = new List<Guid>();
        private readonly ArchiveViewStateStore _store;
        private readonly IUiOperationRunner _runner;
        private readonly IUserErrorPresenter _errors;
        private readonly DateTime _today;
        private readonly ArchiveNavigationRequest _request;
        private readonly ArchiveExportContext _export;
        private readonly ArchiveIntegrityContext _integrity;
        private ArchiveQuery _query = new ArchiveQuery();
        private InvoiceDirection? _yearsFor;
        private bool _initialized;
        private bool _disposed;

        // Runner bỏ qua thao tác cùng tên đang chạy, nên mọi thay đổi bộ lọc/trang/sắp xếp được gom vào một vòng làm mới tuần tự:
        // thay đổi đến khi đang tải sẽ được áp dụng ngay sau lần tải hiện tại, không bị mất.
        private bool _refreshing;
        private bool _dirty;
        private bool _resetPage;
        private bool _clearRequested;

        public ArchivePresenter(
            IArchiveView view,
            IInvoiceArchiveService service,
            ICompanyContextAccessor context,
            INavigationService navigation,
            ArchiveViewStateStore store,
            IUiOperationRunner runner,
            IUserErrorPresenter errors,
            DateTime today,
            ArchiveNavigationRequest request = null,
            ArchiveExportContext export = null,
            ArchiveIntegrityContext integrity = null)
        {
            if (view == null) throw new ArgumentNullException("view");
            if (service == null) throw new ArgumentNullException("service");
            if (context == null) throw new ArgumentNullException("context");
            if (navigation == null) throw new ArgumentNullException("navigation");
            if (store == null) throw new ArgumentNullException("store");
            if (runner == null) throw new ArgumentNullException("runner");
            if (errors == null) throw new ArgumentNullException("errors");
            _view = view;
            _service = service;
            _context = context;
            _navigation = navigation;
            _store = store;
            _runner = runner;
            _errors = errors;
            _today = today;
            _request = request;
            _export = export;
            _integrity = integrity;
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
            _view.InvoiceRequested += OnInvoice;
            _view.SelectionChanged += OnSelection;
            _view.ColumnsChanged += OnColumns;
            _view.ExportExcelRequested += OnExportExcel;
            _view.ExportZipRequested += OnExportZip;
            _view.ShowExportAllowed(_export != null && _export.Entitlement.IsAllowed(AppFeature.ReadExport));

            _refreshing = true;
            Run("Mở kho hóa đơn", async token =>
            {
                try
                {
                    if (!_context.HasCurrent)
                    {
                        _view.ShowMessage("Chưa chọn doanh nghiệp. Chọn doanh nghiệp để xem kho hóa đơn.", false);
                        return;
                    }

                    _view.ShowHiddenColumns(_store.HiddenColumns(_context.Current.CompanyId));

                    ArchiveFilterInput filter;
                    ArchiveQuery saved;
                    if (_request != null)
                    {
                        // Mở từ màn hình khác với bộ lọc cho sẵn: bắt đầu mới, xem mọi năm của chiều được yêu cầu.
                        _query = new ArchiveQuery();
                        filter = new ArchiveFilterInput { Direction = _request.Direction, FileFilter = _request.FileFilter };
                        await ShowYearsAsync(filter.Direction, 0, token);
                        _view.ShowFilter(filter);
                    }
                    else if (_store.TryGet(_context.Current.CompanyId, out filter, out saved))
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

                    _dirty = true;
                    await DrainAsync(token);
                }
                finally
                {
                    _refreshing = false;
                }
            });
        }

        public void Dispose()
        {
            if (!_initialized) return;
            _initialized = false;
            _disposed = true;
            _view.FilterChanged -= OnFilterChanged;
            _view.ClearRequested -= OnClear;
            _view.CheckFilesRequested -= OnCheckFiles;
            _view.PageRequested -= OnPage;
            _view.SortRequested -= OnSort;
            _view.InvoiceRequested -= OnInvoice;
            _view.SelectionChanged -= OnSelection;
            _view.ColumnsChanged -= OnColumns;
            _view.ExportExcelRequested -= OnExportExcel;
            _view.ExportZipRequested -= OnExportZip;
        }

        // ---- Sự kiện ----

        private void OnFilterChanged(object sender, EventArgs e)
        {
            _resetPage = true;
            Refresh();
        }

        private void OnClear(object sender, EventArgs e)
        {
            _clearRequested = true;
            _resetPage = true;
            Refresh();
        }

        private void OnPage(object sender, PageRequestedEventArgs e)
        {
            _query.PageIndex = Math.Max(0, _query.PageIndex + e.Delta);
            Refresh();
        }

        private void OnSort(object sender, SortRequestedEventArgs e)
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

            _resetPage = true;
            Refresh();
        }

        /// <summary>Kết quả mới thay hết các dòng trên lưới nên lựa chọn cũ không còn hiệu lực.</summary>
        private void ResetSelection()
        {
            _selection = new List<Guid>();
            _view.ShowSelectionCount(0);
        }

        private void OnSelection(object sender, SelectionChangedEventArgs e)
        {
            _selection = e.InvoiceIds ?? new List<Guid>();
            _view.ShowSelectionCount(_selection.Count);
        }

        private void OnColumns(object sender, EventArgs e)
        {
            if (_context.HasCurrent) _store.SaveHiddenColumns(_context.Current.CompanyId, _view.HiddenColumns);
        }

        private void OnInvoice(object sender, InvoiceRequestedEventArgs e)
        {
            // Bộ lọc, sắp xếp và trang đã được lưu sau mỗi lần làm mới nên quay lại từ SCR-06 thấy đúng chỗ cũ.
            Run("Mở chi tiết hóa đơn", token => _navigation.NavigateAsync(ScreenId.InvoiceDetail, new InvoiceDetailRequest(e.InvoiceId), token));
        }

        // ---- Xuất Excel / ZIP XML (DD-09) ----

        private void OnExportExcel(object sender, EventArgs e)
        {
            Export("Xuất Excel", "DanhSachHoaDon", "xlsx", "Bảng kê Excel", (scope, path, progress, token) =>
                _export.Service.ExportExcelAsync(new ExportRequest { Scope = scope, TargetPath = path, Overwrite = true }, progress, token));
        }

        private void OnExportZip(object sender, EventArgs e)
        {
            Export("Xuất ZIP XML", "XML", "zip", "Gói ZIP XML", async (scope, path, progress, token) =>
            {
                var request = new ExportRequest { Scope = scope, TargetPath = path, Overwrite = true };
                try
                {
                    return await _export.Service.ExportZipAsync(request, ZipFailurePolicy.Ask, progress, token);
                }
                catch (ExportNeedsDecisionException ex)
                {
                    // Chưa ghi gì ra đĩa: để người dùng chọn xuất phần hợp lệ hay hủy (FR-EXPORT-006).
                    var sample = string.Join(", ", ex.Problems.Take(3).Select(p => p.InvoiceNumber));
                    var message = ex.Problems.Count + " hóa đơn thiếu hoặc lỗi file XML (số " + sample + (ex.Problems.Count > 3 ? ", ..." : string.Empty) + ").\r\n\r\n"
                        + "Xuất " + ex.ValidCount + " hóa đơn hợp lệ và ghi các hóa đơn bị bỏ qua vào manifest.csv?";
                    if (!_export.Confirmation.Confirm("Xuất phần hợp lệ", message)) return null;
                    return await _export.Service.ExportZipAsync(request, ZipFailurePolicy.SkipAndRecord, progress, token);
                }
            });
        }

        private void Export(
            string name,
            string kind,
            string extension,
            string description,
            Func<ExportScope, string, IProgress<ExportProgress>, CancellationToken, Task<ExportResult>> run)
        {
            if (_export == null) return;
            Run(name, async token =>
            {
                if (!_context.HasCurrent)
                {
                    _view.ShowMessage("Chưa chọn doanh nghiệp.", true);
                    return;
                }

                var company = _context.Current;
                Apply(_view.ReadFilter());

                // Có dòng chọn thì chỉ xuất các dòng đó theo thứ tự đang hiển thị; không thì xuất toàn bộ kết quả đang lọc.
                var selected = _selection.ToList();
                var scope = selected.Count > 0 ? ExportScope.ForSelection(selected) : ExportScope.ForQuery(_query);

                var folder = System.IO.Path.Combine(company.StorageRoot, "Exports");
                try
                {
                    System.IO.Directory.CreateDirectory(folder);
                }
                catch (System.IO.IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }

                // Hộp thoại nêu rõ doanh nghiệp bị tác động (NFR-USE-007).
                var title = name + " — " + company.DisplayName + " (MST " + company.TaxCode.Value + ")";
                var suggested = ExportFileNamer.FileName(kind, ExportFileNamer.ScopeLabel(scope), DateTime.Now, extension);
                var path = _export.Picker.Pick(suggested, extension, description, title, folder);
                if (path == null) return;

                var progress = new Progress<ExportProgress>(p => _view.ShowMessage("Đang xuất " + p.Done.ToString("N0", Vi) + "/" + p.Total.ToString("N0", Vi) + " hóa đơn...", false));
                try
                {
                    var result = await run(scope, path, progress, token);
                    if (_disposed) return;
                    if (result == null)
                    {
                        _view.ShowMessage("Đã hủy xuất.", false);
                        return;
                    }

                    var skipped = result.Skipped == null ? 0 : result.Skipped.Count;
                    _view.ShowMessage(
                        "Đã xuất " + result.Exported.ToString("N0", Vi) + " hóa đơn ra " + result.Path
                            + (skipped > 0 ? ". Bỏ qua " + skipped + " hóa đơn thiếu/lỗi file (xem manifest.csv trong gói)." : "."),
                        skipped > 0);
                }
                catch (InvoiceStorageException ex)
                {
                    _view.ShowMessage(ex.Message, true);
                }
            });
        }

        private void OnCheckFiles(object sender, EventArgs e)
        {
            Run("Kiểm tra file", async token =>
            {
                if (!_context.HasCurrent)
                {
                    _view.ShowMessage("Chưa chọn doanh nghiệp.", true);
                    return;
                }

                var company = _context.Current.CompanyId;
                var filter = _view.ReadFilter();
                Apply(filter);
                var query = _query.Clone();
                query.RefreshFiles = true;
                var page = await _service.QueryAsync(query, token);
                if (_disposed) return;

                // Kiểm tra lại đĩa xong thì hiển thị luôn kết quả mới, kèm kết luận.
                _query.PageIndex = page.PageIndex;
                _store.Save(company, filter, _query);
                _view.ShowResult(Build(page, filter));
                ResetSelection();
                var problems = page.Summary.ProblemCount - page.Summary.DamagedCount;
                var existence = problems == 0 ? "Đã kiểm tra: mọi file XML đều còn trên đĩa." : "Đã kiểm tra: " + problems + " hóa đơn thiếu file XML. Lọc theo \"Lỗi/thiếu file\" để xem.";
                _view.ShowMessage(existence, problems > 0);

                if (_integrity == null) return;

                // Kiểm tra sâu: đọc từng file, so SHA-256 và tìm file lạ; chạy lâu với kho lớn nên báo số file đã kiểm và hủy được.
                var progress = new Progress<int>(done => { if (!_disposed) _view.ShowMessage("Đang kiểm tra nội dung file… " + done.ToString("N0", Vi) + " phiên bản đã kiểm.", false); });
                var report = await _integrity.Checker.CheckAsync(progress, token);
                if (_disposed) return;

                // Kết quả đã được ghi vào kho: nạp lại trang để cột Tình trạng file hiện luôn các file lỗi nội dung.
                var after = await _service.QueryAsync(_query.Clone(), token);
                if (_disposed) return;
                _view.ShowResult(Build(after, filter));
                ResetSelection();

                _view.ShowMessage(IntegrityReportText.Summary(report), !report.IsClean);
                if (!report.IsClean) _integrity.Dialog.Show(report);
            });
        }

        // ---- Tra cứu và hiển thị ----

        private void Refresh()
        {
            _dirty = true;
            if (_refreshing) return;
            _refreshing = true;
            Run("Làm mới kho hóa đơn", async token =>
            {
                try
                {
                    await DrainAsync(token);
                }
                finally
                {
                    _refreshing = false;
                }
            });
        }

        private async Task DrainAsync(CancellationToken token)
        {
            while (_dirty && !_disposed)
            {
                _dirty = false;
                await StepAsync(token);
            }
        }

        private async Task StepAsync(CancellationToken token)
        {
            if (!_context.HasCurrent)
            {
                _view.ShowMessage("Chưa chọn doanh nghiệp.", true);
                return;
            }

            if (_clearRequested)
            {
                _clearRequested = false;
                var direction = _view.ReadFilter().Direction;
                await ShowYearsAsync(direction, null, token);
                var year = _view.ReadFilter().Year;
                _view.ShowFilter(new ArchiveFilterInput { Direction = direction, Year = year });
            }

            var filter = _view.ReadFilter();
            if (_yearsFor != filter.Direction)
            {
                // Đổi chiều: danh sách năm và trạng thái chọn năm phải tính lại cho chiều mới.
                await ShowYearsAsync(filter.Direction, null, token);
                _resetPage = true;
            }

            if (_resetPage)
            {
                _query.PageIndex = 0;
                _resetPage = false;
            }

            await RefreshAsync(token);
        }

        private async Task ShowYearsAsync(InvoiceDirection direction, int? keep, CancellationToken token)
        {
            var years = await _service.YearsAsync(direction, token);
            _yearsFor = direction;
            var selected = keep.HasValue && (keep.Value == 0 || years.Contains(keep.Value)) ? keep.Value : (years.Count > 0 ? years[0] : 0);
            _view.ShowYears(years, selected);
        }

        private async Task RefreshAsync(CancellationToken token)
        {
            var company = _context.Current.CompanyId;
            var filter = _view.ReadFilter();
            Apply(filter);
            var page = await _service.QueryAsync(_query, token);
            if (_disposed) return;

            // Trang bị kéo về trang cuối khi dữ liệu ít đi (ví dụ sau khi lọc): giữ chỉ số thật để nút Trước/Sau đúng.
            _query.PageIndex = page.PageIndex;
            _store.Save(company, filter, _query);
            _view.ShowResult(Build(page, filter));
            ResetSelection();
            _view.ShowMessage(string.Empty, false);
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
                ResultSub = "Hóa đơn " + scope + year + (page.Summary.ProblemCount - page.Summary.DamagedCount > 0 ? " · " + (page.Summary.ProblemCount - page.Summary.DamagedCount) + " thiếu file" : string.Empty)
                    + (page.Summary.DamagedCount > 0 ? " · " + page.Summary.DamagedCount + " lỗi nội dung" : string.Empty),
                TotalBeforeTax = Compact(page.Summary.TotalBeforeTax),
                TotalTax = Compact(page.Summary.TotalTax),
                TotalPayment = Compact(page.Summary.TotalPayment),
                FooterText = (page.TotalCount == 0 ? "Không có hóa đơn nào khớp." : "Hiển thị " + first.ToString("N0", Vi) + "–" + last.ToString("N0", Vi) + " / " + page.TotalCount.ToString("N0", Vi)) + LastCheckText(page.Summary),
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
                InvoiceId = row.InvoiceId,
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

        private static string LastCheckText(ArchiveSummary summary)
        {
            return summary.LastFileCheckUtc.HasValue
                ? " · Kiểm tra file lần cuối: " + summary.LastFileCheckUtc.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm", Vi)
                : string.Empty;
        }

        public static string FileText(ArchiveRow row)
        {
            switch (row.FileState)
            {
                case ArchiveFileState.Missing: return "Thiếu XML";
                case ArchiveFileState.Damaged: return "XML lỗi";
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
