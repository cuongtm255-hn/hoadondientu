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

        /// <summary>Mã máy đầy đủ để gửi cho đơn vị phát hành.</summary>
        public string MachineCode { get; set; }
        public string LicenseType { get; set; }
        public string ExpiresAt { get; set; }

        /// <summary>Dòng "Kích hoạt lúc ..." hoặc "Chưa kích hoạt".</summary>
        public string LastValidation { get; set; }
        public string Version { get; set; }
        public string Note { get; set; }
    }

    public interface ILicenseView
    {
        event EventHandler ActivateRequested;
        event EventHandler BackRequested;

        string LicenseKeyInput { get; }

        void ShowState(LicenseViewModel model);
        void SetBusy(bool busy);
        void ShowMessage(string text, bool isError);
        void ClearKeyInput();
    }
}
