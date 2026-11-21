using System;
using System.Collections.Generic;
using System.IO;
using System.Drawing;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using ComponentFactory.Krypton.Toolkit;
using HoaDonDienTu.Application.PublicService;
using HoaDonDienTu.Domain.PublicService;
using HoaDonDienTu.WinForms.Common;

namespace HoaDonDienTu.WinForms.Groups
{
    /// <summary>
    /// Màn hình tra cứu danh mục thủ tục hành chính (FT-DVC-02): bộ lọc, bảng có phân trang, chi tiết. Chỉ đọc, không có "Nộp hồ sơ".
    /// Dựng bằng mã, không có Designer. Logic tìm/phân trang nằm ở <see cref="TthcSearchSession"/>.
    /// </summary>
    public sealed class TthcForm : KryptonForm
    {
        private readonly TthcSearchSession _session;
        private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();
        private readonly KryptonTextBox _keyword = new KryptonTextBox();
        private readonly KryptonComboBox _procedureType = NewCombo();
        private readonly KryptonComboBox _declaration = NewCombo();
        private readonly KryptonComboBox _level = NewCombo();
        private readonly KryptonComboBox _taxpayer = NewCombo();
        private readonly KryptonButton _search = new KryptonButton();
        private readonly DataGridView _grid = new DataGridView();
        private readonly KryptonButton _prev = new KryptonButton();
        private readonly KryptonButton _next = new KryptonButton();
        private readonly KryptonTextBox _pageBox = new KryptonTextBox();
        private readonly KryptonButton _go = new KryptonButton();
        private readonly KryptonButton _exportPage = new KryptonButton();
        private readonly KryptonButton _exportAll = new KryptonButton();
        private readonly Label _summary = new Label();
        private readonly Label _status = new Label();

        public TthcForm(TthcSearchSession session, string notice)
        {
            _session = session;
            Text = "Tra cứu thủ tục hành chính"
#if DEBUG
                + " — [TEST] DỮ LIỆU GIẢ"
#endif
                ;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(960, 600);
            MinimumSize = new Size(820, 480);

            var filters = new Panel { Dock = DockStyle.Top, Height = 92 };
            AddField(filters, "Tên / Mã TTHC", _keyword, 10, 8, 240);
            AddField(filters, "Loại TTHC", _procedureType, 260, 8, 170);
            AddField(filters, "Tờ khai", _declaration, 440, 8, 150);
            AddField(filters, "Mức độ", _level, 600, 8, 120);
            AddField(filters, "Loại người nộp thuế", _taxpayer, 730, 8, 160);
            _search.SetBounds(10, 54, 110, 32);
            _search.Values.Text = "Tìm";
            UiIcons.Apply(_search, UiIcon.Search);
            _search.Click += async (s, e) => await SearchAsync();
            filters.Controls.Add(_search);
            AcceptButton = _search;
            _status.SetBounds(132, 60, 808, 24);
            _status.Text = notice ?? string.Empty;
            _status.ForeColor = Color.DimGray;
            filters.Controls.Add(_status);

            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.RowHeadersVisible = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.BackgroundColor = SystemColors.Window;
            _grid.Columns.Add("stt", "STT");
            _grid.Columns.Add("code", "Mã");
            _grid.Columns.Add("name", "Tên thủ tục");
            _grid.Columns.Add("level", "Mức độ");
            _grid.Columns.Add("agency", "Cơ quan thực hiện");
            _grid.Columns["stt"].FillWeight = 8;
            _grid.Columns["code"].FillWeight = 14;
            _grid.Columns["name"].FillWeight = 46;
            _grid.Columns["level"].FillWeight = 14;
            _grid.Columns["agency"].FillWeight = 28;
            _grid.CellDoubleClick += async (s, e) => { if (e.RowIndex >= 0) await OpenDetailAsync(e.RowIndex); };

            var pager = new Panel { Dock = DockStyle.Bottom, Height = 44 };
            _summary.SetBounds(10, 12, 360, 22);
            _prev.SetBounds(380, 6, 70, 30);
            _prev.Values.Text = "◀ Trước";
            _prev.Click += async (s, e) => await GoToAsync(_session.Page.PageNumber - 1);
            _next.SetBounds(456, 6, 70, 30);
            _next.Values.Text = "Sau ▶";
            _next.Click += async (s, e) => await GoToAsync(_session.Page.PageNumber + 1);
            _pageBox.SetBounds(540, 8, 60, 27);
            _go.SetBounds(606, 6, 50, 30);
            _go.Values.Text = "Đi";
            _go.Click += async (s, e) =>
            {
                int n;
                if (int.TryParse(_pageBox.Text.Trim(), out n)) await GoToAsync(n);
            };
            // Xuất Excel (FT-DVC-08): trang đang xem hoặc tất cả các trang của bộ lọc đã áp dụng.
            _exportPage.SetBounds(690, 6, 120, 32);
            _exportPage.Values.Text = "Xuất trang này";
            UiIcons.Apply(_exportPage, UiIcon.Excel);
            _exportPage.Click += async (s, e) => await ExportAsync(false);
            _exportAll.SetBounds(818, 6, 130, 32);
            _exportAll.Values.Text = "Xuất tất cả";
            UiIcons.Apply(_exportAll, UiIcon.Excel);
            _exportAll.Click += async (s, e) => await ExportAsync(true);
            pager.Controls.Add(_exportPage);
            pager.Controls.Add(_exportAll);
            pager.Controls.Add(_summary);
            pager.Controls.Add(_prev);
            pager.Controls.Add(_next);
            pager.Controls.Add(_pageBox);
            pager.Controls.Add(_go);

            Controls.Add(_grid);
            Controls.Add(pager);
            Controls.Add(filters);
            UpdatePager();

            Shown += async (s, e) => await LoadOptionsAsync();
            FormClosed += (s, e) => _cancellation.Cancel();
        }

