using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Application.Licensing;
using HoaDonDienTu.Domain.Common;
using HoaDonDienTu.Domain.Licensing;
using NUnit.Framework;

namespace HoaDonDienTu.Application.Tests
{
    [TestFixture]
    public class LicenseServiceTests
    {
        private const string Machine = "machine-a";
        private static readonly DateTime T0 = new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);

        private FakeStore _store;
        private FakeServer _server;
        private FakeClock _clock;
        private FakeMachine _machine;
        private FakeVerifier _verifier;
        private CollectingLogger _logger;
        private LicenseService _service;

        [SetUp]
        public void SetUp()
        {
            _store = new FakeStore();
            _server = new FakeServer(Machine);
            _clock = new FakeClock(T0);
            _machine = new FakeMachine(Machine);
            _verifier = new FakeVerifier();
            _logger = new CollectingLogger();
            _service = new LicenseService(_store, _server, _machine, _verifier, _clock, _logger, "1.0.0");
        }

        private static LicenseGrant Grant(string machine = Machine, DateTime? expires = null)
        {
            return new LicenseGrant("Service", machine, T0, expires, new[] { AppFeature.MultiCompany, AppFeature.PortalLogin });
        }

        private void Seed(DateTime lastOnline, DateTime? lastSeen = null, DateTime? expires = null, string machine = Machine, bool revoked = false)
        {
            var grant = Grant(machine, expires);
            _store.Current = new StoredLicense("ABCD-EFGH-1234", grant, FakeVerifier.Sign(grant.ToSigningPayload()), lastOnline, lastSeen ?? lastOnline, revoked);
        }

        // ---- Evaluator ----

        [Test]
        public async Task ValidateLocal_NoFile_IsNone()
        {
            Assert.That(await _service.ValidateLocalAsync(CancellationToken.None), Is.EqualTo(LicenseStatus.None));
            Assert.That(_service.CurrentInfo.Issue, Is.EqualTo(LicenseIssue.NotActivated));
        }

        [Test]
        public async Task ValidateLocal_CorruptFile_IsInvalid_NotNone()
        {
            _store.Corrupt = true;

            Assert.That(await _service.ValidateLocalAsync(CancellationToken.None), Is.EqualTo(LicenseStatus.Invalid));
            Assert.That(_service.CurrentInfo.Issue, Is.EqualTo(LicenseIssue.Corrupt));
        }

        [Test]
        public async Task ValidateLocal_RecentOnline_IsValid()
        {
            Seed(T0.AddHours(-2));

            Assert.That(await _service.ValidateLocalAsync(CancellationToken.None), Is.EqualTo(LicenseStatus.Valid));
        }

        [Test]
        public async Task ValidateLocal_OfflineWithinThirtyDays_IsGrace_WithDeadline()
        {
            Seed(T0.AddDays(-10));

            Assert.That(await _service.ValidateLocalAsync(CancellationToken.None), Is.EqualTo(LicenseStatus.GracePeriod));
            Assert.That(_service.CurrentInfo.MustReconnectBeforeUtc, Is.EqualTo(T0.AddDays(-10).AddDays(30)));
        }

        [Test]
        public async Task ValidateLocal_OfflineOverThirtyDays_IsExpiredReadOnly()
        {
            Seed(T0.AddDays(-31));

            Assert.That(await _service.ValidateLocalAsync(CancellationToken.None), Is.EqualTo(LicenseStatus.Expired));
            Assert.That(_service.CurrentInfo.Issue, Is.EqualTo(LicenseIssue.OfflineGraceExceeded));
        }

        [Test]
        public async Task ValidateLocal_PastExpiry_IsExpired()
        {
            Seed(T0.AddHours(-1), expires: T0.AddMinutes(-1));

            Assert.That(await _service.ValidateLocalAsync(CancellationToken.None), Is.EqualTo(LicenseStatus.Expired));
            Assert.That(_service.CurrentInfo.Issue, Is.EqualTo(LicenseIssue.Expired));
        }

