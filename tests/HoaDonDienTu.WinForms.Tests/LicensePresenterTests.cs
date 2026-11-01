using System;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Licensing;
using HoaDonDienTu.Application.Navigation;
using HoaDonDienTu.Domain.Common;
using HoaDonDienTu.Domain.Licensing;
using HoaDonDienTu.WinForms.Licensing;
using NUnit.Framework;

namespace HoaDonDienTu.WinForms.Tests
{
    [TestFixture]
    public class LicensePresenterTests : CompanyUiTestBase
    {
        private static readonly DateTime T0 = new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);

        private FakeLicenseView _view;
        private ScriptedLicense _license;
        private EntitlementService _entitlement;
        private LicensePresenter _presenter;

        [SetUp]
        public void SetUp()
        {
            _view = new FakeLicenseView();
            _license = new ScriptedLicense();
            _entitlement = new EntitlementService(AppMode.Standard, false);
            _presenter = new LicensePresenter(
                _view, _license, _entitlement, _entitlement, Registry, new FixedMachine("84b2aaaaaaaaaaaaaaaaaaaaaaaaaaaa19c7"),
                () => Nav, Confirm, Runner, Errors, "DESKTOP-KT-01", "1.1.0 · Schema v1");
            _presenter.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            _presenter.Dispose();
        }

        [Test]
        public void Initialize_Standard_ShowsStandardStateAndMaskedMachineId()
        {
            var m = _view.Model;

            Assert.That(m.ModeTitle, Is.EqualTo("Chế độ chuẩn"));
            Assert.That(m.ModeLetter, Is.EqualTo("S"));
            Assert.That(m.Device, Is.EqualTo("DESKTOP-KT-01"));
            Assert.That(m.MachineShort, Is.EqualTo("84B2-••••-19C7"));
            Assert.That(m.LastValidation, Is.EqualTo("Chưa kích hoạt"));
            Assert.That(m.HasLicense, Is.False);
            Assert.That(m.Version, Is.EqualTo("1.1.0 · Schema v1"));
        }

        [Test]
        public void Activate_EmptyKey_ShowsInlineErrorWithoutCallingService()
        {
            _view.Key = "   ";

            _view.RaiseActivate();

            Assert.That(_license.Calls, Has.None.StartWith("activate"));
            Assert.That(_view.LastMessage, Does.Contain("Nhập licence key"));
            Assert.That(_view.LastMessageIsError, Is.True);
        }

        [Test]
        public void Activate_Success_SwitchesToService_ClearsKey_AndOpensCompanyList()
        {
            _view.Key = " HDDT-SVC-1234 ";

            _view.RaiseActivate();

            Assert.That(_license.Calls, Is.EqualTo(new[] { "local", "activate:HDDT-SVC-1234" }));
            Assert.That(_entitlement.CurrentMode, Is.EqualTo(AppMode.Service));
            Assert.That(_view.Model.ModeTitle, Is.EqualTo("Chế độ dịch vụ"));
            Assert.That(_view.Key, Is.Empty);
            Assert.That(_view.LastMessageIsError, Is.False);
            Assert.That(Nav.Targets, Is.EqualTo(new[] { ScreenId.CompanyList }));
        }

        [Test]
        public void Activate_Rejected_ShowsMappedError_AndKeepsModeAndKey()
        {
            _license.ActivateFailure = new LicenseException(LicenseFailure.KeyBoundToOtherMachine, "x");
            _view.Key = "HDDT-SVC-1234";

            _view.RaiseActivate();

            Assert.That(Errors.Shown, Has.Count.EqualTo(1));
            Assert.That(Errors.Shown[0].Code, Is.EqualTo("LICENCE_OTHER_MACHINE"));
            Assert.That(_entitlement.CurrentMode, Is.EqualTo(AppMode.Standard));
            Assert.That(_view.Key, Is.EqualTo("HDDT-SVC-1234"));
            Assert.That(Nav.Targets, Is.Empty);
        }

        [Test]
        public void Busy_IsReportedAndAlwaysReleased()
        {
            _license.ActivateFailure = new LicenseException(LicenseFailure.ServerUnavailable, "x");
            _view.Key = "HDDT-SVC-1234";

            _view.RaiseActivate();

            Assert.That(_view.BusyHistory, Does.Contain(true));
            Assert.That(_view.Busy, Is.False);
        }

