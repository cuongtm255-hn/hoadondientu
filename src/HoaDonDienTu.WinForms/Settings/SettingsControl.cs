using System;
using System.Collections.Generic;
using ComponentFactory.Krypton.Toolkit;

namespace HoaDonDienTu.WinForms.Settings
{
    /// <summary>SCR-09 Cài đặt. Chỉ hiển thị và phát sự kiện chọn mục.</summary>
    public partial class SettingsControl : KryptonPanel, ISettingsView
    {
        private readonly KryptonLabel[] _captions;
        private readonly KryptonLabel[] _values;
        private readonly KeyValuePair<KryptonButton, SettingsSection>[] _sections;

        public SettingsControl()
        {
            InitializeComponent();
            _captions = new[] { lblCap1, lblCap2, lblCap3 };
            _values = new[] { lblVal1, lblVal2, lblVal3 };
            _sections = new[]
            {
                Map(btnSecCompany, SettingsSection.ActiveCompany),
                Map(btnSecStorage, SettingsSection.Storage),
                Map(btnSecPortal, SettingsSection.PortalConnection),
                Map(btnSecDiagnostics, SettingsSection.Diagnostics),
                Map(btnSecLicense, SettingsSection.License),
                Map(btnSecAbout, SettingsSection.About)
            };
        }

        public event EventHandler<SettingsSectionEventArgs> SectionSelected;

        public void ShowSection(SettingsSection section, string title, IList<KeyValuePair<string, string>> rows, string note)
        {
            lblCardTitle.Values.Text = title;
            for (var i = 0; i < _captions.Length; i++)
            {
                var has = i < rows.Count;
                _captions[i].Values.Text = has ? rows[i].Key : string.Empty;
                _values[i].Values.Text = has ? rows[i].Value : string.Empty;
            }

            lblNote.Text = note ?? string.Empty;
            foreach (var pair in _sections)
            {
                pair.Key.Enabled = pair.Value != section;
            }
        }

        public void ShowLicenseSummary(string text)
        {
            btnSecLicense.Values.ExtraText = text ?? string.Empty;
        }

        private static KeyValuePair<KryptonButton, SettingsSection> Map(KryptonButton button, SettingsSection section)
        {
            return new KeyValuePair<KryptonButton, SettingsSection>(button, section);
        }

        private void OnSectionClick(object sender, EventArgs e)
        {
            var handler = SectionSelected;
            if (handler == null) return;
            foreach (var pair in _sections)
            {
                if (ReferenceEquals(pair.Key, sender))
                {
                    handler(this, new SettingsSectionEventArgs(pair.Value));
                    return;
                }
            }
        }
    }
}
