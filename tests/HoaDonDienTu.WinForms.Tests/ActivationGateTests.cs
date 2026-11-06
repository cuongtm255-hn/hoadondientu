using System;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Application.Licensing;
using HoaDonDienTu.Domain.Licensing;
using HoaDonDienTu.WinForms.Licensing;
using NUnit.Framework;

namespace HoaDonDienTu.WinForms.Tests
{
    /// <summary>Cổng vào ứng dụng: còn dùng thử/đã kích hoạt thì vào; hết dùng thử thì hiện hộp thoại và không kích hoạt được thì không vào.</summary>
    [TestFixture]
    [Category("Licensing")]
    public class ActivationGateTests
    {
        private static readonly DateTime Start = new DateTime(2026, 11, 7, 9, 0, 0, DateTimeKind.Utc);
        private Store _store;
        private Clock _clock;
        private MachineLicenseService _license;

        [SetUp]
        public void SetUp()
        {
            _store = new Store();
            _clock = new Clock { Now = Start };
            _license = new MachineLicenseService(_store, new Machine(), new Verifier(), _clock, new Log());
        }

        [Test]
        public void DuringTheTrial_TheAppOpens_WithoutAskingAnything()
        {
            var prompt = new FakePrompt(false);

            Assert.That(ActivationGate.Allow(_license, prompt), Is.True);
            Assert.That(prompt.Shown, Is.EqualTo(0));
        }

        [Test]
        public void AfterTheTrial_TheDialogIsShown_AndClosingItBlocksTheApp()
        {
            _license.Evaluate();
            _clock.Now = Start.AddDays(31);
            var prompt = new FakePrompt(false);

            Assert.That(ActivationGate.Allow(_license, prompt), Is.False);
            Assert.That(prompt.Shown, Is.EqualTo(1));
        }

        [Test]
        public void AfterTheTrial_ActivatingInTheDialog_OpensTheApp()
        {
            _license.Evaluate();
            _clock.Now = Start.AddDays(31);
            var prompt = new FakePrompt(true) { Action = l => l.ActivateAsync("OK-key", CancellationToken.None).GetAwaiter().GetResult() };

            Assert.That(ActivationGate.Allow(_license, prompt), Is.True);
        }

        [Test]
        public void ADialogThatClaimsSuccessWithoutActivating_DoesNotOpenTheApp()
        {
            _license.Evaluate();
            _clock.Now = Start.AddDays(31);

            Assert.That(ActivationGate.Allow(_license, new FakePrompt(true)), Is.False, "Quyết định theo trạng thái thật, không theo giá trị hộp thoại trả về");
        }

        [Test]
        public void AnActivatedMachine_NeverSeesTheDialog_EvenYearsLater()
        {
            _license.ActivateAsync("OK-key", CancellationToken.None).GetAwaiter().GetResult();
            _clock.Now = Start.AddDays(2000);
            var prompt = new FakePrompt(false);

            Assert.That(ActivationGate.Allow(_license, prompt), Is.True);
            Assert.That(prompt.Shown, Is.EqualTo(0));
        }

        [Test]
        public void NullArguments_AreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => ActivationGate.Allow(null, new FakePrompt(false)));
            Assert.Throws<ArgumentNullException>(() => ActivationGate.Allow(_license, null));
        }

        // ---- Màn hình SCR-12 ----

        [Test]
        public void Presenter_Build_DescribesTheTrialAndTheActivatedState()
        {
            var trial = LicensePresenter.Build(_license.Evaluate(), "PC", "1.0.0");
            Assert.That(trial.ModeTitle, Is.EqualTo("Đang dùng thử"));
            Assert.That(trial.ModeSubtitle, Does.StartWith("Còn 30 ngày"));
            Assert.That(trial.MachineCode, Is.EqualTo("AAAA1111-BBBB2222-CCCC3333-DDDD4444"));

            _license.ActivateAsync("OK-key-4321", CancellationToken.None).GetAwaiter().GetResult();
            var active = LicensePresenter.Build(_license.Evaluate(), "PC", "1.0.0");
            Assert.That(active.ModeTitle, Is.EqualTo("Đã kích hoạt"));
            Assert.That(active.ExpiresAt, Is.EqualTo("Không giới hạn"));
            Assert.That(active.ModeSubtitle, Does.Contain("••••4321"));
        }

        [Test]
        public void Presenter_Build_AfterTheTrial_AsksForAKey()
        {
            _license.Evaluate();
            _clock.Now = Start.AddDays(40);

            var model = LicensePresenter.Build(_license.Evaluate(), "PC", "1.0.0");

            Assert.That(model.ModeTitle, Is.EqualTo("Hết thời gian dùng thử"));
            Assert.That(model.Note, Does.Contain("mã kích hoạt"));
        }

        // ---- Fakes ----

        private sealed class FakePrompt : IActivationPrompt
        {
            private readonly bool _result;
            public int Shown;
            public Action<MachineLicenseService> Action;

            public FakePrompt(bool result) { _result = result; }

            public bool Prompt(MachineLicenseService license, LicenseSnapshot snapshot)
            {
                Shown++;
                if (Action != null) Action(license);
                return _result;
            }
        }

        private sealed class Store : ILicenseStateStore
        {
            private LicenseRecord _record;

            public LicenseRecord Load()
            {
                return _record == null ? null : Copy(_record);
            }

            public void Save(LicenseRecord record) { _record = Copy(record); }

            private static LicenseRecord Copy(LicenseRecord r)
            {
                return new LicenseRecord { FirstRunUtc = r.FirstRunUtc, LastSeenUtc = r.LastSeenUtc, ActivationKey = r.ActivationKey, ActivatedAtUtc = r.ActivatedAtUtc };
            }
        }

        private sealed class Machine : IMachineCodeProvider
        {
            public string GetMachineCode() { return "AAAA1111-BBBB2222-CCCC3333-DDDD4444"; }
        }

        private sealed class Clock : IClock
        {
            public DateTime Now;
            public DateTime UtcNow { get { return Now; } }
        }

        private sealed class Log : IApplicationLogger
        {
            public void Info(string message) { }
            public void Warning(string message) { }
            public void Error(string message, Exception exception) { }
        }

        private sealed class Verifier : IActivationKeyVerifier
        {
            public ActivationKeyCheck Verify(string activationKey, string machineCode)
            {
                return activationKey.StartsWith("OK", StringComparison.Ordinal)
                    ? ActivationKeyCheck.Ok(new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc))
                    : ActivationKeyCheck.Fail(LicenseFailure.KeyInvalid, "Mã kích hoạt không hợp lệ.");
            }
        }
    }
}
