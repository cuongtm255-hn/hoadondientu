using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Application.Licensing;
using HoaDonDienTu.Domain.Licensing;
using NUnit.Framework;

namespace HoaDonDienTu.Application.Tests
{
    /// <summary>Licence đơn giản theo máy: dùng thử 30 ngày rồi nhập mã kích hoạt (kiểm offline). Đồng hồ, kho lưu và bộ kiểm mã đều là giả.</summary>
    [TestFixture]
    public class MachineLicenseServiceTests
    {
        private const string Machine = "AAAA1111-BBBB2222-CCCC3333-DDDD4444";
        private static readonly DateTime Start = new DateTime(2026, 11, 7, 9, 30, 0, DateTimeKind.Utc);

        private MemoryStore _store;
        private FakeClock _clock;
        private FakeVerifier _verifier;
        private MachineLicenseService _service;

        [SetUp]
        public void SetUp()
        {
            _store = new MemoryStore();
            _clock = new FakeClock { Now = Start };
            _verifier = new FakeVerifier();
            _service = NewService();
        }

        private MachineLicenseService NewService()
        {
            return new MachineLicenseService(_store, new FakeMachine(), _verifier, _clock, new NullLogger());
        }

        // ---- Dùng thử ----

        [Test]
        public void TheFirstEvaluation_StartsA30DayTrial_WithoutAskingForAnything()
        {
            var snapshot = _service.Evaluate();

            Assert.That(snapshot.Status, Is.EqualTo(LicenseStatus.Trial));
            Assert.That(snapshot.RemainingTrialDays, Is.EqualTo(30));
            Assert.That(snapshot.CanUse, Is.True);
            Assert.That(snapshot.IsActivated, Is.False);
            Assert.That(snapshot.MachineCode, Is.EqualTo(Machine));
            Assert.That(snapshot.FirstRunUtc, Is.EqualTo(Start));
            Assert.That(_store.Record, Is.Not.Null, "Mốc chạy đầu tiên phải được lưu ngay");
            Assert.That(_store.Record.FirstRunUtc, Is.EqualTo(Start));
        }

        [Test]
        public void TheTrialCountsDownByCalendarDays_AndEndsAfterThirtyOfThem()
        {
            _service.Evaluate();

            _clock.Now = Start.AddDays(10);
            Assert.That(_service.Evaluate().RemainingTrialDays, Is.EqualTo(20));

            _clock.Now = Start.AddDays(29);
            var lastDay = _service.Evaluate();
            Assert.That(lastDay.Status, Is.EqualTo(LicenseStatus.Trial));
            Assert.That(lastDay.RemainingTrialDays, Is.EqualTo(1));
            Assert.That(lastDay.CanUse, Is.True);

            _clock.Now = Start.AddDays(30);
            var over = _service.Evaluate();
            Assert.That(over.Status, Is.EqualTo(LicenseStatus.Expired));
            Assert.That(over.IsTrialExpired, Is.True);
            Assert.That(over.RemainingTrialDays, Is.EqualTo(0));
            Assert.That(over.CanUse, Is.False, "Hết thời gian dùng thử mà chưa kích hoạt thì không được vào ứng dụng");
        }

        [Test]
        public void TheTrialEndsAtTheStartOfTheThirtyFirstDay_NotAtTheHourOfTheFirstRun()
        {
            _service.Evaluate();

            _clock.Now = Start.Date.AddDays(30).AddMinutes(-1);
            Assert.That(_service.Evaluate().CanUse, Is.True);

            _clock.Now = Start.Date.AddDays(30);
            Assert.That(_service.Evaluate().CanUse, Is.False);
            Assert.That(_service.Evaluate().TrialEndsUtc, Is.EqualTo(Start.Date.AddDays(30)));
        }

