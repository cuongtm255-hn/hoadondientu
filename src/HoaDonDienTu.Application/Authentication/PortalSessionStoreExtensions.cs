using HoaDonDienTu.Application.Companies;

namespace HoaDonDienTu.Application.Authentication
{
    public static class PortalSessionStoreExtensions
    {
        /// <summary>Đã đăng nhập cổng Thuế đúng doanh nghiệp đang mở (cùng mã doanh nghiệp và MST). Chức năng cần gọi cổng chỉ mở khi điều này đúng.</summary>
        public static bool HasSessionFor(this IPortalSessionStore store, ICompanyContextAccessor context)
        {
            if (!context.HasCurrent) return false;
            var snapshot = store.GetSnapshot();
            return snapshot != null
                && snapshot.CompanyId.Equals(context.Current.CompanyId)
                && snapshot.TaxCode.Equals(context.Current.TaxCode);
        }
    }
}
