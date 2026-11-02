using System;
using System.Collections.Generic;
using System.IO;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Invoices;

namespace HoaDonDienTu.Infrastructure.Persistence.Sqlite
{
    /// <summary>
    /// Cho biết <c>company.db</c> của doanh nghiệp đang mở: <c>&lt;kho&gt;\Data\company.db</c> (DD-06 mục 3). Tạo/nâng cấp file ở lần dùng đầu
    /// tiên của mỗi kho và giữ một <see cref="SqliteDatabase"/> cho mỗi file để các repository dùng chung một hàng đợi ghi.
    /// </summary>
    public sealed class CompanyDatabaseProvider
    {
        public const string RelativePath = "Data\\company.db";

        private readonly ICompanyContextAccessor _context;
        private readonly CompanyDatabaseInitializer _initializer;
        private readonly object _gate = new object();
        private readonly Dictionary<string, SqliteDatabase> _byPath = new Dictionary<string, SqliteDatabase>(StringComparer.OrdinalIgnoreCase);

        public CompanyDatabaseProvider(ICompanyContextAccessor context)
            : this(context, new CompanyDatabaseInitializer())
        {
        }

        public CompanyDatabaseProvider(ICompanyContextAccessor context, CompanyDatabaseInitializer initializer)
        {
            if (context == null) throw new ArgumentNullException("context");
            if (initializer == null) throw new ArgumentNullException("initializer");
            _context = context;
            _initializer = initializer;
        }

        /// <exception cref="StorageGuardException">Chưa mở doanh nghiệp nào.</exception>
        /// <exception cref="CompanyDatabaseException">Cơ sở dữ liệu hỏng hoặc mới hơn ứng dụng.</exception>
        public SqliteDatabase Current()
        {
            if (!_context.HasCurrent) throw new StorageGuardException("Chưa mở doanh nghiệp nào.");
            var path = Path.Combine(_context.Current.StorageRoot, RelativePath);
            lock (_gate)
            {
                SqliteDatabase database;
                if (!_byPath.TryGetValue(path, out database))
                {
                    database = _initializer.Initialize(path);
                    _byPath[path] = database;
                }

                return database;
            }
        }
    }
}
