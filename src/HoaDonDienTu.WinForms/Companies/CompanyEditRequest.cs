using HoaDonDienTu.Domain.Companies;

namespace HoaDonDienTu.WinForms.Companies
{
    public enum CompanyEditMode
    {
        /// <summary>StandardMode chạy lần đầu: khai báo doanh nghiệp duy nhất.</summary>
        FirstRun,

        /// <summary>ServiceMode: thêm doanh nghiệp mới (SCR-11).</summary>
        Add,

        /// <summary>Sửa tên hiển thị; liên kết lại thư mục nếu kho bị mất. MST không sửa được.</summary>
        Edit
    }

    /// <summary>Tham số điều hướng tới màn hình SCR-11.</summary>
    public sealed class CompanyEditRequest
    {
        private CompanyEditRequest(CompanyEditMode mode, CompanyId? companyId)
        {
            Mode = mode;
            CompanyId = companyId;
        }

        public CompanyEditMode Mode { get; private set; }
        public CompanyId? CompanyId { get; private set; }

        public static CompanyEditRequest FirstRun()
        {
            return new CompanyEditRequest(CompanyEditMode.FirstRun, null);
        }

        public static CompanyEditRequest Add()
        {
            return new CompanyEditRequest(CompanyEditMode.Add, null);
        }

        public static CompanyEditRequest Edit(CompanyId id)
        {
            return new CompanyEditRequest(CompanyEditMode.Edit, id);
        }
    }
}
