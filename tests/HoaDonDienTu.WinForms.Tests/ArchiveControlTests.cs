using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using ComponentFactory.Krypton.Toolkit;
using HoaDonDienTu.Application.Invoices;
using HoaDonDienTu.WinForms.Archive;
using NUnit.Framework;

namespace HoaDonDienTu.WinForms.Tests
{
    /// <summary>Hành vi chọn nhiều, ẩn/hiện cột và phím tắt của lưới SCR-05 (control thật trong một form, dữ liệu giả, không cổng Thuế).</summary>
    [TestFixture]
    [Apartment(System.Threading.ApartmentState.STA)]
    public class ArchiveControlTests
    {
        private Form _form;
        private ArchiveControl _control;
        private KryptonDataGridView _grid;
        private ContextMenuStrip _menu;
        private List<SelectionChangedEventArgs> _selections;
        private int _columnsChanged;

        [SetUp]
        public void SetUp()
        {
            _control = new ArchiveControl();
            _form = new Form { Width = 1100, Height = 700 };
            _control.Dock = DockStyle.Fill;
            _form.Controls.Add(_control);
            _form.Show();
            _grid = (KryptonDataGridView)Field("gridInvoices");
            _menu = (ContextMenuStrip)Field("_columnMenu");
            _selections = new List<SelectionChangedEventArgs>();
            _columnsChanged = 0;
            _control.SelectionChanged += (s, e) => _selections.Add(e);
            _control.ColumnsChanged += (s, e) => _columnsChanged++;
            _control.ShowResult(Model(3));
        }

        [TearDown]
        public void TearDown()
        {
            _form.Close();
            _form.Dispose();
        }

        private object Field(string name)
        {
            return typeof(ArchiveControl).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_control);
        }

        private static ArchiveViewModel Model(int rows)
        {
            var list = new List<ArchiveRowViewModel>();
            for (var i = 1; i <= rows; i++)
            {
                list.Add(new ArchiveRowViewModel
                {
                    InvoiceId = Guid.NewGuid(), Number = i.ToString(), IssueDate = "01/01/2026", CounterpartyId = "0100", CounterpartyName = "Đối tác " + i, FormSymbol = "1",
                    InvoiceSymbol = "C26TAA", InvoiceNumber = i.ToString("0000000"), Status = "Hóa đơn mới", FileText = "XML OK", FileState = ArchiveFileState.Ok, BeforeTax = "100", Tax = "10", Payment = "110"
                });
            }

            return new ArchiveViewModel
            {
                CounterpartyIdCaption = "MST người bán", CounterpartyNameCaption = "Tên người bán", ResultCount = rows.ToString(), ResultSub = "x", TotalBeforeTax = "1", TotalTax = "1", TotalPayment = "1",
                FooterText = "Hiển thị 1–" + rows + " / " + rows, PageText = "Trang 1/1", Rows = list
            };
        }

        private void Press(Keys key, bool control = false, bool shift = false)
        {
            var method = typeof(ArchiveControl).GetMethod("OnGridKeyDown", BindingFlags.Instance | BindingFlags.NonPublic);
            var data = key | (control ? Keys.Control : Keys.None) | (shift ? Keys.Shift : Keys.None);
            method.Invoke(_control, new object[] { _grid, new KeyEventArgs(data) });
        }

        private ToolStripMenuItem MenuItem(string column)
        {
            return _menu.Items.Cast<ToolStripItem>().OfType<ToolStripMenuItem>().Single(i => i.Name == "mnuColumn_" + column);
        }

        private List<Guid> Ids()
        {
            return _grid.Rows.Cast<DataGridViewRow>().Select(r => ((ArchiveRowViewModel)r.Tag).InvoiceId).ToList();
        }

        // ---- Chọn nhiều ----