        [Test]
        public void SettingTheClockBack_DoesNotExtendTheTrial()
        {
            _service.Evaluate();
            _clock.Now = Start.AddDays(29);
            Assert.That(_service.Evaluate().RemainingTrialDays, Is.EqualTo(1));

            _clock.Now = Start.AddDays(2);
            Assert.That(_service.Evaluate().RemainingTrialDays, Is.EqualTo(1), "Chỉnh lùi đồng hồ không lấy lại được ngày đã dùng");

            _clock.Now = Start.AddDays(31);
            Assert.That(_service.Evaluate().CanUse, Is.False);

            _clock.Now = Start;
            Assert.That(_service.Evaluate().CanUse, Is.False, "Đã hết hạn thì chỉnh đồng hồ về lúc đầu cũng không mở lại");
        }

        [Test]
        public void AFirstRunDateInTheFuture_IsNotAnAdvantage()
        {
            _store.Record = new LicenseRecord { FirstRunUtc = Start.AddDays(400), LastSeenUtc = Start.AddDays(-1) };

            var snapshot = _service.Evaluate();

            Assert.That(snapshot.RemainingTrialDays, Is.EqualTo(30));
            Assert.That(snapshot.FirstRunUtc, Is.EqualTo(Start));
        }

        [Test]
        public void TheTrialSurvivesARestart_BecauseTheStateIsStored()
        {
            _service.Evaluate();
            _clock.Now = Start.AddDays(12);

            var restarted = NewService().Evaluate();

            Assert.That(restarted.RemainingTrialDays, Is.EqualTo(18));
            Assert.That(restarted.FirstRunUtc, Is.EqualTo(Start));
        }

        // ---- Kích hoạt ----

        [Test]
        public async Task ActivatingWithAValidKey_UnlocksTheAppForGood_AndSurvivesARestart()
        {
            var status = await _service.ActivateAsync("OK-key-1234", CancellationToken.None);

            Assert.That(status, Is.EqualTo(LicenseStatus.Valid));
            var snapshot = _service.Evaluate();
            Assert.That(snapshot.IsActivated, Is.True);
            Assert.That(snapshot.CanUse, Is.True);
            Assert.That(snapshot.KeyHint, Is.EqualTo("••••1234"), "Chỉ 4 ký tự cuối của mã được hiển thị");
            Assert.That(snapshot.ActivatedAtUtc, Is.EqualTo(FakeVerifier.Issued));
            Assert.That(_service.CurrentInfo.Status, Is.EqualTo(LicenseStatus.Valid));
            Assert.That(_service.CurrentInfo.LicenseType, Is.EqualTo("PerMachine"));
            Assert.That(_service.CurrentInfo.ExpiresAtUtc, Is.Null, "Đã kích hoạt thì không có hạn dùng");

            _clock.Now = Start.AddDays(900);
            Assert.That(NewService().Evaluate().Status, Is.EqualTo(LicenseStatus.Valid), "Kích hoạt không hết hạn theo thời gian");
        }

        [Test]
        public async Task AfterTheTrialHasEnded_ActivatingBringsTheAppBack()
        {
            _service.Evaluate();
            _clock.Now = Start.AddDays(45);
            Assert.That(_service.Evaluate().CanUse, Is.False);

            await _service.ActivateAsync("OK-key-0001", CancellationToken.None);

            Assert.That(_service.Evaluate().CanUse, Is.True);
        }

        [Test]
        public void AnInvalidKey_IsRefused_AndChangesNothing()
        {
            _service.Evaluate();
            var before = _store.Record.ActivationKey;

            var ex = Assert.ThrowsAsync<LicenseException>(() => _service.ActivateAsync("BAD-key", CancellationToken.None));

            Assert.That(ex.Failure, Is.EqualTo(LicenseFailure.KeyInvalid));
            Assert.That(ex.Message, Is.Not.Empty);
            Assert.That(_store.Record.ActivationKey, Is.EqualTo(before), "Mã sai không được lưu");
            Assert.That(_service.Evaluate().Status, Is.EqualTo(LicenseStatus.Trial));
        }

