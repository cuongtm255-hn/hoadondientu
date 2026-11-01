namespace HoaDonDienTu.WinForms.Shell
{
    public sealed class ShellModeViewState
    {
        public bool ShowCompanySelector { get; set; }
        public bool ShowCompanyMenu { get; set; }
        public bool CanPortalLogin { get; set; }
        public bool CanDownload { get; set; }
        public bool CanAddCompany { get; set; }
        public string LicenseLabel { get; set; }
    }

    public sealed class CompanyHeaderViewState
    {
        public string CompanyName { get; set; }
        public string TaxCode { get; set; }
    }

    public sealed class PortalSessionViewState
    {
        public bool IsLoggedIn { get; set; }
        public string Text { get; set; }
    }

    public sealed class OperationViewState
    {
        public string Text { get; set; }
        public bool IsBusy { get; set; }
        public int Percent { get; set; }
    }
}
