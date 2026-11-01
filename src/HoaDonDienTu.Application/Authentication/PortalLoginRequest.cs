using HoaDonDienTu.Application.Navigation;
using HoaDonDienTu.Domain.Companies;

namespace HoaDonDienTu.Application.Authentication
{
    public enum LoginReason
    {
        Startup,
        CompanySelected,
        TokenExpired,
        Manual
    }

    /// <summary>Luôn đăng nhập MST đang hoạt động; không có đăng nhập riêng cho chế độ dịch vụ.</summary>
    public sealed class PortalLoginRequest
    {
        public PortalLoginRequest(
            CompanyId companyId,
            TaxCode expectedTaxCode,
            LoginReason reason,
            ScreenId returnScreen,
            object returnParameter)
        {
            CompanyId = companyId;
            ExpectedTaxCode = expectedTaxCode;
            Reason = reason;
            ReturnScreen = returnScreen;
            ReturnParameter = returnParameter;
        }

        public CompanyId CompanyId { get; private set; }
        public TaxCode ExpectedTaxCode { get; private set; }
        public LoginReason Reason { get; private set; }
        public ScreenId ReturnScreen { get; private set; }
        public object ReturnParameter { get; private set; }
    }
}