        [Test]
        public async Task ValidateLocal_OtherMachine_IsInvalid()
        {
            Seed(T0.AddHours(-1), machine: "machine-b");

            Assert.That(await _service.ValidateLocalAsync(CancellationToken.None), Is.EqualTo(LicenseStatus.Invalid));
            Assert.That(_service.CurrentInfo.Issue, Is.EqualTo(LicenseIssue.MachineMismatch));
        }

        [Test]
        public async Task ValidateLocal_TamperedGrant_IsInvalidSignature()
        {
            Seed(T0.AddHours(-1));
            var tampered = new LicenseGrant("Service", Machine, T0, null, new[] { AppFeature.MultiCompany, AppFeature.PortalLogin, AppFeature.NewDownload });
            _store.Current = new StoredLicense("ABCD-EFGH-1234", tampered, _store.Current.Signature, T0.AddHours(-1), T0.AddHours(-1), false);

            Assert.That(await _service.ValidateLocalAsync(CancellationToken.None), Is.EqualTo(LicenseStatus.Invalid));
            Assert.That(_service.CurrentInfo.Issue, Is.EqualTo(LicenseIssue.SignatureInvalid));
        }

        [Test]
        public async Task ValidateLocal_ClockRolledBack_RequiresOnline_AndDoesNotMoveLastSeenBack()
        {
            Seed(T0.AddHours(-1), lastSeen: T0.AddDays(2));

            Assert.That(await _service.ValidateLocalAsync(CancellationToken.None), Is.EqualTo(LicenseStatus.Invalid));
            Assert.That(_service.CurrentInfo.RequiresOnlineValidation, Is.True);
            Assert.That(_store.Current.LastSeenUtc, Is.EqualTo(T0.AddDays(2)));
        }

        [Test]
        public async Task ValidateLocal_SmallClockAdjustment_IsTolerated()
        {
            Seed(T0.AddHours(-1), lastSeen: T0.AddMinutes(3));

            Assert.That(await _service.ValidateLocalAsync(CancellationToken.None), Is.EqualTo(LicenseStatus.Valid));
        }

        [Test]
        public async Task ValidateLocal_AdvancesLastSeen()
        {
            Seed(T0.AddHours(-1));

            await _service.ValidateLocalAsync(CancellationToken.None);

            Assert.That(_store.Current.LastSeenUtc, Is.EqualTo(T0));
        }

        // ---- Activate ----

        [Test]
        public async Task Activate_Succeeds_StoresLicense_AndSendsOnlyKeyMachineVersion()
        {
            var status = await _service.ActivateAsync("  ABCD-EFGH-1234 ", CancellationToken.None);

            Assert.That(status, Is.EqualTo(LicenseStatus.Valid));
            Assert.That(_store.Current.LicenseKey, Is.EqualTo("ABCD-EFGH-1234"));
            Assert.That(_server.Requests, Is.EqualTo(new[] { "activate|ABCD-EFGH-1234|machine-a|1.0.0" }));
            Assert.That(_service.CurrentInfo.KeyHint, Is.EqualTo("••••1234"));
        }

        [Test]
        public void Activate_EmptyKey_IsRejectedWithoutCallingServer()
        {
            var ex = Assert.ThrowsAsync<LicenseException>(() => _service.ActivateAsync("  ", CancellationToken.None));

            Assert.That(ex.Failure, Is.EqualTo(LicenseFailure.KeyInvalid));
            Assert.That(_server.Requests, Is.Empty);
        }

        [Test]
        public void Activate_BadSignature_IsRejected_AndNothingStored()
        {
            _server.CorruptSignature = true;

            var ex = Assert.ThrowsAsync<LicenseException>(() => _service.ActivateAsync("KEY-0001", CancellationToken.None));

            Assert.That(ex.Failure, Is.EqualTo(LicenseFailure.ResponseRejected));
            Assert.That(_store.Current, Is.Null);
        }

