using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Application.Authentication;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Licensing;
using HoaDonDienTu.Domain.Licensing;

namespace HoaDonDienTu.WinForms.Login
{
    public interface IPortalLoginCoordinator
    {
        Task<PortalLoginResult> LoginAsync(PortalLoginRequest request, IWin32Window owner, CancellationToken cancellationToken);
    }

    /// <summary>Chỉ cho phép một form đăng nhập tại một thời điểm và chỉ khi đủ điều kiện (DD-01 mục 10.2).</summary>
    public sealed class PortalLoginCoordinator : IPortalLoginCoordinator
    {
        private readonly ICompanyContextAccessor _context;
        private readonly IEntitlementService _entitlement;
        private readonly IPortalSessionStore _sessionStore;
        private readonly IPortalAuthenticationService _authentication;
        private readonly Func<IPortalLoginBrowser> _browserFactory;
        private readonly IApplicationLogger _logger;
        private int _active;

        public PortalLoginCoordinator(
            ICompanyContextAccessor context,
            IEntitlementService entitlement,
            IPortalSessionStore sessionStore,
            IPortalAuthenticationService authentication,
            Func<IPortalLoginBrowser> browserFactory,
            IApplicationLogger logger)
        {
            if (context == null) throw new ArgumentNullException("context");
            if (entitlement == null) throw new ArgumentNullException("entitlement");
            if (sessionStore == null) throw new ArgumentNullException("sessionStore");
            if (authentication == null) throw new ArgumentNullException("authentication");
            if (browserFactory == null) throw new ArgumentNullException("browserFactory");
            if (logger == null) throw new ArgumentNullException("logger");
            _context = context;
            _entitlement = entitlement;
            _sessionStore = sessionStore;
            _authentication = authentication;
            _browserFactory = browserFactory;
            _logger = logger;
        }

        public Task<PortalLoginResult> LoginAsync(PortalLoginRequest request, IWin32Window owner, CancellationToken cancellationToken)
        {
            if (request == null) throw new ArgumentNullException("request");

            if (Interlocked.CompareExchange(ref _active, 1, 0) != 0)
            {
                return Task.FromResult(PortalLoginResult.Failure(PortalLoginOutcome.AlreadyInProgress, "Đang có cửa sổ đăng nhập khác."));
            }

            try
            {
                if (!_context.HasCurrent
                    || !_context.Current.CompanyId.Equals(request.CompanyId)
                    || !_context.Current.TaxCode.Equals(request.ExpectedTaxCode))
                {
                    return Task.FromResult(PortalLoginResult.Failure(PortalLoginOutcome.NotAllowed, "Chưa có doanh nghiệp phù hợp để đăng nhập."));
                }

                if (!_entitlement.IsAllowed(AppFeature.PortalLogin))
                {
                    return Task.FromResult(PortalLoginResult.Failure(PortalLoginOutcome.NotAllowed, "Chế độ hiện tại không cho phép đăng nhập cổng Thuế."));
                }

                using (var form = new PortalLoginForm())
                using (var presenter = new PortalLoginPresenter(
                    form, request, _context, _browserFactory(), _authentication, _sessionStore, _logger))
                {
                    presenter.Initialize();
                    form.ShowDialog(owner);
                    var result = presenter.Result
                        ?? PortalLoginResult.Failure(PortalLoginOutcome.Cancelled, null);
                    return Task.FromResult(result);
                }
            }
            finally
            {
                Interlocked.Exchange(ref _active, 0);
            }
        }
    }
}
