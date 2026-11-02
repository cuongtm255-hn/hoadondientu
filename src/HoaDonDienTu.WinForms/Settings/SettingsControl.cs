using System;
using System.Collections.Generic;
using System.Windows.Forms;
using ComponentFactory.Krypton.Toolkit;

namespace HoaDonDienTu.WinForms.Settings
{
    /// <summary>SCR-09 Cài đặt. Chỉ hiển thị và phát sự kiện chọn mục.</summary>
    public partial class SettingsControl : KryptonPanel, ISettingsView
    {
        private const int RowHeight = 30;
        private const int ActionsHeight = 54;
        private const int NoteHeight = 56;

        private readonly KeyValuePair<KryptonButton, SettingsSection>[] _sections;

        public SettingsControl()
        {
            InitializeComponent();
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
        public event EventHandler<SettingsActionEventArgs> ActionRequested;

        public void ShowSection(SettingsSection section, string title, IList<KeyValuePair<string, string>> rows, string note)
        {
            OnUi(delegate
            {
                lblCardTitle.Values.Text = title;
                BuildRows(rows);
                lblNote.Text = note ?? string.Empty;
                foreach (var pair in _sections)
                {
                    pair.Key.Enabled = pair.Value != section;
                }

                // Đổi mục thì thao tác và thông báo của mục trước không còn đúng.
                flowActions.Controls.Clear();
                lblMessage.Text = string.Empty;
                ResizeCard();
            });
        }

        public void ShowActions(IList<SettingsAction> actions)
        {
            OnUi(delegate
            {
                flowActions.SuspendLayout();
                var old = new List<Control>();
                foreach (Control c in flowActions.Controls) old.Add(c);
                flowActions.Controls.Clear();
                foreach (var c in old) c.Dispose();
                foreach (var a in actions)
                {
                    var button = new KryptonButton { Width = 150, Height = 36, Enabled = a.Enabled, Tag = a.Id, Margin = new Padding(0, 0, 8, 0) };
                    button.Values.Text = a.Text;
                    button.Name = "btnAction_" + a.Id;
                    button.Click += OnActionClick;
                    flowActions.Controls.Add(button);
                }

                flowActions.ResumeLayout(true);
                ResizeCard();
            });
        }

        public void ShowMessage(string text, bool isError)
        {
            OnUi(delegate
            {
                lblMessage.Text = text ?? string.Empty;
                lblMessage.ForeColor = isError ? System.Drawing.Color.FromArgb(170, 40, 30) : System.Drawing.Color.FromArgb(30, 110, 60);
            });
        }

        private void BuildRows(IList<KeyValuePair<string, string>> rows)
        {
            tlpRows.SuspendLayout();
            var old = new List<Control>();
            foreach (Control c in tlpRows.Controls) old.Add(c);
            tlpRows.Controls.Clear();
            foreach (var c in old) c.Dispose();
            tlpRows.RowStyles.Clear();
            tlpRows.RowCount = rows.Count;
            for (var i = 0; i < rows.Count; i++)
            {
                tlpRows.RowStyles.Add(new RowStyle(SizeType.Absolute, RowHeight));
                var caption = new System.Windows.Forms.Label
                {
                    Text = rows[i].Key, Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                    Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold), AutoEllipsis = true
                };
                var value = new System.Windows.Forms.Label
                {
                    Text = rows[i].Value, Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                    Font = new System.Drawing.Font("Segoe UI", 9F), AutoEllipsis = true
                };
                tlpRows.Controls.Add(caption, 0, i);
                tlpRows.Controls.Add(value, 1, i);
            }

            tlpRows.Height = rows.Count * RowHeight;
            tlpRows.ResumeLayout(true);
        }

        private void ResizeCard()
        {
            var actions = flowActions.Controls.Count > 0 ? ActionsHeight : 0;
            pnlCard.Height = 40 + tlpRows.Height + actions + lblMessage.Height + (string.IsNullOrEmpty(lblNote.Text) ? 0 : NoteHeight) + 24;
        }

        private void OnUi(Action action)
        {
            if (IsDisposed) return;
            try
            {
                if (IsHandleCreated && InvokeRequired) BeginInvoke(action);
                else action();
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        private void OnActionClick(object sender, EventArgs e)
        {
            var button = sender as KryptonButton;
            var handler = ActionRequested;
            if (button != null && button.Tag is string && handler != null) handler(this, new SettingsActionEventArgs((string)button.Tag));
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
