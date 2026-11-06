using System;
using HoaDonDienTu.Domain.Common;
using HoaDonDienTu.Domain.Licensing;

namespace HoaDonDienTu.Application.Licensing
{
    /// <summary>
    /// Quyền tính năng theo chế độ hiện tại (DD-03 mục 3). Chế độ do <see cref="StartupCoordinator"/> hoặc
    /// <see cref="ApplyLicense"/> quyết định; mọi application service gọi <see cref="Demand"/> (FR-LIC-012).
    /// </summary>
    public sealed class EntitlementService : IEntitlementService, IEntitlementUpdater
    {
        private readonly object _sync = new object();
        private AppMode _mode;
        private bool _hasServiceHistory;

        public EntitlementService(AppMode initialMode, bool hasServiceHistory)
        {
            _mode = initialMode;
            _hasServiceHistory = hasServiceHistory;
        }

        public event EventHandler ModeChanged;

        public AppMode CurrentMode
        {
            get { lock (_sync) { return _mode; } }
        }

        public bool IsAllowed(AppFeature feature)
        {
            return FeatureMatrix.IsAllowed(CurrentMode, feature);
        }

        public void Demand(AppFeature feature)
        {
            if (!IsAllowed(feature)) throw new EntitlementDeniedException(feature);
        }

        /// <summary>Cập nhật chế độ sau kích hoạt/xác thực/hủy kích hoạt; phát ModeChanged nếu thay đổi.</summary>
        public void ApplyLicense(LicenseStatus status, bool hasServiceHistory)
        {
            AppMode next;
            lock (_sync)
            {
                _hasServiceHistory = _hasServiceHistory || hasServiceHistory;
                next = ResolveMode(status, _hasServiceHistory);
                if (next == _mode) return;
                _mode = next;
            }

            var handler = ModeChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        /// <summary>Cùng quy tắc với StartupCoordinator: Service khi licence còn hiệu lực; mất hiệu lực thì Standard hoặc ReadOnly nếu từng có nhiều kho.</summary>
        public static AppMode ResolveMode(LicenseStatus status, bool hasServiceHistory)
        {
            switch (status)
            {
                case LicenseStatus.Valid:
                case LicenseStatus.Trial:
                case LicenseStatus.GracePeriod:
                    return AppMode.Service;
                default:
                    return hasServiceHistory ? AppMode.ReadOnlyService : AppMode.Standard;
            }
        }
    }
}
