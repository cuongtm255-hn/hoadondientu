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

    /// <summary>Một nút thao tác dưới nội dung của mục (ví dụ "Tạo bản sao lưu").</summary>
    public sealed class SettingsAction
    {
        public SettingsAction(string id, string text, bool enabled = true, bool primary = false)
        {
            Id = id;
            Text = text;
            Enabled = enabled;
            Primary = primary;
        }

        public string Id { get; private set; }
        public string Text { get; private set; }
        public bool Enabled { get; private set; }
        public bool Primary { get; private set; }
    }

    public sealed class SettingsActionEventArgs : EventArgs
    {
        public SettingsActionEventArgs(string actionId)
        {
            ActionId = actionId;
        }

        public string ActionId { get; private set; }
    }

    public interface ISettingsView
    {
        event EventHandler<SettingsSectionEventArgs> SectionSelected;

        /// <summary>Người dùng bấm một nút thao tác do <see cref="ShowActions"/> dựng.</summary>
        event EventHandler<SettingsActionEventArgs> ActionRequested;

        /// <summary>Thay các nút thao tác của mục đang hiển thị.</summary>
        void ShowActions(IList<SettingsAction> actions);

        /// <summary>Kết quả hoặc tiến độ của thao tác gần nhất (xóa khi chuyển mục).</summary>
        void ShowMessage(string text, bool isError);

        /// <summary>Hiển thị nội dung của một mục: tiêu đề, các dòng "tên: giá trị" và ghi chú (có thể rỗng).</summary>
        void ShowSection(SettingsSection section, string title, IList<KeyValuePair<string, string>> rows, string note);

        /// <summary>Dòng tóm tắt bản quyền hiển thị cạnh mục Bản quyền ứng dụng.</summary>
        void ShowLicenseSummary(string text);
    }
}
