using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Domain.PublicService;

namespace HoaDonDienTu.Application.PublicService
{
    /// <summary>
    /// Bộ điều khiển tìm/phân trang/chi tiết của màn hình TTHC, không phụ thuộc giao diện (DD-DVC-02 mục 4).
    /// Giữ bộ lọc ĐÃ ÁP DỤNG lúc bấm Tìm nên đổi trang không bị lẫn với ô nhập đang sửa dở; mỗi lần chỉ một yêu cầu.
    /// </summary>
    public sealed class TthcSearchSession
    {
        private readonly TthcService _service;
        private readonly int _pageDelayMs;
        private TthcQuery _applied;
        private bool _busy;

        /// <param name="pageDelayMs">Nghỉ giữa các trang khi xuất tất cả các trang, để không dồn dập yêu cầu lên cổng (mặc định 300 ms).</param>
        public TthcSearchSession(TthcService service, int pageDelayMs = 300)
        {
            if (service == null) throw new ArgumentNullException("service");
            _service = service;
            _pageDelayMs = Math.Max(0, pageDelayMs);
        }

        /// <summary>Bộ lọc đã áp dụng lúc bấm Tìm (bản sao, không có số trang); null nếu chưa tìm.</summary>
        public TthcQuery AppliedFilters
        {
            get
            {
                if (_applied == null) return null;
                TthcQuery copy = _applied.Clone();
                copy.PageNumber = 0;
                return copy;
            }
        }

        public TthcPage Page { get; private set; }

        public TthcDetail Detail { get; private set; }

        public TthcFilterOptions Options { get; private set; }

        /// <summary>Lỗi của thao tác gần nhất, None nếu thành công.</summary>
        public TthcFailure LastFailure { get; private set; }

        public bool IsBusy
        {
            get { return _busy; }
        }

        public async Task LoadOptionsAsync(CancellationToken cancellationToken)
        {
            if (_busy) return;
            _busy = true;
            try
            {
                Options = await _service.GetFilterOptionsAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (TthcServiceException)
            {
                // Ô chọn để trống; vẫn tìm được bằng từ khóa (DD-DVC-02 mục 4.1).
                Options = TthcFilterOptions.Empty;
            }
            finally
            {
                _busy = false;
            }
        }

        /// <summary>Bấm Tìm: áp dụng bộ lọc mới, về trang 1. Trả false nếu đang bận.</summary>
        public Task<bool> SearchAsync(TthcQuery filters, CancellationToken cancellationToken)
        {
            if (filters == null) throw new ArgumentNullException("filters");
            var query = filters.Clone();
            query.PageNumber = 1;
            return RunSearchAsync(query, true, cancellationToken);
        }

        /// <summary>Đổi trang theo bộ lọc đã áp dụng. Trả false nếu bận, chưa tìm, hoặc trang ngoài khoảng.</summary>
        public Task<bool> GoToPageAsync(int pageNumber, CancellationToken cancellationToken)
        {
            if (_applied == null || Page == null || pageNumber < 1 || pageNumber > Math.Max(1, Page.PageCount))
            {
                return Task.FromResult(false);
            }
            var query = _applied.Clone();
            query.PageNumber = pageNumber;
            return RunSearchAsync(query, false, cancellationToken);
        }

        private async Task<bool> RunSearchAsync(TthcQuery query, bool replaceApplied, CancellationToken cancellationToken)
        {
            if (_busy) return false;
            _busy = true;
            try
            {
                Page = await _service.SearchAsync(query, cancellationToken).ConfigureAwait(false);
                if (replaceApplied || _applied == null) _applied = query;
                else _applied.PageNumber = query.PageNumber;
                LastFailure = TthcFailure.None;
                return true;
            }
            catch (TthcServiceException ex)
            {
                LastFailure = ex.Failure;
                return false;
            }
            finally
            {
                _busy = false;
            }
        }

        /// <summary>
        /// Tải TẤT CẢ các trang của bộ lọc đã áp dụng, tuần tự và có nghỉ giữa các trang (BR-DVC-05), để xuất Excel. Không đổi <see cref="Page"/> đang hiển thị.
        /// Trả null nếu bận, chưa tìm, hoặc bất kỳ trang nào lỗi (không trả kết quả dở dang; xem <see cref="LastFailure"/>).
        /// </summary>
        public async Task<IList<TthcSummary>> FetchAllPagesAsync(IProgress<int> progress, CancellationToken cancellationToken)
        {
            if (_busy || _applied == null || Page == null) return null;
            _busy = true;
            try
            {
                int pageCount = Math.Max(1, Page.PageCount);
                var all = new List<TthcSummary>();
                for (int number = 1; number <= pageCount; number++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (number > 1 && _pageDelayMs > 0) await Task.Delay(_pageDelayMs, cancellationToken).ConfigureAwait(false);
                    TthcQuery query = _applied.Clone();
                    query.PageNumber = number;
                    TthcPage page = await _service.SearchAsync(query, cancellationToken).ConfigureAwait(false);
                    all.AddRange(page.Items);
                    if (progress != null) progress.Report(number);
                }
                LastFailure = TthcFailure.None;
                return all;
            }
            catch (TthcServiceException ex)
            {
                LastFailure = ex.Failure;
                return null;
            }
            finally
            {
                _busy = false;
            }
        }

        public async Task<bool> OpenDetailAsync(string code, CancellationToken cancellationToken)
        {
            if (_busy) return false;
            _busy = true;
            try
            {
                Detail = await _service.GetDetailAsync(code, cancellationToken).ConfigureAwait(false);
                LastFailure = TthcFailure.None;
                return true;
            }
            catch (TthcServiceException ex)
            {
                LastFailure = ex.Failure;
                return false;
            }
            finally
            {
                _busy = false;
            }
        }

        /// <summary>Quay lại danh sách; bộ lọc và trang được giữ nguyên.</summary>
        public void CloseDetail()
        {
            Detail = null;
        }
    }
}