        [Test]
        public void Validate_Revoked_MovesToReadOnly_WhenServiceHistoryExists()
        {
            _entitlement.ApplyLicense(LicenseStatus.Valid, true);
            _license.Info = Info(LicenseStatus.Valid, LicenseIssue.None);
            _license.ValidateResult = Info(LicenseStatus.Revoked, LicenseIssue.Revoked);

            _view.RaiseValidate();

            Assert.That(_entitlement.CurrentMode, Is.EqualTo(AppMode.ReadOnlyService));
            Assert.That(_view.Model.ModeTitle, Does.StartWith("Chỉ đọc"));
            Assert.That(_view.Model.ModeSubtitle, Is.EqualTo("Licence đã bị thu hồi"));
            Assert.That(_view.LastMessageIsError, Is.True);
        }

        [Test]
        public void Validate_Grace_ShowsReconnectDeadline_AndStaysService()
        {
            _entitlement.ApplyLicense(LicenseStatus.Valid, true);
            _license.ValidateResult = new LicenseInfo(LicenseStatus.GracePeriod, LicenseIssue.None, "Service", "••••1234", null, T0.AddDays(-10), T0.AddDays(20));

            _view.RaiseValidate();

            Assert.That(_entitlement.CurrentMode, Is.EqualTo(AppMode.Service));
            Assert.That(_view.Model.ModeSubtitle, Does.Contain("kết nối lại trước"));
            Assert.That(_view.LastMessage, Does.Contain("ngoại tuyến"));
        }

        [Test]
        public void Validate_ClockTampered_ExplainsHowToRecover()
        {
            _license.Info = Info(LicenseStatus.Invalid, LicenseIssue.ClockTampered);
            _license.ValidateResult = _license.Info;

            _view.RaiseValidate();

            Assert.That(_view.Model.Note, Does.Contain("Xác thực ngay"));
        }

        [Test]
        public void Deactivate_Declined_DoesNothing()
        {
            _license.Info = Info(LicenseStatus.Valid, LicenseIssue.None);
            Confirm.Answer = false;

            _view.RaiseDeactivate();

            Assert.That(_license.Calls, Has.None.EqualTo("deactivate"));
            Assert.That(Confirm.Messages, Has.Count.EqualTo(1));
            Assert.That(Confirm.Messages[0], Does.Contain("••••1234"));
        }

        [Test]
        public void Deactivate_Confirmed_ReturnsToStandard()
        {
            _entitlement.ApplyLicense(LicenseStatus.Valid, false);
            _license.Info = Info(LicenseStatus.Valid, LicenseIssue.None);

            _view.RaiseDeactivate();

            Assert.That(_license.Calls, Has.Some.EqualTo("deactivate"));
            Assert.That(_entitlement.CurrentMode, Is.EqualTo(AppMode.Standard));
            Assert.That(_view.Model.HasLicense, Is.False);
        }

        [Test]
        public void Deactivate_ServerDown_ShowsError_AndKeepsMode()
        {
            _entitlement.ApplyLicense(LicenseStatus.Valid, false);
            _license.Info = Info(LicenseStatus.Valid, LicenseIssue.None);
            _license.DeactivateFailure = new LicenseException(LicenseFailure.ServerUnavailable, "x");

            _view.RaiseDeactivate();

            Assert.That(Errors.Shown[0].Code, Is.EqualTo("LICENCE_SERVER"));
            Assert.That(_entitlement.CurrentMode, Is.EqualTo(AppMode.Service));
        }

        [Test]
        public void ReadOnly_ShowsLockedCapabilitiesNote()
        {
            _license.Info = new LicenseInfo(LicenseStatus.Expired, LicenseIssue.OfflineGraceExceeded, "Service", "••••1234", null, T0.AddDays(-40), T0.AddDays(-10));
            _entitlement.ApplyLicense(LicenseStatus.Expired, true);
            _view.RaiseValidateLocalRefresh(_presenter);

            Assert.That(_view.Model.ModeSubtitle, Is.EqualTo("Quá 30 ngày chưa xác thực trực tuyến"));
            Assert.That(_view.Model.Note, Does.Contain("khóa"));
        }

