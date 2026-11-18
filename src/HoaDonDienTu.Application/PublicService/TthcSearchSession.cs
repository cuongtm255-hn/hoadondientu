using System;
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
        private TthcQuery _applied;
        private bool _busy;

        public TthcSearchSession(TthcService service)
        {
            if (service == null) throw new ArgumentNullException("service");
            _service = service;
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