        [Test]
        public void SelectingRows_ReportsTheirInvoicesInDisplayOrder()
        {
            var ids = Ids();
            _selections.Clear();

            _grid.Rows[2].Selected = true;
            _grid.Rows[0].Selected = true;

            var last = _selections.Last().InvoiceIds;
            Assert.That(last, Is.EqualTo(new[] { ids[0], ids[2] }), "Theo thứ tự hiển thị trên lưới");
        }

        [Test]
        public void SelectionCount_AppearsOnTheExportButtonsAndTheFooter()
        {
            _control.ShowSelectionCount(2);

            Assert.That(((KryptonButton)Field("btnExportExcel")).Values.Text, Is.EqualTo("Xuất Excel (2)"));
            Assert.That(((KryptonButton)Field("btnExportZip")).Values.Text, Is.EqualTo("Xuất ZIP XML (2)"));
            Assert.That(((KryptonLabel)Field("lblFooter")).Values.Text, Does.EndWith("Đã chọn 2"));

            _control.ShowSelectionCount(0);

            Assert.That(((KryptonButton)Field("btnExportExcel")).Values.Text, Is.EqualTo("Xuất Excel"));
            Assert.That(((KryptonLabel)Field("lblFooter")).Values.Text, Does.Not.Contain("Đã chọn"));
        }

        [Test]
        public void ShowingNewResults_ReportsAnEmptySelectionOnce()
        {
            _grid.Rows[1].Selected = true;
            _selections.Clear();

            _control.ShowResult(Model(2));

            Assert.That(_selections.Count, Is.EqualTo(1));
            Assert.That(_selections[0].InvoiceIds, Is.Empty);
            Assert.That(_grid.SelectedRows.Count, Is.EqualTo(0));
        }

        [Test]
        public void Escape_ClearsTheSelection()
        {
            _grid.Rows[0].Selected = true;
            _grid.Rows[1].Selected = true;

            Press(Keys.Escape);

            Assert.That(_grid.SelectedRows.Count, Is.EqualTo(0));
            Assert.That(_selections.Last().InvoiceIds, Is.Empty);
        }

        [Test]
        public void SelectAll_IsAvailableForTheWholePage_ByTheGridItself()
        {
            Assert.That(_grid.MultiSelect, Is.True);
            Assert.That(_grid.SelectionMode, Is.EqualTo(DataGridViewSelectionMode.FullRowSelect));

            _grid.SelectAll();

            Assert.That(_selections.Last().InvoiceIds.Count, Is.EqualTo(3));
        }

        [Test]
        public void Copy_IsEnabled_IncludingHeaders_SoCtrlCWorksOnTheSelectedRows()
        {
            Assert.That(_grid.ClipboardCopyMode, Is.EqualTo(DataGridViewClipboardCopyMode.EnableWithAutoHeaderText));
        }

        // ---- Phím tắt ----

        [Test]
        public void Enter_OpensTheDetailOfTheCurrentRow_AndIsSwallowed()
        {
            var requested = new List<Guid>();
            _control.InvoiceRequested += (s, e) => requested.Add(e.InvoiceId);
            _grid.CurrentCell = _grid.Rows[1].Cells[0];
            var args = new KeyEventArgs(Keys.Enter);
            typeof(ArchiveControl).GetMethod("OnGridKeyDown", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_control, new object[] { _grid, args });

            Assert.That(requested, Is.EqualTo(new[] { Ids()[1] }));
            Assert.That(args.Handled, Is.True, "Enter không nhảy xuống dòng kế");
        }

        [Test]
        public void CtrlE_ExportsToExcel_AndCtrlShiftE_ToZip_OnlyWhenAllowed()
        {
            var excel = 0;
            var zip = 0;
            _control.ExportExcelRequested += (s, e) => excel++;
            _control.ExportZipRequested += (s, e) => zip++;

            _control.ShowExportAllowed(false);
            Press(Keys.E, control: true);
            Press(Keys.E, control: true, shift: true);
            Assert.That(excel + zip, Is.EqualTo(0), "Bị khóa thì phím tắt cũng không chạy");

            _control.ShowExportAllowed(true);
            Press(Keys.E, control: true);
            Press(Keys.E, control: true, shift: true);
            Assert.That(excel, Is.EqualTo(1));
            Assert.That(zip, Is.EqualTo(1));
        }

