using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using ComponentFactory.Krypton.Toolkit;
using HoaDonDienTu.Application.PublicService;
using HoaDonDienTu.Domain.PublicService;
using HoaDonDienTu.WinForms.Common;

namespace HoaDonDienTu.WinForms.Groups
{
    /// <summary>
    /// Màn hình tra cứu hồ sơ đã nộp (FT-DVC-03): hai nguồn (Dịch vụ công / thuế điện tử), bộ lọc, CAPTCHA do người dùng gõ cho MỖI lần tìm,
    /// bảng kết quả. Chỉ đọc. Dựng bằng mã, không có Designer. Logic ở <see cref="HoSoSearchSession"/>.
    /// </summary>
    public sealed class HoSoForm : KryptonForm
    {
        private readonly HoSoSearchSession _session;
        private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();
        private readonly ComboBox _source = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly Label _nameLabel = new Label { AutoSize = true, Text = "Tên TTHC" };
        private readonly KryptonTextBox _name = new KryptonTextBox();
        private readonly Label _declLabel = new Label { AutoSize = true, Text = "Tờ khai" };
        private readonly KryptonTextBox _declaration = new KryptonTextBox();
        private readonly Label _codeLabel = new Label { AutoSize = true, Text = "Mã hồ sơ" };
        private readonly KryptonTextBox _code = new KryptonTextBox();
        private readonly DateTimePicker _from = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy" };
        private readonly DateTimePicker _to = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy" };
        private readonly KryptonTextBox _authorized = new KryptonTextBox();
        private readonly PictureBox _captchaImage = new PictureBox { SizeMode = PictureBoxSizeMode.Zoom, BorderStyle = BorderStyle.FixedSingle };
        private readonly KryptonTextBox _captchaBox = new KryptonTextBox();
        private readonly KryptonButton _refresh = new KryptonButton();
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
        private readonly Label _warning = new Label();
        private readonly string _notice;