        [Test]
        public void AKeyForAnotherMachine_IsRefusedWithAClearReason()
        {
            var ex = Assert.ThrowsAsync<LicenseException>(() => _service.ActivateAsync("OTHER-machine", CancellationToken.None));

            Assert.That(ex.Failure, Is.EqualTo(LicenseFailure.KeyBoundToOtherMachine));
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase(null)]
        public void AnEmptyKey_AsksTheUserToEnterOne(string key)
        {
            var ex = Assert.ThrowsAsync<LicenseException>(() => _service.ActivateAsync(key, CancellationToken.None));

            Assert.That(ex.Failure, Is.EqualTo(LicenseFailure.KeyInvalid));
            Assert.That(ex.Message, Does.Contain("nhập mã kích hoạt"));
            Assert.That(_verifier.Calls, Is.EqualTo(0));
        }

        [Test]
        public async Task SpacesAroundThePastedKey_AreIgnored()
        {
            await _service.ActivateAsync("   OK-key-5555\r\n", CancellationToken.None);

            Assert.That(_store.Record.ActivationKey, Is.EqualTo("OK-key-5555"));
        }

        [Test]
        public async Task AStoredKeyThatNoLongerMatchesThisMachine_FallsBackToTheTrialClock_AndSaysWhy()
        {
            await _service.ActivateAsync("OK-key-7777", CancellationToken.None);
            _verifier.RejectStored = true; // ví dụ cài lại Windows nên mã máy đổi

            var snapshot = NewService().Evaluate();

            Assert.That(snapshot.IsActivated, Is.False);
            Assert.That(snapshot.StoredKeyRejected, Is.True);
            Assert.That(snapshot.StoredKeyProblem, Is.Not.Empty);
            Assert.That(snapshot.Status, Is.EqualTo(LicenseStatus.Trial));

            var info = NewServiceInfo();
            Assert.That(info.Issue, Is.EqualTo(LicenseIssue.MachineMismatch));
        }

        private LicenseInfo NewServiceInfo()
        {
            var service = NewService();
            service.Evaluate();
            return service.CurrentInfo;
        }

        [Test]
        public async Task Deactivating_RemovesTheKey_ButDoesNotRenewTheTrial()
        {
            _service.Evaluate();
            _clock.Now = Start.AddDays(40);
            await _service.ActivateAsync("OK-key-8888", CancellationToken.None);

            var status = await _service.DeactivateAsync(CancellationToken.None);

            Assert.That(status, Is.EqualTo(LicenseStatus.Expired));
            Assert.That(_store.Record.ActivationKey, Is.Null.Or.Empty);
        }

        // ---- Thông tin và sự kiện ----

        [Test]
        public void CurrentInfo_DescribesTheTrial_WithTheEndDate()
        {
            _service.Evaluate();

            var info = _service.CurrentInfo;

            Assert.That(info.Status, Is.EqualTo(LicenseStatus.Trial));
            Assert.That(info.LicenseType, Is.EqualTo("Trial"));
            Assert.That(info.ExpiresAtUtc, Is.EqualTo(Start.Date.AddDays(30)));
            Assert.That(info.Issue, Is.EqualTo(LicenseIssue.None));
        }

        [Test]
        public void CurrentInfo_AfterTheTrial_ReportsExpired()
        {
            _service.Evaluate();
            _clock.Now = Start.AddDays(31);
            _service.Evaluate();

            Assert.That(_service.CurrentInfo.Status, Is.EqualTo(LicenseStatus.Expired));
            Assert.That(_service.CurrentInfo.Issue, Is.EqualTo(LicenseIssue.Expired));
        }