        [Test]
        public void Back_NavigatesToSettings()
        {
            _view.RaiseBack();

            Assert.That(Nav.Targets, Is.EqualTo(new[] { ScreenId.Settings }));
        }

        [Test]
        public void ShortMachine_ShortInputIsReturnedAsIs()
        {
            Assert.That(LicensePresenter.ShortMachine("abc"), Is.EqualTo("abc"));
            Assert.That(LicensePresenter.ShortMachine(null), Is.Empty);
        }

        private static LicenseInfo Info(LicenseStatus status, LicenseIssue issue)
        {
            return new LicenseInfo(status, issue, "Service", "••••1234", T0.AddYears(1), T0.AddHours(-1), T0.AddDays(30));
        }

        private sealed class ScriptedLicense : ILicenseService
        {
            public System.Collections.Generic.List<string> Calls = new System.Collections.Generic.List<string>();
            public LicenseException ActivateFailure;
            public LicenseException DeactivateFailure;
            public LicenseInfo Info = LicenseInfo.NotActivated;
            public LicenseInfo ValidateResult;

            public LicenseInfo CurrentInfo
            {
                get { return Info; }
            }

            public event EventHandler InfoChanged
            {
                add { }
                remove { }
            }

            public Task<LicenseStatus> ValidateLocalAsync(CancellationToken cancellationToken)
            {
                Calls.Add("local");
                return Task.FromResult(Info.Status);
            }

            public Task<LicenseStatus> ValidateOnlineAsync(CancellationToken cancellationToken)
            {
                Calls.Add("online");
                if (ValidateResult != null) Info = ValidateResult;
                return Task.FromResult(Info.Status);
            }

            public Task<LicenseStatus> ActivateAsync(string licenseKey, CancellationToken cancellationToken)
            {
                Calls.Add("activate:" + licenseKey);
                if (ActivateFailure != null) throw ActivateFailure;
                Info = new LicenseInfo(LicenseStatus.Valid, LicenseIssue.None, "Service", LicenseKeys.Hint(licenseKey), T0.AddYears(1), T0, T0.AddDays(30));
                return Task.FromResult(LicenseStatus.Valid);
            }

            public Task<LicenseStatus> DeactivateAsync(CancellationToken cancellationToken)
            {
                Calls.Add("deactivate");
                if (DeactivateFailure != null) throw DeactivateFailure;
                Info = LicenseInfo.NotActivated;
                return Task.FromResult(LicenseStatus.None);
            }
        }

        private sealed class FixedMachine : IMachineIdentity
        {
            private readonly string _hash;

            public FixedMachine(string hash)
            {
                _hash = hash;
            }

            public string GetMachineIdHash()
            {
                return _hash;
            }
        }

        private sealed class FakeLicenseView : ILicenseView
        {
            public string Key = string.Empty;
            public LicenseViewModel Model;
            public string LastMessage;
            public bool LastMessageIsError;
            public bool Busy;
            public System.Collections.Generic.List<bool> BusyHistory = new System.Collections.Generic.List<bool>();

            public event EventHandler ActivateRequested;
            public event EventHandler ValidateRequested;
            public event EventHandler DeactivateRequested;
            public event EventHandler BackRequested;

            public string LicenseKeyInput
            {
                get { return Key; }
            }

            public void ShowState(LicenseViewModel model)
            {
                Model = model;
            }

            public void SetBusy(bool busy)
            {
                Busy = busy;
                BusyHistory.Add(busy);
            }

            public void ShowMessage(string text, bool isError)
            {
                LastMessage = text;
                LastMessageIsError = isError;
            }

            public void ClearKeyInput()
            {
                Key = string.Empty;
            }

            public void RaiseActivate()
            {
                ActivateRequested(this, EventArgs.Empty);
            }

            public void RaiseValidate()
            {
                ValidateRequested(this, EventArgs.Empty);
            }

            public void RaiseDeactivate()
            {
                DeactivateRequested(this, EventArgs.Empty);
            }

            public void RaiseBack()
            {
                BackRequested(this, EventArgs.Empty);
            }

            /// <summary>Gọi lại bước khởi tạo bằng cách xác thực trực tuyến, vì Render là private.</summary>
            public void RaiseValidateLocalRefresh(LicensePresenter presenter)
            {
                RaiseValidate();
            }
        }
    }
}
