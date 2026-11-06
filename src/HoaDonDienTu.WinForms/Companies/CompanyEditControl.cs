using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using ComponentFactory.Krypton.Toolkit;
using HoaDonDienTu.Application.Companies;

namespace HoaDonDienTu.WinForms.Companies
{
    /// <summary>SCR-11 Thêm/sửa doanh nghiệp (cũng là màn hình khai báo lần đầu của StandardMode). Chỉ hiển thị và phát sự kiện.</summary>
    public partial class CompanyEditControl : KryptonPanel, ICompanyEditView
    {
        private readonly KryptonLabel[] _checkLabels;
        private bool _suppress;

        public CompanyEditControl()
        {
            InitializeComponent();
            HoaDonDienTu.WinForms.Common.UiIcons.Decorate(this);
            _checkLabels = new[] { lblCheck1, lblCheck2, lblCheck3, lblCheck4, lblCheck5 };
        }

        public event EventHandler InputChanged;
        public event EventHandler TaxCodeEdited;
        public event EventHandler BrowseRequested;
        public event EventHandler PrimaryRequested;
        public event EventHandler CancelRequested;

        public CompanyInput ReadInput()
        {
            return new CompanyInput
            {
                TaxCodeText = txtTaxCode.Text,
                DisplayName = txtDisplayName.Text,
                StoragePath = txtStoragePath.Text
            };
        }

        public void Configure(CompanyEditConfig config)
        {
            lblTitle.Values.Text = config.Title;
            lblSubtitle.Values.Text = config.Subtitle;
            btnPrimary.Values.Text = config.PrimaryText;
            lnkBreadcrumb.Visible = config.ShowBreadcrumb;
            btnCancel.Visible = config.ShowCancel;
            txtTaxCode.ReadOnly = config.TaxCodeReadOnly;
            txtStoragePath.ReadOnly = config.FolderReadOnly;
            btnBrowse.Enabled = !config.FolderReadOnly;
            lblSteps.Text = string.Join(Environment.NewLine, (config.AfterSteps ?? new string[0]).Select((s, i) => (i + 1) + ". " + s));
        }

        public void ShowValues(string taxCode, string displayName, string storagePath)
        {
            _suppress = true;
            try
            {
                txtTaxCode.Text = taxCode ?? string.Empty;
                txtDisplayName.Text = displayName ?? string.Empty;
                txtStoragePath.Text = storagePath ?? string.Empty;
            }
            finally
            {
                _suppress = false;
            }
        }

        public void SetStoragePath(string path)
        {
            // Đổi đường dẫn do chương trình (gợi ý theo MST, chọn thư mục) vẫn phải kích hoạt kiểm tra lại.
            txtStoragePath.Text = path ?? string.Empty;
        }

        public void ShowChecks(IList<CompanyCheck> checks)
        {
            for (var i = 0; i < _checkLabels.Length; i++)
            {
                var label = _checkLabels[i];
                if (i >= checks.Count)
                {
                    label.Visible = false;
                    continue;
                }

                var check = checks[i];
                label.Visible = true;
                label.Values.Text = (check.Passed ? "✓  " : "✕  ") + check.Message;
                label.StateCommon.ShortText.Color1 = check.Passed ? Color.FromArgb(30, 110, 60) : Color.FromArgb(170, 40, 30);
            }

            lblMessage.Text = string.Empty;
        }

        public void SetPrimaryEnabled(bool enabled)
        {
            btnPrimary.Enabled = enabled;
        }

        public void ShowMessage(string message)
        {
            lblMessage.Text = message ?? string.Empty;
        }

        private void OnTaxCodeTextChanged(object sender, EventArgs e)
        {
            if (_suppress) return;
            var edited = TaxCodeEdited;
            if (edited != null) edited(this, EventArgs.Empty);
            Raise(InputChanged);
        }

        private void OnInputTextChanged(object sender, EventArgs e)
        {
            if (_suppress) return;
            Raise(InputChanged);
        }

        private void OnBrowseClick(object sender, EventArgs e)
        {
            Raise(BrowseRequested);
        }

        private void OnPrimaryClick(object sender, EventArgs e)
        {
            Raise(PrimaryRequested);
        }

        private void OnCancelClick(object sender, EventArgs e)
        {
            Raise(CancelRequested);
        }

        private void Raise(EventHandler handler)
        {
            if (handler != null) handler(this, EventArgs.Empty);
        }
    }
}
