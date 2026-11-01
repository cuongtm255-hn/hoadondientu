using HoaDonDienTu.Application.Startup;
using HoaDonDienTu.Domain.Common;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.Domain.Licensing;
using HoaDonDienTu.WinForms.Shell;
using NUnit.Framework;

namespace HoaDonDienTu.WinForms.Tests
{
    [TestFixture]
    public class ShellModeMapperTests
    {
        private static StartupResult Result(AppMode mode, StartupRoute route, CompanyProfile company = null)
        {
            return new StartupResult(mode, route, LicenseStatus.None, company, null, RecoveryReason.None);
        }

        [Test]
        public void Standard_HidesCompanySelectorAndMenu_AllowsLoginAndDownload()
        {
            var state = ShellModeMapper.ToModeState(Result(AppMode.Standard, StartupRoute.StandardPortalLogin));

            Assert.That(state.ShowCompanySelector, Is.False);
            Assert.That(state.ShowCompanyMenu, Is.False);
            Assert.That(state.CanAddCompany, Is.False);
            Assert.That(state.CanPortalLogin, Is.True);
            Assert.That(state.CanDownload, Is.True);
        }

        [Test]
        public void Service_ShowsCompanySelectorAndMenu_CapableOfLoginAndDownload_OncePresenterSeesACompany()
        {
            var state = ShellModeMapper.ToModeState(Result(AppMode.Service, StartupRoute.ServiceCompanySelection));

            Assert.That(state.ShowCompanySelector, Is.True);
            Assert.That(state.ShowCompanyMenu, Is.True);
            Assert.That(state.CanAddCompany, Is.True);
            Assert.That(state.CanPortalLogin, Is.True, "Khả năng theo chế độ; MainShellPresenter khóa khi chưa mở doanh nghiệp");
            Assert.That(state.CanDownload, Is.True);
        }

        [Test]
        public void ReadOnly_LocksLoginDownloadAndAddCompany()
        {
            var state = ShellModeMapper.ToModeState(Result(AppMode.ReadOnlyService, StartupRoute.ReadOnlyCompanySelection));

            Assert.That(state.ShowCompanySelector, Is.True);
            Assert.That(state.CanPortalLogin, Is.False);
            Assert.That(state.CanDownload, Is.False);
            Assert.That(state.CanAddCompany, Is.False);
        }

        [Test]
        public void Recovery_DisablesEverything()
        {
            var state = ShellModeMapper.ToModeState(Result(AppMode.Standard, StartupRoute.Recovery));

            Assert.That(state.CanPortalLogin, Is.False);
            Assert.That(state.CanDownload, Is.False);
            Assert.That(state.ShowCompanySelector, Is.False);
        }

        [Test]
        public void CompanyHeader_StandardShowsCompany_ServiceShowsPlaceholder()
        {
            var company = new CompanyProfile(CompanyId.NewId(), "CÔNG TY MẪU", TaxCode.Parse("0101234567"));

            var standard = ShellModeMapper.ToCompanyState(Result(AppMode.Standard, StartupRoute.StandardPortalLogin, company));
            var service = ShellModeMapper.ToCompanyState(Result(AppMode.Service, StartupRoute.ServiceCompanySelection));

            Assert.That(standard.CompanyName, Is.EqualTo("CÔNG TY MẪU"));
            Assert.That(standard.TaxCode, Is.EqualTo("0101234567"));
            Assert.That(service.CompanyName, Is.EqualTo("Chưa chọn doanh nghiệp"));
            Assert.That(service.TaxCode, Is.Empty);
        }
    }
}
