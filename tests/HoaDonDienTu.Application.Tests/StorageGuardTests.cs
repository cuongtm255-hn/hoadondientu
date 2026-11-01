using System;
using HoaDonDienTu.Application.Authentication;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Licensing;
using HoaDonDienTu.Domain.Common;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.Domain.Licensing;
using NUnit.Framework;

namespace HoaDonDienTu.Application.Tests
{
    [TestFixture]
    public class StorageGuardTests
    {
        private static readonly CompanyId Id = CompanyId.NewId();
        private static readonly TaxCode Mst = TaxCode.Parse("0101234567");
        private const string Root = @"C:\data\A";

        [Test]
        public void AllMatch_Passes()
        {
            var guard = Guard(true, Mst, true, AppMode.Service);

            Assert.DoesNotThrow(() => guard.EnsureCanWrite(Id, Mst, Root + @"\2026\x.xml", AppFeature.NewDownload, true));
        }

        [Test]
        public void NoCurrentContext_Rejected()
        {
            var guard = Guard(false, Mst, true, AppMode.Service);

            Assert.Throws<StorageGuardException>(() => guard.EnsureCanWrite(Id, Mst, Root + @"\x", AppFeature.NewDownload, false));
        }

        [Test]
        public void DifferentCompanyId_Rejected()
        {
            var guard = Guard(true, Mst, true, AppMode.Service);

            Assert.Throws<StorageGuardException>(() => guard.EnsureCanWrite(CompanyId.NewId(), Mst, Root + @"\x", AppFeature.NewDownload, false));
        }

        [Test]
        public void DifferentTaxCode_Rejected()
        {
            var guard = Guard(true, Mst, true, AppMode.Service);

            Assert.Throws<StorageGuardException>(() =>
                guard.EnsureCanWrite(Id, TaxCode.Parse("0109999999"), Root + @"\x", AppFeature.NewDownload, false));
        }

        [Test]
        public void PortalSessionOfOtherTaxCode_Rejected()
        {
            var guard = Guard(true, TaxCode.Parse("0109999999"), true, AppMode.Service);

            Assert.Throws<StorageGuardException>(() => guard.EnsureCanWrite(Id, Mst, Root + @"\x", AppFeature.NewDownload, true));
        }

        [TestCase(@"C:\data\B\x.xml")]
        [TestCase(@"C:\data\A\..\B\x.xml")]
        [TestCase(@"C:\data\AB\x.xml")]
        [TestCase("")]
        public void PathOutsideRoot_Rejected(string path)
        {
            var guard = Guard(true, Mst, true, AppMode.Service);

            Assert.Throws<StorageGuardException>(() => guard.EnsureCanWrite(Id, Mst, path, AppFeature.NewDownload, false));
        }

        [Test]
        public void ReadOnlyMode_NewDownload_Denied()
        {
            var guard = Guard(true, Mst, true, AppMode.ReadOnlyService);

            Assert.Throws<EntitlementDeniedException>(() => guard.EnsureCanWrite(Id, Mst, Root + @"\x", AppFeature.NewDownload, false));
        }

        private static StorageGuard Guard(bool hasContext, TaxCode sessionTax, bool authenticated, AppMode mode)
        {
            var context = hasContext ? new CompanyContext(Id, Mst, "A", Root, null) : null;
            return new StorageGuard(new Accessor(context), new Session(authenticated, sessionTax), new Entitlement(mode));
        }

        private sealed class Accessor : ICompanyContextAccessor
        {
            public Accessor(CompanyContext current)
            {
                Current = current;
            }

            public bool HasCurrent { get { return Current != null; } }
            public CompanyContext Current { get; private set; }
        }

        private sealed class Session : IPortalSessionStore
        {
            private readonly PortalSessionSnapshot _snapshot;

            public Session(bool authenticated, TaxCode taxCode)
            {
                if (authenticated) _snapshot = new PortalSessionSnapshot(Id, taxCode, DateTime.UtcNow, 1);
            }

            public event EventHandler SessionChanged { add { } remove { } }
            public bool HasActiveSession { get { return _snapshot != null; } }
            public PortalSessionSnapshot GetSnapshot() { return _snapshot; }
            public PortalSession GetSessionForApi() { return null; }
            public void Set(PortalSession session) { }
            public void Clear() { }
        }

        private sealed class Entitlement : IEntitlementService
        {
            private readonly AppMode _mode;

            public Entitlement(AppMode mode)
            {
                _mode = mode;
            }

            public AppMode CurrentMode { get { return _mode; } }

            public bool IsAllowed(AppFeature feature)
            {
                return _mode != AppMode.ReadOnlyService || feature == AppFeature.ReadExport;
            }

            public void Demand(AppFeature feature)
            {
                if (!IsAllowed(feature)) throw new EntitlementDeniedException(feature);
            }

            public event EventHandler ModeChanged { add { } remove { } }
        }
    }
}