        public HoSoForm(HoSoSearchSession session, string notice)
        {
            _session = session;
            _notice = notice ?? string.Empty;
            Text = "Tra cứu hồ sơ đã nộp"
#if DEBUG
                + " — [TEST] DỮ LIỆU GIẢ"
#endif
                ;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(1000, 640);
            MinimumSize = new Size(900, 520);

            _source.Items.Add("Hồ sơ đã nộp trên Dịch vụ công");
            _source.Items.Add("Hồ sơ đã nộp trên thuế điện tử");

            var filters = new Panel { Dock = DockStyle.Top, Height = 228 };
            AddLabelled(filters, new Label { AutoSize = true, Text = "Nguồn" }, _source, 10, 6, 300);
            AddLabelled(filters, _nameLabel, _name, 330, 6, 220);
            AddLabelled(filters, _declLabel, _declaration, 560, 6, 200);
            AddLabelled(filters, _codeLabel, _code, 770, 6, 200);
            AddLabelled(filters, new Label { AutoSize = true, Text = "Từ ngày" }, _from, 10, 58, 130);
            AddLabelled(filters, new Label { AutoSize = true, Text = "Đến ngày" }, _to, 150, 58, 130);
            AddLabelled(filters, new Label { AutoSize = true, Text = "MST ủy quyền (nếu có)" }, _authorized, 330, 58, 220);

            // Hàng CAPTCHA: ảnh, Đổi mã, ô nhập mã và nút Tìm cùng căn giữa theo chiều dọc.
            AddLabelled(filters, new Label { AutoSize = true, Text = "Mã xác nhận (nhập lại cho mỗi lần tìm)" }, _captchaBox, 330, 110, 170);
            _captchaImage.SetBounds(10, 124, 140, 44);
            _refresh.SetBounds(158, 124, 100, 44);
            _refresh.Values.Text = "Đổi mã";
            UiIcons.Apply(_refresh, UiIcon.Refresh);
            _refresh.Click += async (s, e) => { await RunBusyAsync(async () => { await _session.RefreshCaptchaAsync(_cancellation.Token); ShowCaptcha(); }); };
            _search.SetBounds(512, 129, 110, 38);
            _search.Values.Text = "Tìm";
            UiIcons.Apply(_search, UiIcon.Search);
            _search.Click += async (s, e) => await SearchAsync();
            AcceptButton = _search;
            filters.Controls.Add(_captchaImage);
            filters.Controls.Add(_refresh);
            filters.Controls.Add(_search);

            // Thông báo trạng thái (lỗi, không có dữ liệu) và cảnh báo cố định của tab thuế điện tử nằm ở hai dòng riêng, không đè lên nhau.
            _status.SetBounds(10, 176, 980, 20);
            _status.ForeColor = Color.DimGray;
            _warning.SetBounds(10, 198, 980, 24);
            _warning.TextAlign = ContentAlignment.MiddleLeft;
            _warning.BackColor = Color.FromArgb(255, 243, 205);
            _warning.ForeColor = Color.FromArgb(102, 77, 3);
            _warning.Padding = new Padding(8, 0, 0, 0);
            _warning.Visible = false;
            filters.Controls.Add(_status);
            filters.Controls.Add(_warning);

            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.RowHeadersVisible = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.BackgroundColor = SystemColors.Window;

            var pager = new Panel { Dock = DockStyle.Bottom, Height = 44 };
            _summary.SetBounds(10, 12, 400, 22);
            _prev.SetBounds(420, 6, 70, 30);
            _prev.Values.Text = "◀ Trước";
            _prev.Click += async (s, e) => await GoToAsync(_session.Page.PageNumber - 1);
            _next.SetBounds(496, 6, 70, 30);
            _next.Values.Text = "Sau ▶";
            _next.Click += async (s, e) => await GoToAsync(_session.Page.PageNumber + 1);
            _pageBox.SetBounds(580, 8, 60, 27);
            _go.SetBounds(646, 6, 50, 30);
            _go.Values.Text = "Đi";
            _go.Click += async (s, e) =>
            {
                int n;
                if (int.TryParse(_pageBox.Text.Trim(), out n)) await GoToAsync(n);
            };
            // Xuất Excel (FT-DVC-08): trang đang xem hoặc tất cả các trang của bộ lọc đã áp dụng.
            _exportPage.SetBounds(730, 6, 120, 32);
            _exportPage.Values.Text = "Xuất trang này";
            UiIcons.Apply(_exportPage, UiIcon.Excel);
            _exportPage.Click += async (s, e) => await ExportAsync(false);
            _exportAll.SetBounds(858, 6, 130, 32);
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

            _source.SelectedIndexChanged += async (s, e) => await OnSourceChangedAsync();
            Shown += (s, e) => { _source.SelectedIndex = 0; };
            FormClosed += (s, e) => { _cancellation.Cancel(); _session.ClearPagingCaptcha(); };
        }

        private static void AddLabelled(Control parent, Label label, Control input, int x, int y, int width)
        {
            label.Location = new Point(x, y);
            parent.Controls.Add(label);
            input.SetBounds(x, y + 20, width, 27);
            parent.Controls.Add(input);
        }

        private HoSoSource CurrentSource
        {
            get { return _source.SelectedIndex == 1 ? HoSoSource.ThueDienTu : HoSoSource.Dvc; }
        }

        private async System.Threading.Tasks.Task OnSourceChangedAsync()
        {
            bool dvc = CurrentSource == HoSoSource.Dvc;
            _nameLabel.Visible = _name.Visible = dvc;
            _codeLabel.Text = dvc ? "Mã hồ sơ" : "Mã giao dịch";
            DateTime today = DateTime.Today;
            _from.Value = dvc ? new DateTime(today.Year, today.Month, 1) : new DateTime(2025, 6, 1);
            _to.Value = dvc ? today : new DateTime(2025, 6, 30);
            _name.Clear();
            _declaration.Clear();
            _code.Clear();
            _captchaBox.Clear();
            _grid.Rows.Clear();
            _grid.Columns.Clear();
            _status.ForeColor = Color.DimGray;
            _status.Text = _notice;
            _warning.Visible = !dvc;
            _warning.Text = dvc ? string.Empty : "Lưu ý của cổng: chỉ hỗ trợ tờ khai nộp trước ngày 01/07/2025.";
            UpdatePager();
            await RunBusyAsync(async () =>
            {
                await _session.SelectSourceAsync(CurrentSource, _cancellation.Token);
                ShowCaptcha();
            });
        }

