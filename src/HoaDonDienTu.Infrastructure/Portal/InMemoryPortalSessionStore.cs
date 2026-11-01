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
            if (handler != null) handler(this, EventArgs.Empty);
        }
    }
}
