using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HoaDonDienTu.Application.Backup
{
    public sealed class ConnectivityResult
    {
        public string Name { get; set; }
        public bool Reachable { get; set; }

        /// <summary>Mô tả ngắn: thời gian phản hồi, mã trạng thái hoặc lý do lỗi; không chứa URL kèm token.</summary>
        public string Detail { get; set; }
    }

    /// <summary>Kiểm tra khả năng kết nối tới cổng Thuế và máy chủ licence (FR-SET-009). Chỉ kiểm tra máy chủ trả lời, không đăng nhập, không gọi API hóa đơn.</summary>
    public interface IConnectivityChecker
    {
        Task<IList<ConnectivityResult>> CheckAsync(CancellationToken cancellationToken);
    }
}
