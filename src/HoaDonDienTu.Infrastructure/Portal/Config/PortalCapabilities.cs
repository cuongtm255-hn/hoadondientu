using System;
using System.IO;
using HoaDonDienTu.Application.Downloads;
using HoaDonDienTu.Domain.Invoices;

namespace HoaDonDienTu.Infrastructure.Portal.Config
{
    /// <summary>Bộ lọc nào được hỗ trợ là do cấu hình quyết định: có tên tham số tức là cổng hỗ trợ (FR-SEARCH-007). Mỗi chiều (mua vào/bán ra) có bộ trường riêng.</summary>
    public sealed class PortalApiConfigCapabilities : IPortalCapabilities
    {
        private readonly PortalApiConfig _config;

        public PortalApiConfigCapabilities(PortalApiConfig config)
        {
            if (config == null) throw new ArgumentNullException("config");
            _config = config;
        }

        public bool SupportsFilter(InvoiceDirection direction, string filterField)
        {
            string name;
            var search = _config.Search;
            var map = search == null ? null : (direction == InvoiceDirection.Purchase ? search.PurchaseFilterFields : search.SoldFilterFields);
            return map != null && map.TryGetValue(filterField, out name) && !string.IsNullOrWhiteSpace(name);
        }
    }

    /// <summary>Thử tạo rồi xóa một file nhỏ trong thư mục kho để biết có ghi được không.</summary>
    public sealed class FileSystemStorageProbe : IStorageProbe
    {
        public StorageProbeResult Check(string storageRoot)
        {
            if (string.IsNullOrWhiteSpace(storageRoot)) return StorageProbeResult.Failed("chưa cấu hình thư mục");

            try
            {
                Directory.CreateDirectory(storageRoot);
                var probe = Path.Combine(storageRoot, ".write-test-" + Guid.NewGuid().ToString("N"));
                File.WriteAllText(probe, "x");
                File.Delete(probe);
                return StorageProbeResult.Ok();
            }
            catch (UnauthorizedAccessException)
            {
                return StorageProbeResult.Failed("không có quyền ghi");
            }
            catch (PathTooLongException)
            {
                return StorageProbeResult.Failed("đường dẫn quá dài");
            }
            catch (ArgumentException)
            {
                return StorageProbeResult.Failed("đường dẫn không hợp lệ");
            }
            catch (NotSupportedException)
            {
                return StorageProbeResult.Failed("đường dẫn không hợp lệ");
            }
            catch (IOException)
            {
                return StorageProbeResult.Failed("không ghi được (ổ đĩa đầy, bị khóa hoặc không tồn tại)");
            }
        }
    }
}