        [Test]
        public void Activate_ResponseForOtherMachine_IsRejected()
        {
            _server.GrantMachine = "machine-b";

            var ex = Assert.ThrowsAsync<LicenseException>(() => _service.ActivateAsync("KEY-0001", CancellationToken.None));

            Assert.That(ex.Failure, Is.EqualTo(LicenseFailure.KeyBoundToOtherMachine));
            Assert.That(_store.Current, Is.Null);
        }

        [Test]
        public void Activate_ServerRejectsKey_PropagatesFailure()
        {
            _server.Fail = new LicenseException(LicenseFailure.KeyBoundToOtherMachine, "x");

            var ex = Assert.ThrowsAsync<LicenseException>(() => _service.ActivateAsync("KEY-0001", CancellationToken.None));

            Assert.That(ex.Failure, Is.EqualTo(LicenseFailure.KeyBoundToOtherMachine));
        }

        [Test]
        public void Activate_StoreFails_ReportsStorageFailed()
        {
            _store.SaveFails = true;

            var ex = Assert.ThrowsAsync<LicenseException>(() => _service.ActivateAsync("KEY-0001", CancellationToken.None));

            Assert.That(ex.Failure, Is.EqualTo(LicenseFailure.StorageFailed));
        }

        [Test]
        public async Task Activate_NeverLogsTheFullKey()
        {
            await _service.ActivateAsync("SECRET-KEY-9876", CancellationToken.None);

            Assert.That(string.Join("\n", _logger.Lines), Does.Not.Contain("SECRET"));
            Assert.That(string.Join("\n", _logger.Lines), Does.Contain("9876"));
        }

        // ---- Validate online ----

        [Test]
        public async Task ValidateOnline_Success_RefreshesLastOnline()
        {
            Seed(T0.AddDays(-10));

            var status = await _service.ValidateOnlineAsync(CancellationToken.None);

            Assert.That(status, Is.EqualTo(LicenseStatus.Valid));
            Assert.That(_store.Current.LastOnlineValidationUtc, Is.EqualTo(T0));
        }

        [Test]
        public async Task ValidateOnline_ServerDown_KeepsGraceInsteadOfRevoking()
        {
            Seed(T0.AddDays(-10));
            _server.Fail = new LicenseException(LicenseFailure.ServerUnavailable, "timeout");

            var status = await _service.ValidateOnlineAsync(CancellationToken.None);

            Assert.That(status, Is.EqualTo(LicenseStatus.GracePeriod));
            Assert.That(_store.Current.LastOnlineValidationUtc, Is.EqualTo(T0.AddDays(-10)));
        }

        [Test]
        public async Task ValidateOnline_ServerDown_AfterThirtyDays_IsExpired()
        {
            Seed(T0.AddDays(-40));
            _server.Fail = new LicenseException(LicenseFailure.ServerUnavailable, "timeout");

            Assert.That(await _service.ValidateOnlineAsync(CancellationToken.None), Is.EqualTo(LicenseStatus.Expired));
        }

        [Test]
        public async Task ValidateOnline_Revoked_PersistsRevocation_SoOfflineStaysReadOnly()
        {
            Seed(T0.AddHours(-1));
            _server.Fail = new LicenseException(LicenseFailure.Revoked, "revoked");

            Assert.That(await _service.ValidateOnlineAsync(CancellationToken.None), Is.EqualTo(LicenseStatus.Revoked));
            Assert.That(await _service.ValidateLocalAsync(CancellationToken.None), Is.EqualTo(LicenseStatus.Revoked));
        }

