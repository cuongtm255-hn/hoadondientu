using HoaDonDienTu.Domain.Companies;

namespace HoaDonDienTu.Application.Authentication
{
    /// <summary>Phiên cổng Thuế chỉ giữ trong bộ nhớ (T01-09 cung cấp implementation).</summary>
    public interface IPortalSessionStore
    {
        bool IsAuthenticated { get; }

        /// <summary>MST của phiên đã xác minh; null khi chưa đăng nhập.</summary>
        TaxCode TaxCode { get; }

        void Clear();
    }
}
