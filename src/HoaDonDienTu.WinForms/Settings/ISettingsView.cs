using System;
using System.Collections.Generic;

namespace HoaDonDienTu.WinForms.Settings
{
    public enum SettingsSection
    {
        ActiveCompany,
        Storage,
        PortalConnection,
        Diagnostics,
        License,
        About
    }

    public sealed class SettingsSectionEventArgs : EventArgs
    {
        public SettingsSectionEventArgs(SettingsSection section)
        {
            Section = section;
        }

        public SettingsSection Section { get; private set; }
    }

    public interface ISettingsView
    {
        event EventHandler<SettingsSectionEventArgs> SectionSelected;

        /// <summary>Hiển thị nội dung của một mục: tiêu đề, các dòng "tên: giá trị" và ghi chú (có thể rỗng).</summary>
        void ShowSection(SettingsSection section, string title, IList<KeyValuePair<string, string>> rows, string note);

        /// <summary>Dòng tóm tắt bản quyền hiển thị cạnh mục Bản quyền ứng dụng.</summary>
        void ShowLicenseSummary(string text);
    }
}