        private async System.Threading.Tasks.Task SearchAsync()
        {
            var query = new HoSoQuery
            {
                FromDate = _from.Value.Date,
                ToDate = _to.Value.Date,
                AuthorizedTaxCode = _authorized.Text.Trim(),
                ProcedureName = _name.Text.Trim(),
                Declaration = _declaration.Text.Trim(),
                FileCode = CurrentSource == HoSoSource.Dvc ? _code.Text.Trim() : null,
                TransactionCode = CurrentSource == HoSoSource.ThueDienTu ? _code.Text.Trim() : null,
                CaptchaText = _captchaBox.Text.Trim()
            };
            await RunBusyAsync(async () => await _session.SearchAsync(query, _cancellation.Token));
        }

        private async System.Threading.Tasks.Task GoToAsync(int page)
        {
            // Đổi trang dùng lại mã của lần tìm này (như trang cổng); chỉ khi cổng từ chối hoặc không còn mã mới cần gõ mã mới.
            string captcha = _captchaBox.Text.Trim();
            if (captcha.Length == 0 && !_session.CanPageWithoutNewCaptcha)
            {
                _status.ForeColor = Color.Firebrick;
                _status.Text = "Cổng cần mã xác nhận mới: nhập mã trong ảnh rồi bấm lại.";
                return;
            }
            await RunBusyAsync(async () => await _session.GoToPageAsync(page, captcha, _cancellation.Token));
        }

        private async System.Threading.Tasks.Task RunBusyAsync(Func<System.Threading.Tasks.Task> action)
        {
            if (_session.IsBusy) return;
            UseWaitCursor = true;
            try
            {
                await action();
                if (IsDisposed) return;
                // Mã đã gõ không dùng lại được: luôn xóa ô và hiện ảnh mới.
                _captchaBox.Clear();
                ShowCaptcha();
                if (_session.LastFailure != HoSoFailure.None)
                {
                    _status.ForeColor = Color.Firebrick;
                    _status.Text = HoSoService.MessageFor(_session.LastFailure);
                }
                else
                {
                    _status.ForeColor = Color.DimGray;
                    _status.Text = _session.Page != null && _session.Page.Rows.Count == 0 ? _session.Page.EmptyMessage : string.Empty;
                }
                ShowPage();
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                if (!IsDisposed) UseWaitCursor = false;
            }
        }

