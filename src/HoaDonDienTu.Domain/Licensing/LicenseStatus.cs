namespace HoaDonDienTu.Domain.Licensing
{
    public enum LicenseStatus
    {
        None,
        Valid,
        GracePeriod,
        Expired,
        Revoked,
        Invalid,

        /// <summary>Đang trong 30 ngày dùng thử (chưa nhập mã kích hoạt): dùng đầy đủ chức năng.</summary>
        Trial
    }
}
