using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ComponentFactory.Krypton.Toolkit;
using HoaDonDienTu.WinForms.Common;

namespace HoaDonDienTu.WinForms.Companies
{
    /// <summary>Màn hình khôi phục danh mục doanh nghiệp. Chỉ hiển thị và phát sự kiện; bố cục dựng bằng mã (không có Designer).</summary>
    public sealed class RecoveryControl : KryptonPanel, IRecoveryView
    {
        private static readonly Color Good = Color.FromArgb(30, 110, 60);
        private static readonly Color Bad = Color.FromArgb(170, 40, 30);

        private readonly KryptonLabel _title = new KryptonLabel();
        private readonly Label _intro = new Label();
        private readonly KryptonButton _scan = new KryptonButton();
        private readonly KryptonButton _browse = new KryptonButton();
        private readonly KryptonButton _rebuild = new KryptonButton();
        private readonly KryptonButton _restart = new KryptonButton();
        private readonly ListView _list = new ListView();
        private readonly Label _message = new Label();
        private bool _busy;

        public RecoveryControl()
        {
            _title.Values.Text = "Cần khôi phục";
            _title.StateCommon.ShortText.Font = new Font("Segoe UI", 18F, FontStyle.Bold);
            _title.Location = new Point(14, 8);
            _title.AutoSize = true;

            _intro.Location = new Point(18, 52);
            _intro.Size = new Size(900, 54);
            _intro.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            _intro.Font = new Font("Segoe UI", 9.5F);
            _intro.BackColor = Color.Transparent;

            ConfigureButton(_scan, "btnScan", "Quét lại", 18, 112, 120);
            ConfigureButton(_browse, "btnBrowseFolder", "Chọn thư mục khác...", 146, 112, 190);
            ConfigureButton(_rebuild, "btnRebuild", "Đăng ký các kho đã chọn", 344, 112, 210);
            ConfigureButton(_restart, "btnRestart", "Khởi động lại ứng dụng", 344, 112, 210);
            _rebuild.Enabled = false;
            _restart.Visible = false;

            _list.Location = new Point(18, 160);
            _list.Size = new Size(900, 300);
            _list.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _list.View = View.Details;
            _list.CheckBoxes = true;
            _list.FullRowSelect = true;
            _list.GridLines = true;
            _list.Name = "listStores";
            _list.Columns.Add("Thư mục kho", 520);
            _list.Columns.Add("MST", 130);
            _list.Columns.Add("Trạng thái", 240);
            _list.ItemChecked += delegate { RefreshRebuildButton(); };

            _message.Location = new Point(18, 468);
            _message.Size = new Size(900, 54);
            _message.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            _message.Font = new Font("Segoe UI", 9.5F);
            _message.BackColor = Color.Transparent;
            _message.Name = "lblMessage";

            Controls.AddRange(new Control[] { _title, _intro, _scan, _browse, _rebuild, _restart, _list, _message });
            UiIcons.Decorate(this);
        }

        public event EventHandler ScanRequested;
        public event EventHandler BrowseRequested;
        public event EventHandler RebuildRequested;
        public event EventHandler RestartRequested;

        public IList<string> SelectedPaths
        {
            get { return _list.CheckedItems.Cast<ListViewItem>().Select(i => i.Text).ToList(); }
        }

        public void ShowIntro(string text)
        {
            OnUi(delegate { _intro.Text = text ?? string.Empty; });
        }

        public void ShowStores(IList<RecoveryRow> rows)
        {
            OnUi(delegate
            {
                _list.BeginUpdate();
                try
                {
                    _list.Items.Clear();
                    foreach (var row in rows)
                    {
                        var item = new ListViewItem(row.Path) { Checked = row.CanRegister };
                        item.SubItems.Add(row.TaxCode);
                        item.SubItems.Add(row.Status);
                        if (!row.CanRegister) item.ForeColor = SystemColors.GrayText;
                        _list.Items.Add(item);
                    }
                }
                finally
                {
                    _list.EndUpdate();
                }

                RefreshRebuildButton();
            });
        }

        public void ShowMessage(string text, bool isError)
        {
            OnUi(delegate
            {
                _message.Text = text ?? string.Empty;
                _message.ForeColor = isError ? Bad : Good;
            });
        }

        public void SetBusy(bool busy)
        {
            OnUi(delegate
            {
                _busy = busy;
                _scan.Enabled = !busy;
                _browse.Enabled = !busy;
                RefreshRebuildButton();
            });
        }

        public void ShowRestart(bool visible)
        {
            OnUi(delegate
            {
                _restart.Visible = visible;
                _rebuild.Visible = !visible;
                _scan.Enabled = !visible;
                _browse.Enabled = !visible;
            });
        }

        private void RefreshRebuildButton()
        {
            _rebuild.Enabled = !_busy && _list.CheckedItems.Count > 0;
        }

        private void ConfigureButton(KryptonButton button, string name, string text, int x, int y, int width)
        {
            button.Name = name;
            button.Values.Text = text;
            button.Location = new Point(x, y);
            button.Size = new Size(width, 36);
            button.Click += OnButtonClick;
        }

        private void OnButtonClick(object sender, EventArgs e)
        {
            EventHandler handler = null;
            if (ReferenceEquals(sender, _scan)) handler = ScanRequested;
            else if (ReferenceEquals(sender, _browse)) handler = BrowseRequested;
            else if (ReferenceEquals(sender, _rebuild)) handler = RebuildRequested;
            else if (ReferenceEquals(sender, _restart)) handler = RestartRequested;
            if (handler != null) handler(this, EventArgs.Empty);
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
    }
}
