using System.Threading;
using System.Threading.Tasks;

namespace HoaDonDienTu.Application.PublicService
{
    /// <summary>
    /// Chạy một đoạn script TRONG trang cổng Dịch vụ công của WebView ngầm (ADR DVC-0001). WebView chỉ giữ một trang DVC (BR-DVC-06):
    /// nếu trang hiện tại khác <c>pagePath</c> thì nạp trang đó trước rồi mới chạy script. Cookie, token và header của cổng ở lại trong WebView.
    /// </summary>
    public interface IDvcPageRunner
    {
        /// <summary>Trả về chuỗi do script trả (script là một biểu thức async trả về chuỗi).</summary>
        Task<string> RunOnPageAsync(string pagePath, string script, CancellationToken cancellationToken);
    }
}
