using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Application.Authentication;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Portal;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.Infrastructure.Portal.Config;

namespace HoaDonDienTu.Infrastructure.Portal
{
    /// <summary>Tạo client API cho từng generation của phiên. <c>handlerFactory</c> cho phép thay HTTP bằng mock khi test/dev.</summary>
    public sealed class PortalApiClientFactory : IPortalApiClientFactory
    {
        private readonly PortalApiConfig _config;
        private readonly Func<HttpMessageHandler> _handlerFactory;

        public PortalApiClientFactory(PortalApiConfig config, Func<HttpMessageHandler> handlerFactory)
        {
            if (config == null) throw new ArgumentNullException("config");
            _config = config;
            _handlerFactory = handlerFactory;
        }

        public IPortalApiClient Create(CompanyContext company, PortalSession session)
        {
            return new PortalHttpApiClient(_config, session.Material, _handlerFactory == null ? null : _handlerFactory());
        }
    }

    /// <summary>
    /// Xác minh phiên đăng nhập từ WebView bằng chính token: claim <c>sub</c> của JWT là MST đăng nhập (sample-api-web.md mục 1) nên so với
    /// MST của kho đang mở mà không cần gọi mạng. Token hết hạn hoặc không đọc được bị từ chối. Mọi lần gọi API sau đó vẫn do cổng kiểm tra lại
    /// token, và 401/403 sẽ đưa phiên tải về trạng thái chờ đăng nhập.
    /// </summary>
    public sealed class PortalAuthenticationService : IPortalAuthenticationService
    {
        private readonly IClock _clock;

        public PortalAuthenticationService(IClock clock)
        {
            if (clock == null) throw new ArgumentNullException("clock");
            _clock = clock;
        }

        public Task<PortalLoginResult> VerifyAsync(CompanyContext company, PortalSessionMaterial candidate, CancellationToken cancellationToken)
        {
            if (company == null) throw new ArgumentNullException("company");
            if (candidate == null) throw new ArgumentNullException("candidate");
            return Task.FromResult(Verify(company, candidate));
        }

        private PortalLoginResult Verify(CompanyContext company, PortalSessionMaterial candidate)
        {
            var token = candidate.Token;
            if (token == null) return PortalLoginResult.Failure(PortalLoginOutcome.InvalidSession, null);
            if (token.IsExpired(_clock.UtcNow)) return PortalLoginResult.Failure(PortalLoginOutcome.InvalidSession, null);

            TaxCode actual;
            if (!TaxCode.TryParse(token.Subject, out actual)) return PortalLoginResult.Failure(PortalLoginOutcome.InvalidSession, null);
            if (!actual.Equals(company.TaxCode)) // Message mang MST của tài khoản vừa đăng nhập để hộp thoại nói rõ (chỉ hiển thị, không ghi nhật ký).
            return PortalLoginResult.Failure(PortalLoginOutcome.TaxCodeMismatch, actual.Value);

            return PortalLoginResult.Success(new PortalSession(company.CompanyId, company.TaxCode, candidate, _clock.UtcNow));
        }
    }
}
