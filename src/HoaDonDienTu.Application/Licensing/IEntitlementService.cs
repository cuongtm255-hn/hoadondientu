using System;
using HoaDonDienTu.Domain.Common;
using HoaDonDienTu.Domain.Licensing;

namespace HoaDonDienTu.Application.Licensing
{
    public sealed class EntitlementDeniedException : Exception
    {
        public EntitlementDeniedException(AppFeature feature)
            : base("Tính năng không được phép ở chế độ hiện tại: " + feature)
        {
            Feature = feature;
        }

        public AppFeature Feature { get; private set; }
    }

    /// <summary>Contract theo DD-03 mục 3.2.</summary>
    public interface IEntitlementService
    {
        AppMode CurrentMode { get; }
        bool IsAllowed(AppFeature feature);

        /// <exception cref="EntitlementDeniedException"/>
        void Demand(AppFeature feature);

        event EventHandler ModeChanged;
    }
}
