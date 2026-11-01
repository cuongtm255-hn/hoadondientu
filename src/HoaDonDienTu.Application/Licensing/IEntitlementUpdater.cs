using HoaDonDienTu.Domain.Licensing;

namespace HoaDonDienTu.Application.Licensing
{
    /// <summary>Cập nhật chế độ làm việc sau khi licence đổi (kích hoạt, xác thực, hủy). <see cref="EntitlementService"/> thực hiện.</summary>
    public interface IEntitlementUpdater
    {
        void ApplyLicense(LicenseStatus status, bool hasServiceHistory);
    }
}
