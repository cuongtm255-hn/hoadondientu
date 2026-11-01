using System;
using HoaDonDienTu.Application.Authentication;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.Infrastructure.Portal;
using NUnit.Framework;

namespace HoaDonDienTu.Infrastructure.Tests
{
    [TestFixture]
    public class InMemoryPortalSessionStoreTests
    {
        private static PortalSession NewSession(string tax)
        {
            var material = new PortalSessionMaterial(null, null, "secret-token-123");
            return new PortalSession(CompanyId.NewId(), TaxCode.Parse(tax), material, DateTime.UtcNow);
        }

        [Test]
        public void Initially_NoSession()
        {
            var store = new InMemoryPortalSessionStore();

            Assert.That(store.HasActiveSession, Is.False);
            Assert.That(store.GetSnapshot(), Is.Null);
            Assert.That(store.GetSessionForApi(), Is.Null);
        }

        [Test]
        public void Set_ReplacesSession_AndBumpsGeneration()
        {
            var store = new InMemoryPortalSessionStore();

            store.Set(NewSession("0101234561"));
            var first = store.GetSnapshot();
            store.Set(NewSession("0101234562"));
            var second = store.GetSnapshot();

            Assert.That(second.Generation, Is.GreaterThan(first.Generation));
            Assert.That(second.TaxCode.Value, Is.EqualTo("0101234562"));
            Assert.That(store.HasActiveSession, Is.True);
        }

        [Test]
        public void Clear_RemovesSession_AndRaisesOnlyWhenChanged()
        {
            var store = new InMemoryPortalSessionStore();
            var raised = 0;
            store.SessionChanged += (s, e) => raised++;

            store.Clear();
            store.Set(NewSession("0101234561"));
            store.Clear();
            store.Clear();

            Assert.That(raised, Is.EqualTo(2));
            Assert.That(store.HasActiveSession, Is.False);
        }

        [Test]
        public void Snapshot_AndToString_DoNotExposeToken()
        {
            var store = new InMemoryPortalSessionStore();
            var session = NewSession("0101234561");
            store.Set(session);

            var snapshotText = string.Join("|", store.GetSnapshot().TaxCode, store.GetSnapshot().CompanyId);

            Assert.That(snapshotText, Does.Not.Contain("secret-token-123"));
            Assert.That(session.ToString(), Does.Not.Contain("secret-token-123"));
            Assert.That(session.Material.ToString(), Does.Not.Contain("secret-token-123"));
        }

        [Test]
        public void Set_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new InMemoryPortalSessionStore().Set(null));
        }
    }
}
