using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using ComponentFactory.Krypton.Toolkit;

namespace HoaDonDienTu.WinForms.Companies
{
    /// <summary>SCR-10 Quản lý doanh nghiệp. Chỉ hiển thị và phát sự kiện; mọi quyết định nằm ở presenter.</summary>
    public partial class CompanyListControl : KryptonPanel, ICompanyListView
    {
        private CompanyRowViewModel _contextRow;

        public CompanyListControl()
        {
            InitializeComponent();
            HoaDonDienTu.WinForms.Common.UiIcons.Decorate(this);
            cboStatusFilter.Items.AddRange(new object[] { "Mọi trạng thái", "Sẵn sàng", "Cần xử lý" });
            cboStatusFilter.SelectedIndex = 0;
        }

        public event EventHandler SearchRequested;
        public event EventHandler AddRequested;
        public event EventHandler<CompanyRowActionEventArgs> RowActionRequested;

        public string SearchText
        {
            get { return txtSearch.Text; }
        }

        public string StatusFilter
        {
            get
            {
                switch (cboStatusFilter.SelectedIndex)
                {
                    case 1: return "Active";
                    case 2: return "Attention";
                    default: return null;
                }
            }
        }

        public void ShowBanner(string text, bool readOnly)
        {
            lblBanner.Values.Text = text;
            pnlBanner.StateCommon.Color1 = readOnly ? Color.FromArgb(255, 244, 224) : Color.FromArgb(232, 245, 236);
            lblBanner.StateCommon.ShortText.Color1 = readOnly ? Color.FromArgb(130, 80, 10) : Color.FromArgb(30, 100, 60);
        }

        public void ShowAddEnabled(bool enabled)
        {
            btnAddCompany.Enabled = enabled;
        }

        public void ShowRows(IList<CompanyRowViewModel> rows, CompanyListStats stats)
        {
            gridCompanies.Rows.Clear();
            foreach (var row in rows)
            {
                var index = gridCompanies.Rows.Add(
                    row.Name + Environment.NewLine + row.StoragePath,
                    row.TaxCode,
                    row.LastOpened,
                    row.InvoiceCount,
                    row.Size,
                    row.StatusText,
                    row.PrimaryActionText);
                gridCompanies.Rows[index].Tag = row;
                if (row.Status != HoaDonDienTu.Application.Companies.CompanyCatalogStatus.Active)
                {
                    gridCompanies.Rows[index].Cells["colStatus"].Style.ForeColor = Color.FromArgb(170, 40, 30);
                }
            }

            lblStat1Value.Values.Text = stats.Total.ToString();
            lblStat1Hint.Values.Text = "Hiển thị " + stats.Shown + " trong " + stats.Total + " doanh nghiệp";
            lblStat2Value.Values.Text = stats.NeedAttention.ToString();
            lblStat3Value.Values.Text = stats.TotalSize;
            lblStat4Value.Values.Text = stats.CurrentTaxCode ?? "Chưa chọn";
            lblStat4Hint.Values.Text = stats.CurrentName ?? string.Empty;
            lblSummary.Values.Text = stats.Total == 0
                ? "Chưa có doanh nghiệp nào. Chọn \"Thêm doanh nghiệp\" để bắt đầu."
                : "Hiển thị " + stats.Shown + " trong " + stats.Total + " doanh nghiệp";
        }

        private void OnGridCellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex != colAction.Index) return;
            var row = gridCompanies.Rows[e.RowIndex].Tag as CompanyRowViewModel;
            if (row != null) RaiseRowAction(row, row.PrimaryAction);
        }

        private void OnGridCellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            // Chuột phải chọn dòng trước khi mở menu để menu tác động đúng doanh nghiệp.
            if (e.Button != MouseButtons.Right || e.RowIndex < 0) return;
            gridCompanies.ClearSelection();
            gridCompanies.Rows[e.RowIndex].Selected = true;
            _contextRow = gridCompanies.Rows[e.RowIndex].Tag as CompanyRowViewModel;
        }

        private void OnMenuOpenFolder(object sender, EventArgs e)
        {
            Raise(CompanyRowAction.OpenFolder);
        }

        private void OnMenuRename(object sender, EventArgs e)
        {
            Raise(CompanyRowAction.Rename);
        }

        private void OnMenuUnlink(object sender, EventArgs e)
        {
            Raise(CompanyRowAction.Unlink);
        }

        private void Raise(CompanyRowAction action)
        {
            if (_contextRow != null) RaiseRowAction(_contextRow, action);
        }

        private void RaiseRowAction(CompanyRowViewModel row, CompanyRowAction action)
        {
            var handler = RowActionRequested;
            if (handler != null) handler(this, new CompanyRowActionEventArgs(row.Id, action));
        }

        private void OnSearchClick(object sender, EventArgs e)
        {
            var handler = SearchRequested;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void OnSearchKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            OnSearchClick(sender, EventArgs.Empty);
        }

        private void OnAddClick(object sender, EventArgs e)
        {
            var handler = AddRequested;
            if (handler != null) handler(this, EventArgs.Empty);
        }
    }
}