        private static KryptonComboBox NewCombo()
        {
            return new KryptonComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        }

        private static void AddField(Control parent, string label, Control input, int x, int y, int width)
        {
            parent.Controls.Add(new Label { Text = label, AutoSize = true, Location = new Point(x, y) });
            input.SetBounds(x, y + 20, width, 27);
            parent.Controls.Add(input);
        }

        private static void Fill(KryptonComboBox combo, System.Collections.Generic.IList<TthcOption> options)
        {
            combo.Items.Clear();
            combo.Items.Add(new TthcOption(string.Empty, "(Tất cả)"));
            foreach (var option in options) combo.Items.Add(option);
            combo.SelectedIndex = 0;
        }

        private static string CodeOf(KryptonComboBox combo)
        {
            var option = combo.SelectedItem as TthcOption;
            return option == null || option.Code.Length == 0 ? null : option.Code;
        }

        private async System.Threading.Tasks.Task LoadOptionsAsync()
        {
            await _session.LoadOptionsAsync(_cancellation.Token);
            if (IsDisposed) return;
            Fill(_procedureType, _session.Options.ProcedureTypes);
            Fill(_declaration, _session.Options.Declarations);
            Fill(_level, _session.Options.Levels);
            Fill(_taxpayer, _session.Options.TaxpayerTypes);
        }

        private async System.Threading.Tasks.Task SearchAsync()
        {
            var query = new TthcQuery
            {
                Keyword = _keyword.Text.Trim(),
                ProcedureType = CodeOf(_procedureType),
                Declaration = CodeOf(_declaration),
                Level = CodeOf(_level),
                TaxpayerType = CodeOf(_taxpayer)
            };
            await RunAsync(() => _session.SearchAsync(query, _cancellation.Token));
        }

        private System.Threading.Tasks.Task GoToAsync(int page)
        {
            return RunAsync(() => _session.GoToPageAsync(page, _cancellation.Token));
        }

        private async System.Threading.Tasks.Task RunAsync(Func<System.Threading.Tasks.Task<bool>> action)
        {
            if (_session.IsBusy) return;
            UseWaitCursor = true;
            try
            {
                bool ok = await action();
                if (IsDisposed) return;
                if (ok) ShowPage();
                else if (_session.LastFailure != TthcFailure.None) ShowError();
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                if (!IsDisposed) UseWaitCursor = false;
            }
        }

