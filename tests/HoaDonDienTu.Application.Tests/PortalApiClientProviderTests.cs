using System;
using HoaDonDienTu.Application.Authentication;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Domain.Companies;
using NUnit.Framework;

namespace HoaDonDienTu.Application.Tests
{
    [TestFixture]
    public class PortalApiClientProviderTests
    {
        private readonly CompanyId _id = CompanyId.NewId();
        private readonly TaxCode _tax = TaxCode.Parse("0101234567");
        private Store _store;
        private Factory _factory;
        private Accessor _context;
        private PortalApiClientProvider _provider;

        [SetUp]
        public void SetUp()
        {
            _store = new Store();
            _factory = new Factory();
            _context = new Accessor(new CompanyContext(_id, _tax, "A", @"C:\data\A", null));
            _provider = new PortalApiClientProvider(_store, _context, _factory);
        }

        [Test]
        public void NoSession_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => _provider.GetClient());
        }

        [Test]
        public void SameGeneration_ReusesClient()
        {
            _store.Set(_id, _tax);

            var first = _provider.GetClient();
            var second = _provider.GetClient();

            Assert.That(second, Is.SameAs(first));
            Assert.That(_factory.Created.Count, Is.EqualTo(1));
        }

        [Test]
        public void NewGeneration_DisposesOldClient_CreatesNew()
        {
            _store.Set(_id, _tax);
            var first = (FakeClient)_provider.GetClient();

            _store.Set(_id, _tax);
            var second = _provider.GetClient();

            Assert.That(first.Disposed, Is.True);
            Assert.That(second, Is.Not.SameAs(first));
        }

        [Test]
        public void Clear_DisposesClient()
        {
            _store.Set(_id, _tax);
            var client = (FakeClient)_provider.GetClient();

            _store.Clear();

            Assert.That(client.Disposed, Is.True);
            Assert.Throws<InvalidOperationException>(() => _provider.GetClient());
        }

        [Test]
        public void SessionOfOtherTaxCode_Rejected()
        {
            _store.Set(_id, TaxCode.Parse("0109999999"));

            Assert.Throws<InvalidOperationException>(() => _provider.GetClient());
        }

        [Test]
        public void Dispose_DisposesClient_AndStopsListening()
        {
            _store.Set(_id, _tax);
            var client = (FakeClient)_provider.GetClient();

            _provider.Dispose();

            Assert.That(client.Disposed, Is.True);
            Assert.That(_store.ListenerCount, Is.EqualTo(0));
        }

        private sealed class FakeClient : IPortalApiClient
        {
            public bool Disposed { get; private set; }

            public void Dispose()
            {
                Disposed = true;
            }
        }

        private sealed class Factory : IPortalApiClientFactory
        {
            public System.Collections.Generic.List<FakeClient> Created = new System.Collections.Generic.List<FakeClient>();

            public IPortalApiClient Create(CompanyContext company, PortalSession session)
            {
                var client = new FakeClient();
                Created.Add(client);
                return client;
            }
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

        private sealed class Store : IPortalSessionStore
        {
            private PortalSession _session;
            private int _generation;
            private EventHandler _changed;

            public event EventHandler SessionChanged
            {
                add { _changed += value; }
                remove { _changed -= value; }
            }

            public int ListenerCount
            {
                get { return _changed == null ? 0 : _changed.GetInvocationList().Length; }
            }

            public bool HasActiveSession { get { return _session != null; } }

            public PortalSessionSnapshot GetSnapshot()
            {
                return _session == null
                    ? null
                    : new PortalSessionSnapshot(_session.CompanyId, _session.VerifiedTaxCode, _session.EstablishedAtUtc, _generation);
            }

            public PortalSession GetSessionForApi() { return _session; }

            public void Set(PortalSession session)
            {
                _session = session;
                _generation++;
                if (_changed != null) _changed(this, EventArgs.Empty);
            }

            public void Set(CompanyId id, TaxCode tax)
            {
                Set(new PortalSession(id, tax, new PortalSessionMaterial(null, null, "t"), DateTime.UtcNow));
            }

            public void Clear()
            {
                _session = null;
                if (_changed != null) _changed(this, EventArgs.Empty);
            }
        }
    }
}
