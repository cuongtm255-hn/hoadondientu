using System;

namespace HoaDonDienTu.WinForms.Licensing
{
    /// <summary>Trạng thái SCR-12 đã định dạng sẵn để view chỉ việc hiển thị.</summary>
    public sealed class LicenseViewModel
    {
        public string ModeLetter { get; set; }
        public string ModeTitle { get; set; }
        public string ModeSubtitle { get; set; }
        public string Device { get; set; }
        public string MachineShort { get; set; }
        public string LicenseType { get; set; }
        public string ExpiresAt { get; set; }
        public string LastValidation { get; set; }
        public string Version { get; set; }
        public string Note { get; set; }

        /// <summary>Đã có licence trên máy: bật Xác thực ngay và Hủy kích hoạt.</summary>
        public bool HasLicense { get; set; }
    }

    public interface ILicenseView
    {
        event EventHandler ActivateRequested;
        event EventHandler ValidateRequested;
        event EventHandler DeactivateRequested;
        event EventHandler BackRequested;

        string LicenseKeyInput { get; }

        void ShowState(LicenseViewModel model);
        void SetBusy(bool busy);
        void ShowMessage(string text, bool isError);
        void ClearKeyInput();
    }
}