        // ---- Ẩn/hiện cột ----

        [Test]
        public void ColumnMenu_ListsEveryColumn_AllCheckedAtFirst()
        {
            var items = _menu.Items.Cast<ToolStripItem>().OfType<ToolStripMenuItem>().Where(i => i.Tag is string).ToList();

            Assert.That(items.Count, Is.EqualTo(_grid.Columns.Count));
            Assert.That(items.All(i => i.Checked), Is.True);
            Assert.That(_control.HiddenColumns, Is.Empty);
            foreach (DataGridViewColumn c in _grid.Columns) Assert.That(c.HeaderCell.ContextMenuStrip, Is.SameAs(_menu), c.Name);
        }

        [Test]
        public void UncheckingAColumn_HidesIt_AndReportsTheChange()
        {
            var item = MenuItem("colTax");

            item.PerformClick();

            Assert.That(_grid.Columns["colTax"].Visible, Is.False);
            Assert.That(_control.HiddenColumns, Is.EqualTo(new[] { "colTax" }));
            Assert.That(_columnsChanged, Is.EqualTo(1));

            item.PerformClick();

            Assert.That(_grid.Columns["colTax"].Visible, Is.True);
            Assert.That(_control.HiddenColumns, Is.Empty);
            Assert.That(_columnsChanged, Is.EqualTo(2));
        }

        [Test]
        public void TheLastVisibleColumn_CannotBeHidden()
        {
            foreach (DataGridViewColumn c in _grid.Columns.Cast<DataGridViewColumn>().Skip(1)) MenuItem(c.Name).PerformClick();
            Assert.That(_grid.Columns.GetColumnCount(DataGridViewElementStates.Visible), Is.EqualTo(1));
            var before = _columnsChanged;

            MenuItem("colIssueDate").PerformClick();

            Assert.That(_grid.Columns["colIssueDate"].Visible, Is.True);
            Assert.That(MenuItem("colIssueDate").Checked, Is.True);
            Assert.That(_columnsChanged, Is.EqualTo(before), "Không đổi gì nên không báo");
        }

        [Test]
        public void ShowAll_BringsEveryColumnBack()
        {
            MenuItem("colTax").PerformClick();
            MenuItem("colForm").PerformClick();

            _menu.Items.Cast<ToolStripItem>().Single(i => i.Name == "mnuColumn_all").PerformClick();

            Assert.That(_control.HiddenColumns, Is.Empty);
            Assert.That(MenuItem("colTax").Checked, Is.True);
        }

        [Test]
        public void RestoringHiddenColumns_SyncsTheMenu_WithoutRaisingTheEvent()
        {
            _control.ShowHiddenColumns(new[] { "colTax", "colPayment" });

            Assert.That(_grid.Columns["colTax"].Visible, Is.False);
            Assert.That(_grid.Columns["colPayment"].Visible, Is.False);
            Assert.That(MenuItem("colTax").Checked, Is.False);
            Assert.That(MenuItem("colPayment").Checked, Is.False);
            Assert.That(MenuItem("colNumber").Checked, Is.True);
            Assert.That(_columnsChanged, Is.EqualTo(0));
        }

        [Test]
        public void RestoringEverythingHidden_StillKeepsOneColumnVisible()
        {
            _control.ShowHiddenColumns(_grid.Columns.Cast<DataGridViewColumn>().Select(c => c.Name).ToList());

            Assert.That(_grid.Columns.GetColumnCount(DataGridViewElementStates.Visible), Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void ColumnVisibility_SurvivesNewResults()
        {
            MenuItem("colTax").PerformClick();

            _control.ShowResult(Model(5));

            Assert.That(_grid.Columns["colTax"].Visible, Is.False);
            Assert.That(_grid.Rows.Count, Is.EqualTo(5));
        }
    }
}
