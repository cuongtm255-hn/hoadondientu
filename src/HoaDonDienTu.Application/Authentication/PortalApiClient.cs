using System;
using HoaDonDienTu.Application.Companies;

namespace HoaDonDienTu.Application.Authentication
{
    /// <summary>Client API cổng Thuế cho đúng một generation của phiên (tra cứu, tải XML).</summary>
    public interface IPortalApiClient : HoaDonDienTu.Application.Portal.IPortalInvoiceClient, IDisposable
    {
    }

    public interface IPortalApiClientFactory
    {
        IPortalApiClient Create(CompanyContext company, PortalSession session);
    }

    /// <summary>
    /// Giữ một client cho mỗi generation của phiên. Phiên đổi/xóa thì client + handler cũ bị dispose.
    /// Không tạo client theo từng request.
    /// </summary>
    public sealed class PortalApiClientProvider : IDisposable
    {
        private readonly IPortalSessionStore _sessionStore;
        private readonly ICompanyContextAccessor _context;
        private readonly IPortalApiClientFactory _factory;
        private readonly object _gate = new object();
        private IPortalApiClient _client;
        private int _generation = -1;

        public PortalApiClientProvider(
            IPortalSessionStore sessionStore,
            ICompanyContextAccessor context,
            IPortalApiClientFactory factory)
        {
            if (sessionStore == null) throw new ArgumentNullException("sessionStore");
            if (context == null) throw new ArgumentNullException("context");
            if (factory == null) throw new ArgumentNullException("factory");
            _sessionStore = sessionStore;
            _context = context;
            _factory = factory;
            _sessionStore.SessionChanged += OnSessionChanged;
        }

        /// <exception cref="InvalidOperationException">Chưa có phiên hợp lệ cho MST đang mở.</exception>
        public IPortalApiClient GetClient()
        {
            lock (_gate)
            {
                var snapshot = _sessionStore.GetSnapshot();
                if (snapshot == null) throw new InvalidOperationException("Chưa đăng nhập cổng Thuế.");
                if (!_context.HasCurrent) throw new InvalidOperationException("Chưa có doanh nghiệp nào được mở.");

                var company = _context.Current;
                if (!company.TaxCode.Equals(snapshot.TaxCode) || !company.CompanyId.Equals(snapshot.CompanyId))
                {
                    throw new InvalidOperationException("Phiên cổng Thuế không thuộc doanh nghiệp đang mở.");
                }

                if (_client == null || _generation != snapshot.Generation)
                {
                    DisposeClient();
                    _client = _factory.Create(company, _sessionStore.GetSessionForApi());
                    _generation = snapshot.Generation;
                }

                return _client;
            }
        }

        public void Dispose()
        {
            _sessionStore.SessionChanged -= OnSessionChanged;
            lock (_gate)
            {
                DisposeClient();
            }
        }

        private void OnSessionChanged(object sender, EventArgs e)
        {
            lock (_gate)
            {
                DisposeClient();
            }
        }

        private void DisposeClient()
        {
            var client = _client;
            _client = null;
            _generation = -1;
            if (client != null) client.Dispose();
        }
    }
}