        private void ShowPage()
        {
            _status.ForeColor = Color.DimGray;
            _status.Text = _session.Page.Items.Count == 0 ? "Không tìm thấy thủ tục phù hợp." : string.Empty;
            _grid.Rows.Clear();
            int stt = (_session.Page.PageNumber - 1) * TthcPage.DefaultPageSize;
            _grid.Columns["stt"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            _grid.Columns["stt"].Width = 52;
            foreach (var item in _session.Page.Items)
            {
                _grid.Rows.Add(++stt, item.Code, item.Name, item.Level, item.Agency);
            }
            UpdatePager();
        }

        private void ShowError()
        {
            _status.ForeColor = Color.Firebrick;
            _status.Text = TthcService.MessageFor(_session.LastFailure);
        }

        private void UpdatePager()
        {
            var page = _session.Page;
            if (page == null)
            {
                _summary.Text = "Chưa tìm. Nhập điều kiện rồi bấm Tìm.";
                _prev.Enabled = _next.Enabled = _go.Enabled = _pageBox.Enabled = false;
                _exportPage.Enabled = _exportAll.Enabled = false;
                return;
            }
            _summary.Text = "Trang " + page.PageNumber + "/" + Math.Max(1, page.PageCount)
                + " — Tổng số bản ghi: " + page.TotalRecords + " (" + page.TotalProcedures + " TTHC)";
            _prev.Enabled = page.PageNumber > 1;
            _next.Enabled = page.PageNumber < page.PageCount;
            _go.Enabled = _pageBox.Enabled = page.PageCount > 1;
            _exportPage.Enabled = page.Items.Count > 0;
            _exportAll.Enabled = page.Items.Count > 0;
            _pageBox.Text = page.PageNumber.ToString();
        }

        /// <summary>Xuất Excel: một trang đang xem, hoặc tất cả các trang (tải tuần tự, có nghỉ giữa các trang, hủy được bằng cách đóng màn hình).</summary>
        private async System.Threading.Tasks.Task ExportAsync(bool allPages)
        {
            if (_session.IsBusy || _session.Page == null) return;

            string path;
            using (var dialog = new SaveFileDialog
            {
                Title = "Xuất danh mục thủ tục hành chính ra Excel",
                Filter = "Bảng tính Excel (*.xlsx)|*.xlsx",
                DefaultExt = "xlsx",
                FileName = TthcExcelExport.SuggestedFileName(DateTime.Now),
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                OverwritePrompt = true
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                path = dialog.FileName;
            }

            UseWaitCursor = true;
            _exportPage.Enabled = _exportAll.Enabled = false;
            try
            {
                TthcPage page = _session.Page;
                IList<TthcSummary> items;
                string scope;
                if (allPages)
                {
                    int total = Math.Max(1, page.PageCount);
                    _status.ForeColor = Color.DimGray;
                    var progress = new Progress<int>(n => _status.Text = "Đang tải trang " + n + "/" + total + " để xuất Excel...");
                    items = await _session.FetchAllPagesAsync(progress, _cancellation.Token);
                    if (items == null)
                    {
                        ShowError();
                        return;
                    }
                    scope = "Tất cả các trang (" + total + " trang)";
                }
                else
                {
                    items = page.Items;
                    scope = "Trang " + page.PageNumber + "/" + Math.Max(1, page.PageCount);
                }

                using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    TthcExcelExport.Write(stream, items, _session.AppliedFilters, _session.Options, scope, DateTime.Now, null);
                }
                _status.ForeColor = Color.DimGray;
                _status.Text = "Đã xuất " + items.Count + " thủ tục ra " + path;
            }
            catch (OperationCanceledException)
            {
            }
            catch (IOException)
            {
                _status.ForeColor = Color.Firebrick;
                _status.Text = "Không ghi được tệp. Kiểm tra tệp có đang mở trong Excel hoặc thư mục có quyền ghi không.";
            }
            catch (UnauthorizedAccessException)
            {
                _status.ForeColor = Color.Firebrick;
                _status.Text = "Không có quyền ghi vào vị trí đã chọn. Hãy chọn thư mục khác.";
            }
            finally
            {
                if (!IsDisposed)
                {
                    UseWaitCursor = false;
                    UpdatePager();
                }
            }
        }

        private async System.Threading.Tasks.Task OpenDetailAsync(int rowIndex)
        {
            string code = Convert.ToString(_grid.Rows[rowIndex].Cells["code"].Value);
            await RunAsync(() => _session.OpenDetailAsync(code, _cancellation.Token));
            if (IsDisposed || _session.Detail == null) return;
            using (var form = new TthcDetailForm(_session.Detail))
            {
                form.ShowDialog(this);
            }
            _session.CloseDetail();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _cancellation.Dispose();
            base.Dispose(disposing);
        }
    }

    /// <summary>Chi tiết một TTHC dạng văn bản thuần, chỉ đọc (NFR-DVC-SEC-003: không vẽ HTML của cổng).</summary>
    public sealed class TthcDetailForm : KryptonForm
    {
        public TthcDetailForm(TthcDetail detail)
        {
            Text = "Chi tiết thủ tục " + detail.Code;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(760, 560);

            var text = new StringBuilder();
            text.AppendLine(detail.Name);
            text.AppendLine();
            foreach (var section in detail.Sections)
            {
                text.AppendLine(section.Key.ToUpperInvariant());
                text.AppendLine(section.Value);
                text.AppendLine();
            }

            var box = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Dock = DockStyle.Fill,
                Text = text.ToString().Replace("\r\n", "\n").Replace("\n", Environment.NewLine),
                BackColor = SystemColors.Window
            };
            var back = new KryptonButton { Text = "Quay lại", Dock = DockStyle.Bottom, Height = 36 };
            UiIcons.Apply(back, UiIcon.Back);
            back.Click += (s, e) => Close();
            Controls.Add(box);
            Controls.Add(back);
            box.Select(0, 0);
        }
    }
}
