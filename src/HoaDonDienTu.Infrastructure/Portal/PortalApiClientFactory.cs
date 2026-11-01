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
    /// Xác minh phiên bằng API hồ sơ của cổng và đối chiếu MST với doanh nghiệp đang mở (FR-AUTH-011).
    /// Điều hướng khỏi trang đăng nhập không đủ để coi là thành công.
    /// </summary>
    public sealed class PortalAuthenticationService : IPortalAuthenticationService
    {
        private readonly PortalApiConfig _config;
        private readonly IClock _clock;
        private readonly Func<HttpMessageHandler> _handlerFactory;

        public PortalAuthenticationService(PortalApiConfig config, IClock clock, Func<HttpMessageHandler> handlerFactory)
        {
            if (config == null) throw new ArgumentNullException("config");
            if (clock == null) throw new ArgumentNullException("clock");
            _config = config;
            _clock = clock;
            _handlerFactory = handlerFactory;
        }

        public async Task<PortalLoginResult> VerifyAsync(CompanyContext company, PortalSessionMaterial candidate, CancellationToken cancellationToken)
        {
            if (company == null) throw new ArgumentNullException("company");
            if (candidate == null) throw new ArgumentNullException("candidate");

            string profileTaxCode;
            using (var client = new PortalHttpApiClient(_config, candidate, _handlerFactory == null ? null : _handlerFactory()))
            {
                try
                {
                    profileTaxCode = await client.GetProfileTaxCodeAsync(cancellationToken).ConfigureAwait(true);
                }
                catch (PortalApiException ex)
                {
                    return PortalLoginResult.Failure(OutcomeOf(ex.Kind), null);
                }
            }

            TaxCode actual;
            if (!TaxCode.TryParse(profileTaxCode, out actual))
            {
                return PortalLoginResult.Failure(PortalLoginOutcome.InvalidSession, null);
            }

            if (!actual.Equals(company.TaxCode))
            {
                return PortalLoginResult.Failure(PortalLoginOutcome.TaxCodeMismatch, null);
            }

            return PortalLoginResult.Success(new PortalSession(company.CompanyId, company.TaxCode, candidate, _clock.UtcNow));
        }

        private static PortalLoginOutcome OutcomeOf(PortalErrorKind kind)
        {
            switch (kind)
            {
                case PortalErrorKind.AuthExpired:
                case PortalErrorKind.SchemaChanged:
                    return PortalLoginOutcome.InvalidSession;
                default:
                    return PortalLoginOutcome.PortalUnavailable;
            }
        }
    }
}
