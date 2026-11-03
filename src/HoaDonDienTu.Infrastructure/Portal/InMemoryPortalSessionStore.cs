using System;
using HoaDonDienTu.Application.Authentication;

namespace HoaDonDienTu.Infrastructure.Portal
{
    public sealed class InMemoryPortalSessionStore : IPortalSessionStore
    {
        private readonly object _gate = new object();
        private PortalSession _session;
        private int _generation;

        public event EventHandler SessionChanged;

        public bool HasActiveSession
        {
            get
            {
                lock (_gate)
                {
                    return _session != null;
                }
            }
        }

        public PortalSessionSnapshot GetSnapshot()
        {
            lock (_gate)
            {
                if (_session == null) return null;
                return new PortalSessionSnapshot(_session.CompanyId, _session.VerifiedTaxCode, _session.EstablishedAtUtc, _generation);
            }
        }

        public PortalSession GetSessionForApi()
        {
            lock (_gate)
            {
                return _session;
            }
        }

        public void Set(PortalSession session)
        {
            if (session == null) throw new ArgumentNullException("session");
            lock (_gate)
            {
                _session = session;
                _generation++;
            }

            RaiseChanged();
        }

        public void Clear()
        {
            bool changed;
            lock (_gate)
            {
                changed = _session != null;
                _session = null;
            }

            if (changed) RaiseChanged();
        }

        private void RaiseChanged()
        {
            var handler = SessionChanged;
            if (handler == null) return;

            // Mỗi nơi nghe được báo độc lập: một màn hình lỗi khi cập nhật không được làm đăng nhập dang dở (form không đóng)
            // hoặc làm các màn hình còn lại không biết phiên đã đổi.
            foreach (EventHandler listener in handler.GetInvocationList())
            {
                try
                {
                    listener(this, EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.TraceError("Lỗi khi cập nhật theo phiên đăng nhập: " + ex.GetType().Name);
                }
            }
        }
    }
}
