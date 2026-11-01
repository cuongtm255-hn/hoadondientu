using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Common;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Licensing;
using HoaDonDienTu.Application.Navigation;
using HoaDonDienTu.Domain.Common;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.Domain.Licensing;
using HoaDonDienTu.WinForms.Settings;
using NUnit.Framework;

namespace HoaDonDienTu.WinForms.Tests
{
    [TestFixture]
    public class SettingsPresenterTests : CompanyUiTestBase
    {
        private FakeSettingsView _view;
        private FakeLicenseInfoSource _license;
        private SettingsPresenter _presenter;

        [SetUp]
        public void SetUp()
        {
            _view = new FakeSettingsView();
            _license = new FakeLicenseInfoSource();
            Entitlement.Value = AppMode.Standard;
            _presenter = new SettingsPresenter(_view, _license, Entitlement, Context, () => Nav, Runner, Errors, "1.1.0 · Schema v1");
        }

        [TearDown]
        public void TearDown()
        {
            _presenter.Dispose();
        }

        [Test]
        public void Initialize_NoCompany_ShowsHintAndStandardLicenceSummary()
        {
            _presenter.Initialize();

            Assert.That(_view.Section, Is.EqualTo(SettingsSection.ActiveCompany));
            Assert.That(_view.Rows, Is.Empty);
            Assert.That(_view.Note, Does.Contain("Chưa mở doanh nghiệp"));
            Assert.That(_view.LicenseSummary, Is.EqualTo("Chưa kích hoạt"));
        }

        [Test]
        public void ActiveCompany_ShowsNameTaxCodeAndFolder()
        {
            Context.Current = new CompanyContext(CompanyId.NewId(), TaxCode.Parse("0101234567"), "CÔNG TY MẪU", @"D:\Kho\0101234567", null);
            _presenter.Initialize();

            Assert.That(_view.Rows, Has.Count.EqualTo(3));
            Assert.That(_view.Rows[0].Value, Is.EqualTo("CÔNG TY MẪU"));
            Assert.That(_view.Rows[1].Value, Is.EqualTo("0101234567"));
            Assert.That(_view.Rows[2].Value, Is.EqualTo(@"D:\Kho\0101234567"));
        }

        [Test]
        public void About_ShowsVersionModeAndLicence()
        {
            Entitlement.Value = AppMode.Service;
            _license.Info = new LicenseInfo(LicenseStatus.Valid, LicenseIssue.None, "Service", "••••1234", null, null, null);
            _presenter.Initialize();

            _view.Select(SettingsSection.About);

            Assert.That(_view.Title, Is.EqualTo("Thông tin ứng dụng"));
            Assert.That(_view.Rows[0].Value, Is.EqualTo("1.1.0 · Schema v1"));
            Assert.That(_view.Rows[1].Value, Is.EqualTo("Chế độ dịch vụ"));
            Assert.That(_view.Rows[2].Value, Is.EqualTo("Đang hiệu lực ••••1234"));
        }

        [Test]
        public void ReadOnly_SummaryShowsRenewalHint()
        {
            Entitlement.Value = AppMode.ReadOnlyService;
            _presenter.Initialize();

            Assert.That(_view.LicenseSummary, Is.EqualTo("Chỉ đọc – cần gia hạn"));
        }

        [TestCase(SettingsSection.Storage)]
        [TestCase(SettingsSection.PortalConnection)]
        [TestCase(SettingsSection.Diagnostics)]
        public void PendingSections_SayTheyBelongToDd11_AndOfferNoRows(SettingsSection section)
        {
            _presenter.Initialize();

            _view.Select(section);

            Assert.That(_view.Section, Is.EqualTo(section));
            Assert.That(_view.Rows, Is.Empty);
            Assert.That(_view.Note, Does.Contain("DD-11"));
        }

        [Test]
        public void LicenseSection_NavigatesToScr12_AndKeepsCurrentSection()
        {
            _presenter.Initialize();

            _view.Select(SettingsSection.License);

            Assert.That(Nav.Targets, Is.EqualTo(new[] { ScreenId.License }));
            Assert.That(_view.Section, Is.EqualTo(SettingsSection.ActiveCompany));
        }

        [Test]
        public void Dispose_StopsListening()
        {
            _presenter.Initialize();
            _presenter.Dispose();

            _view.Select(SettingsSection.License);

            Assert.That(Nav.Targets, Is.Empty);
        }

        private sealed class FakeLicenseInfoSource : ILicenseService
        {
            public LicenseInfo Info = LicenseInfo.NotActivated;

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
                throw new NotSupportedException();
            }

            public Task<LicenseStatus> ValidateOnlineAsync(CancellationToken cancellationToken)
            {
                throw new NotSupportedException();
            }

            public Task<LicenseStatus> ActivateAsync(string licenseKey, CancellationToken cancellationToken)
            {
                throw new NotSupportedException();
            }

            public Task<LicenseStatus> DeactivateAsync(CancellationToken cancellationToken)
            {
                throw new NotSupportedException();
            }
        }

        private sealed class FakeSettingsView : ISettingsView
        {
            public SettingsSection Section;
            public string Title;
            public IList<KeyValuePair<string, string>> Rows;
            public string Note;
            public string LicenseSummary;

            public event EventHandler<SettingsSectionEventArgs> SectionSelected;

            public void ShowSection(SettingsSection section, string title, IList<KeyValuePair<string, string>> rows, string note)
            {
                Section = section;
                Title = title;
                Rows = rows;
                Note = note;
            }

            public void ShowLicenseSummary(string text)
            {
                LicenseSummary = text;
            }

            public void Select(SettingsSection section)
            {
                var handler = SectionSelected;
                if (handler != null) handler(this, new SettingsSectionEventArgs(section));
            }
        }
    }
}
