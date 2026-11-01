#if DEBUG
using System;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Application.Authentication;
using HoaDonDienTu.Application.Companies;

namespace HoaDonDienTu.Infrastructure.Development
{
    /// <summary>Xác minh giả: trả kết quả cấu hình sẵn, không gọi mạng. Chỉ có ở Debug; thay bằng adapter thật sau G-02 (T01-16).</summary>
    public sealed class FakePortalAuthenticationService : IPortalAuthenticationService
    {
        private readonly PortalLoginOutcome _outcome;
        private readonly IClock _clock;

        public FakePortalAuthenticationService(PortalLoginOutcome outcome, IClock clock)
        {
            _outcome = outcome;
            _clock = clock;
        }

        public Task<PortalLoginResult> VerifyAsync(CompanyContext company, PortalSessionMaterial candidate, CancellationToken cancellationToken)
        {
            if (_outcome != PortalLoginOutcome.Success)
            {
                return Task.FromResult(PortalLoginResult.Failure(_outcome, "Kết quả giả lập: " + _outcome));
            }

            var session = new PortalSession(company.CompanyId, company.TaxCode, candidate, _clock.UtcNow);
            return Task.FromResult(PortalLoginResult.Success(session));
        }
    }

    public sealed class StubPortalApiClientFactory : IPortalApiClientFactory
    {
        public IPortalApiClient Create(CompanyContext company, PortalSession session)
        {
            return new StubPortalApiClient();
        }

        private sealed class StubPortalApiClient : IPortalApiClient
        {
            public void Dispose()
            {
            }
        }
    }
}
#endif
