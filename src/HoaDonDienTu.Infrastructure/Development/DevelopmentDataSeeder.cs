#if DEBUG
using System;
using System.IO;
using System.Threading;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.Infrastructure.Companies;

namespace HoaDonDienTu.Infrastructure.Development
{
    /// <summary>
    /// Tạo dữ liệu mẫu thật (danh mục + thư mục kho trên đĩa) cho từng chế độ dev để chạy qua đúng mã production của DD-02.
    /// Chỉ tạo khi danh mục chưa tồn tại. Chỉ có ở Debug.
    /// </summary>
    public static class DevelopmentDataSeeder
    {
        public static void Seed(string dataDirectory, string mode)
        {
            Directory.CreateDirectory(dataDirectory);
            var catalogPath = Path.Combine(dataDirectory, "companies.json");
            if (File.Exists(catalogPath)) return;

            if (string.Equals(mode, "Recovery", StringComparison.OrdinalIgnoreCase))
            {
                File.WriteAllText(catalogPath, "{ \"schemaVersion\": 1, \"companies\": [ truncated");
                return;
            }

            var count = 0;
            if (string.IsNullOrEmpty(mode) || string.Equals(mode, "Standard", StringComparison.OrdinalIgnoreCase)) count = 1;
            else if (string.Equals(mode, "Service", StringComparison.OrdinalIgnoreCase) || string.Equals(mode, "ReadOnly", StringComparison.OrdinalIgnoreCase)) count = 3;
            if (count == 0) return;

            var catalog = new JsonCompanyCatalog(catalogPath);
            var storage = new FileSystemCompanyStorage();
            for (var i = 0; i < count; i++)
            {
                var taxCode = TaxCode.Parse("01012345" + (60 + i).ToString("00"));
                var id = CompanyId.NewId();
                var path = Path.Combine(dataDirectory, "Companies", taxCode.Value);
                storage.Initialize(path, id, taxCode);
                catalog.AddAsync(
                    new CompanyCatalogItem(id, taxCode, "CÔNG TY MẪU " + (i + 1), path, null, null, 0, 0, CompanyCatalogStatus.Active),
                    CancellationToken.None).GetAwaiter().GetResult();
            }
        }
    }
}
#endif
