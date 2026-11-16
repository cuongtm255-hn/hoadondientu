namespace HoaDonDienTu.Application.Groups
{
    /// <summary>Nhóm chức năng của phần mềm (DD-DVC-00). Hai nhóm không dùng chung phiên, WebView hay dữ liệu.</summary>
    public enum AppGroup
    {
        /// <summary>Tra cứu hóa đơn (cổng hoadondientu.gdt.gov.vn).</summary>
        Invoice = 0,

        /// <summary>Tra cứu Dịch vụ công thuế (cổng dichvucong.gdt.gov.vn/tthc/).</summary>
        PublicService = 1
    }
}