        [Test]
        public async Task ValidateOnline_ClockWasRolledBack_RecoversAfterOnlineSuccess()
        {
            Seed(T0.AddHours(-1), lastSeen: T0.AddDays(2));

            var status = await _service.ValidateOnlineAsync(CancellationToken.None);

            Assert.That(status, Is.EqualTo(LicenseStatus.Valid));
            Assert.That(_service.CurrentInfo.RequiresOnlineValidation, Is.False);
        }

        [Test]
        public async Task ValidateOnline_NotActivated_DoesNotCallServer()
        {
            Assert.That(await _service.ValidateOnlineAsync(CancellationToken.None), Is.EqualTo(LicenseStatus.None));
            Assert.That(_server.Requests, Is.Empty);
        }

        // ---- Deactivate ----

        [Test]
        public async Task Deactivate_ReleasesMachine_DeletesFile()
        {
            Seed(T0.AddHours(-1));

            var status = await _service.DeactivateAsync(CancellationToken.None);

            Assert.That(status, Is.EqualTo(LicenseStatus.None));
            Assert.That(_store.Current, Is.Null);
            Assert.That(_server.Requests, Has.Some.StartWith("deactivate|"));
        }

        [Test]
        public void Deactivate_ServerDown_KeepsLicenseFile()
        {
            Seed(T0.AddHours(-1));
            _server.Fail = new LicenseException(LicenseFailure.ServerUnavailable, "timeout");

            Assert.ThrowsAsync<LicenseException>(() => _service.DeactivateAsync(CancellationToken.None));
            Assert.That(_store.Current, Is.Not.Null);
        }

        [Test]
        public async Task InfoChanged_RaisedOnEachTransition()
        {
            var count = 0;
            _service.InfoChanged += (s, e) => count++;

            await _service.ActivateAsync("KEY-0001", CancellationToken.None);
            await _service.DeactivateAsync(CancellationToken.None);

            Assert.That(count, Is.EqualTo(2));
        }

        // ---- Fakes ----

        private sealed class FakeStore : ILicenseStore
        {
            public StoredLicense Current { get; set; }
            public bool Corrupt { get; set; }
            public bool SaveFails { get; set; }

            public LicenseLoadResult Load()
            {
                if (Corrupt) return new LicenseLoadResult(LicenseLoadStatus.Corrupt, null);
                return Current == null
                    ? new LicenseLoadResult(LicenseLoadStatus.NotFound, null)
                    : new LicenseLoadResult(LicenseLoadStatus.Ok, Current);
            }

            public void Save(StoredLicense license)
            {
                if (SaveFails) throw new System.IO.IOException("disk");
                Current = license;
            }

            public void Delete()
            {
                Current = null;
            }
        }

        private sealed class FakeServer : ILicenseServer
        {
            private readonly string _machine;

            public FakeServer(string machine)
            {
                _machine = machine;
                Requests = new List<string>();
            }

            public List<string> Requests { get; private set; }
            public Exception Fail { get; set; }
            public bool CorruptSignature { get; set; }
            public string GrantMachine { get; set; }

            public Task<LicenseServerResponse> ActivateAsync(string key, string machine, string version, CancellationToken ct)
            {
                Requests.Add("activate|" + key + "|" + machine + "|" + version);
                return Respond();
            }

            public Task<LicenseServerResponse> ValidateAsync(string key, string machine, string version, CancellationToken ct)
            {
                Requests.Add("validate|" + key + "|" + machine + "|" + version);
                return Respond();
            }

            public Task DeactivateAsync(string key, string machine, CancellationToken ct)
            {
                Requests.Add("deactivate|" + key + "|" + machine);
                if (Fail != null) throw Fail;
                return Task.FromResult(0);
            }

            private Task<LicenseServerResponse> Respond()
            {
                if (Fail != null) throw Fail;
                var grant = new LicenseGrant("Service", GrantMachine ?? _machine, T0, null, new[] { AppFeature.MultiCompany, AppFeature.PortalLogin });
                var signature = CorruptSignature ? "bad" : FakeVerifier.Sign(grant.ToSigningPayload());
                return Task.FromResult(new LicenseServerResponse(grant, signature));
            }
        }

