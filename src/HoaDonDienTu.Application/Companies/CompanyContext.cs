using System;
using HoaDonDienTu.Domain.Companies;

namespace HoaDonDienTu.Application.Companies
{
    /// <summary>
    /// Kho đang mở của đúng một MST. Dispose đóng tài nguyên (SQLite...) của kho đó.
    /// UnitOfWorkFactory sẽ bổ sung khi implement DD-06.
    /// </summary>
    public sealed class CompanyContext : IDisposable
    {
        private readonly IDisposable _resources;
        private bool _disposed;

        public CompanyContext(CompanyId companyId, TaxCode taxCode, string displayName, string storageRoot, IDisposable resources)
        {
            if (taxCode == null) throw new ArgumentNullException("taxCode");
            if (string.IsNullOrEmpty(storageRoot)) throw new ArgumentException("storageRoot");
            CompanyId = companyId;
            TaxCode = taxCode;
            DisplayName = displayName ?? string.Empty;
            StorageRoot = storageRoot;
            _resources = resources;
        }

        public CompanyId CompanyId { get; private set; }
        public TaxCode TaxCode { get; private set; }
        public string DisplayName { get; private set; }
        public string StorageRoot { get; private set; }

        public bool IsDisposed
        {
            get { return _disposed; }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_resources != null) _resources.Dispose();
        }
    }
}