        [Test]
        public async Task InfoChanged_IsRaisedWhenTheStateChanges_AndNotOnRepeatedEvaluations()
        {
            var raised = 0;
            _service.InfoChanged += (s, e) => raised++;

            _service.Evaluate();
            var afterFirst = raised;
            _service.Evaluate();
            _service.Evaluate();
            Assert.That(raised, Is.EqualTo(afterFirst), "Đánh giá lặp lại không đổi gì thì không phát sự kiện");

            await _service.ActivateAsync("OK-key-9999", CancellationToken.None);
            Assert.That(raised, Is.GreaterThan(afterFirst));
        }

        [Test]
        public async Task TheAsyncContractMethods_AllReportTheSameLocalState()
        {
            Assert.That(await _service.ValidateLocalAsync(CancellationToken.None), Is.EqualTo(LicenseStatus.Trial));
            Assert.That(await _service.ValidateOnlineAsync(CancellationToken.None), Is.EqualTo(LicenseStatus.Trial), "Không có máy chủ nên kiểm trực tuyến giống kiểm cục bộ");
        }

        [Test]
        public void AStoreThatCannotBeWritten_DoesNotBreakTheApp()
        {
            _store.FailSave = true;

            var snapshot = _service.Evaluate();

            Assert.That(snapshot.CanUse, Is.True);
            Assert.That(snapshot.Status, Is.EqualTo(LicenseStatus.Trial));
        }

        [TestCase(0, "đã hết")]
        [TestCase(1, "còn 1 ngày")]
        [TestCase(30, "còn 30 ngày")]
        public void DescribeRemaining_IsReadableForTheUser(int days, string expected)
        {
            Assert.That(MachineLicenseService.DescribeRemaining(days), Is.EqualTo(expected));
        }

        // ---- Fakes ----

        private sealed class MemoryStore : ILicenseStateStore
        {
            public LicenseRecord Record;
            public bool FailSave;

            public LicenseRecord Load()
            {
                return Record == null ? null : new LicenseRecord { FirstRunUtc = Record.FirstRunUtc, LastSeenUtc = Record.LastSeenUtc, ActivationKey = Record.ActivationKey, ActivatedAtUtc = Record.ActivatedAtUtc };
            }

            public void Save(LicenseRecord record)
            {
                if (FailSave) throw new System.IO.IOException("không ghi được");
                Record = new LicenseRecord { FirstRunUtc = record.FirstRunUtc, LastSeenUtc = record.LastSeenUtc, ActivationKey = record.ActivationKey, ActivatedAtUtc = record.ActivatedAtUtc };
            }
        }

        private sealed class FakeMachine : IMachineCodeProvider
        {
            public string GetMachineCode() { return Machine; }
        }

        private sealed class FakeClock : IClock
        {
            public DateTime Now;
            public DateTime UtcNow { get { return Now; } }
        }

        private sealed class NullLogger : IApplicationLogger
        {
            public void Info(string message) { }
            public void Warning(string message) { }
            public void Error(string message, Exception exception) { }
        }

        /// <summary>Quy ước: mã bắt đầu "OK" hợp lệ, "OTHER" thuộc máy khác, còn lại sai chữ ký.</summary>
        private sealed class FakeVerifier : IActivationKeyVerifier
        {
            public static readonly DateTime Issued = new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);
            public int Calls;
            public bool RejectStored;

            public ActivationKeyCheck Verify(string activationKey, string machineCode)
            {
                Calls++;
                if (RejectStored) return ActivationKeyCheck.Fail(LicenseFailure.KeyBoundToOtherMachine, "Mã kích hoạt không đúng với máy này.");
                if (activationKey.StartsWith("OK", StringComparison.Ordinal)) return ActivationKeyCheck.Ok(Issued);
                if (activationKey.StartsWith("OTHER", StringComparison.Ordinal)) return ActivationKeyCheck.Fail(LicenseFailure.KeyBoundToOtherMachine, "Mã kích hoạt không đúng với máy này.");
                return ActivationKeyCheck.Fail(LicenseFailure.KeyInvalid, "Mã kích hoạt không hợp lệ hoặc đã bị thay đổi.");
            }
        }
    }
}
