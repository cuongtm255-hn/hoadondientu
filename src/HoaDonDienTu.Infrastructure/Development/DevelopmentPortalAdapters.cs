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
        /// <summary>Header do trình duyệt giả đặt vào material để mô phỏng MST của tài khoản vừa đăng nhập.</summary>
        public const string DevTaxCodeHeader = "x-dev-tax-code";

        private readonly PortalLoginOutcome? _forcedOutcome;
        private readonly IClock _clock;

        public FakePortalAuthenticationService(PortalLoginOutcome? forcedOutcome, IClock clock)
        {
            _forcedOutcome = forcedOutcome;
            _clock = clock;
        }

        public Task<PortalLoginResult> VerifyAsync(CompanyContext company, PortalSessionMaterial candidate, CancellationToken cancellationToken)
        {
            if (_forcedOutcome.HasValue && _forcedOutcome.Value != PortalLoginOutcome.Success)
            {
                return Task.FromResult(PortalLoginResult.Failure(_forcedOutcome.Value, "Kết quả giả lập: " + _forcedOutcome.Value));
            }

            string loggedInTaxCode;
            if (!candidate.Headers.TryGetValue(DevTaxCodeHeader, out loggedInTaxCode))
            {
                return Task.FromResult(PortalLoginResult.Failure(PortalLoginOutcome.InvalidSession, null));
            }

            if (!string.Equals(loggedInTaxCode, company.TaxCode.Value, StringComparison.Ordinal))
            {
                return Task.FromResult(PortalLoginResult.Failure(PortalLoginOutcome.TaxCodeMismatch, null));
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