        /// <summary>
        /// Xuất Excel: trang đang xem, hoặc tất cả các trang (tuần tự, có nghỉ giữa các trang; đổi trang dùng lại mã của lần tìm). Kết quả một trang thì
        /// xuất ngay không gọi cổng. Cổng từ chối mã thì báo gõ mã mới rồi bấm lại; không ghi tệp dở dang.
        /// </summary>
        private async System.Threading.Tasks.Task ExportAsync(bool allPages)
        {
            if (_session.IsBusy || _session.Page == null || _session.Page.Rows.Count == 0) return;

            string path;
            using (var dialog = new SaveFileDialog
            {
                Title = "Xuất hồ sơ đã nộp ra Excel",
                Filter = "Bảng tính Excel (*.xlsx)|*.xlsx",
                DefaultExt = "xlsx",
                FileName = HoSoExcelExport.SuggestedFileName(DateTime.Now),
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
                HoSoPage page = _session.Page;
                System.Collections.Generic.IList<HoSoRow> rows;
                string scope;
                if (allPages)
                {
                    int total = Math.Max(1, page.PageCount);
                    _status.ForeColor = Color.DimGray;
                    var progress = new Progress<int>(n => _status.Text = "Đang tải trang " + n + "/" + total + " để xuất Excel...");
                    rows = await _session.FetchAllPagesAsync(_captchaBox.Text.Trim(), progress, _cancellation.Token);
                    _captchaBox.Clear();
                    if (rows == null)
                    {
                        _status.ForeColor = Color.Firebrick;
                        _status.Text = _session.LastFailure == HoSoFailure.WrongCaptcha || _session.LastFailure == HoSoFailure.InvalidInput
                            ? "Cổng cần mã xác nhận mới: nhập mã trong ảnh rồi bấm Xuất tất cả lại."
                            : HoSoService.MessageFor(_session.LastFailure);
                        ShowCaptcha();
                        return;
                    }
                    scope = total == 1 ? "Trang 1/1" : "Tất cả các trang (" + total + " trang)";
                }
                else
                {
                    rows = page.Rows;
                    scope = "Trang " + page.PageNumber + "/" + Math.Max(1, page.PageCount);
                }

                using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    HoSoExcelExport.Write(stream, page.Columns, rows, _session.AppliedFilters, scope, DateTime.Now, null);
                }
                _status.ForeColor = Color.DimGray;
                _status.Text = "Đã xuất " + rows.Count + " hồ sơ ra " + path;
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

        private void ShowCaptcha()
        {
            var old = _captchaImage.Image;
            if (_session.CaptchaImage == null || _session.CaptchaImage.Length == 0)
            {
                _captchaImage.Image = null;
            }
            else
            {
                using (var stream = new MemoryStream(_session.CaptchaImage))
                {
                    _captchaImage.Image = new Bitmap(stream);
                }
            }
            if (old != null) old.Dispose();
        }

        private void ShowPage()
        {
            var page = _session.Page;
            if (page == null) { UpdatePager(); return; }

            _grid.Rows.Clear();
            _grid.Columns.Clear();
            // Không có kết quả (cổng không trả bảng): để lưới trống, chỉ hiện thông điệp "Không có dữ liệu" ở dòng trạng thái.
            if (page.Columns.Count == 0)
            {
                UpdatePager();
                return;
            }
            _grid.Columns.Add("stt", "STT");
            _grid.Columns["stt"].FillWeight = 6;
            foreach (string column in page.Columns) _grid.Columns.Add(column, column);
            _grid.Columns["stt"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            _grid.Columns["stt"].Width = 52;
            int stt = (page.PageNumber - 1) * HoSoPage.DefaultPageSize;
            foreach (var row in page.Rows)
            {
                var cells = new object[row.Cells.Count + 1];
                cells[0] = ++stt;
                for (int i = 0; i < row.Cells.Count; i++) cells[i + 1] = row.Cells[i];
                _grid.Rows.Add(cells);
            }
            UpdatePager();
        }

        private void UpdatePager()
        {
            var page = _session.Page;
            if (page == null)
            {
                _summary.Text = "Chưa tìm. Nhập điều kiện và mã xác nhận rồi bấm Tìm.";
                _prev.Enabled = _next.Enabled = _go.Enabled = _pageBox.Enabled = false;
                _exportPage.Enabled = _exportAll.Enabled = false;
                return;
            }
            _summary.Text = page.TotalRecords == 0
                ? "Không có bản ghi nào."
                : "Trang " + page.PageNumber + "/" + Math.Max(1, page.PageCount) + " — Tổng số bản ghi: " + page.TotalRecords;
            _prev.Enabled = page.PageNumber > 1;
            _next.Enabled = page.PageNumber < page.PageCount;
            _go.Enabled = _pageBox.Enabled = page.PageCount > 1;
            _exportPage.Enabled = _exportAll.Enabled = page.Rows.Count > 0;
            _pageBox.Text = page.PageNumber.ToString();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cancellation.Dispose();
                if (_captchaImage.Image != null) _captchaImage.Image.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