        private sealed class FakeVerifier : ILicenseSignatureVerifier
        {
            public static string Sign(string payload)
            {
                return "sig:" + payload;
            }

            public bool Verify(string payload, string signature)
            {
                return signature == Sign(payload);
            }
        }

        private sealed class FakeMachine : IMachineIdentity
        {
            private readonly string _hash;

            public FakeMachine(string hash)
            {
                _hash = hash;
            }

            public string GetMachineIdHash()
            {
                return _hash;
            }
        }

        private sealed class FakeClock : IClock
        {
            public FakeClock(DateTime now)
            {
                UtcNow = now;
            }

            public DateTime UtcNow { get; set; }
        }

        private sealed class CollectingLogger : IApplicationLogger
        {
            public List<string> Lines = new List<string>();

            public void Info(string message)
            {
                Lines.Add(message);
            }

            public void Warning(string message)
            {
                Lines.Add(message);
            }

            public void Error(string message, Exception exception)
            {
                Lines.Add(message);
            }
        }
    }

    [TestFixture]
    public class EntitlementServiceTests
    {
        [TestCase(AppMode.Standard, AppFeature.MultiCompany, false)]
        [TestCase(AppMode.Standard, AppFeature.PortalLogin, true)]
        [TestCase(AppMode.Standard, AppFeature.NewDownload, true)]
        [TestCase(AppMode.Service, AppFeature.MultiCompany, true)]
        [TestCase(AppMode.ReadOnlyService, AppFeature.ReadExport, true)]
        [TestCase(AppMode.ReadOnlyService, AppFeature.PortalLogin, false)]
        [TestCase(AppMode.ReadOnlyService, AppFeature.NewDownload, false)]
        [TestCase(AppMode.ReadOnlyService, AppFeature.MultiCompany, false)]
        [TestCase(AppMode.ReadOnlyService, AppFeature.ManageSettings, false)]
        public void Matrix_FollowsDesign(AppMode mode, AppFeature feature, bool allowed)
        {
            var service = new EntitlementService(mode, false);

            Assert.That(service.IsAllowed(feature), Is.EqualTo(allowed));
            if (allowed) Assert.DoesNotThrow(() => service.Demand(feature));
            else Assert.Throws<EntitlementDeniedException>(() => service.Demand(feature));
        }

        [Test]
        public void ModeTransitions_StandardToServiceToReadOnlyToService()
        {
            var service = new EntitlementService(AppMode.Standard, false);
            var changes = new List<AppMode>();
            service.ModeChanged += (s, e) => changes.Add(service.CurrentMode);

            service.ApplyLicense(LicenseStatus.Valid, true);
            service.ApplyLicense(LicenseStatus.Valid, true);
            service.ApplyLicense(LicenseStatus.Expired, true);
            service.ApplyLicense(LicenseStatus.GracePeriod, true);

            Assert.That(changes, Is.EqualTo(new[] { AppMode.Service, AppMode.ReadOnlyService, AppMode.Service }));
        }

        [Test]
        public void LicenseLost_WithoutServiceHistory_ReturnsToStandard()
        {
            var service = new EntitlementService(AppMode.Standard, false);
            service.ApplyLicense(LicenseStatus.Valid, false);

            service.ApplyLicense(LicenseStatus.None, false);

            Assert.That(service.CurrentMode, Is.EqualTo(AppMode.Standard));
        }

        [Test]
        public void ServiceHistory_Sticks_SoLosingLicenseGoesReadOnly()
        {
            var service = new EntitlementService(AppMode.Service, false);
            service.ApplyLicense(LicenseStatus.Valid, true);

            service.ApplyLicense(LicenseStatus.Revoked, false);

            Assert.That(service.CurrentMode, Is.EqualTo(AppMode.ReadOnlyService));
        }
    }
}
