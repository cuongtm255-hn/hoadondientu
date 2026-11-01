using System;
using HoaDonDienTu.Application.Authentication;
using HoaDonDienTu.Application.Licensing;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.Domain.Licensing;

namespace HoaDonDienTu.Application.Companies
{
    public sealed class StorageGuardException : Exception
    {
        public StorageGuardException(string message)
            : base(message)
        {
        }
    }

    /// <summary>Điều kiện bắt buộc trước mọi thao tác ghi (DD-01 mục 9.4). Không khớp thì từ chối trước khi ghi.</summary>
    public sealed class StorageGuard
    {
        private readonly ICompanyContextAccessor _context;
        private readonly IPortalSessionStore _portalSession;
        private readonly IEntitlementService _entitlement;

        public StorageGuard(
            ICompanyContextAccessor context,
            IPortalSessionStore portalSession,
            IEntitlementService entitlement)
        {
            if (context == null) throw new ArgumentNullException("context");
            if (portalSession == null) throw new ArgumentNullException("portalSession");
            if (entitlement == null) throw new ArgumentNullException("entitlement");
            _context = context;
            _portalSession = portalSession;
            _entitlement = entitlement;
        }

        /// <exception cref="StorageGuardException"/>
        /// <exception cref="EntitlementDeniedException"/>
        public void EnsureCanWrite(
            CompanyId commandCompanyId,
            TaxCode commandTaxCode,
            string targetStoragePath,
            AppFeature requiredFeature,
            bool requiresPortalSession)
        {
            if (!_context.HasCurrent) throw new StorageGuardException("Chưa có doanh nghiệp nào được mở.");
            var current = _context.Current;

            if (!current.CompanyId.Equals(commandCompanyId))
                throw new StorageGuardException("Thao tác thuộc doanh nghiệp khác với kho đang mở.");

            if (commandTaxCode == null || !current.TaxCode.Equals(commandTaxCode))
                throw new StorageGuardException("MST của thao tác không khớp với kho đang mở.");

            if (requiresPortalSession)
            {
                var session = _portalSession.GetSnapshot();
                if (session == null || !current.TaxCode.Equals(session.TaxCode))
                    throw new StorageGuardException("Phiên cổng Thuế không thuộc MST của kho đang mở.");
            }

            if (!IsUnderRoot(targetStoragePath, current.StorageRoot))
                throw new StorageGuardException("Đường dẫn ghi nằm ngoài thư mục của doanh nghiệp.");

            _entitlement.Demand(requiredFeature);
        }

        private static bool IsUnderRoot(string path, string root)
        {
            if (string.IsNullOrEmpty(path)) return false;
            var fullPath = System.IO.Path.GetFullPath(path);
            var fullRoot = System.IO.Path.GetFullPath(root).TrimEnd('\\', '/') + System.IO.Path.DirectorySeparatorChar;
            return fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
        }
    }
}
