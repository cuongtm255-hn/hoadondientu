using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using ComponentFactory.Krypton.Toolkit;
using HoaDonDienTu.Application.PublicService;
using HoaDonDienTu.Domain.PublicService;

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
        private readonly Label _summary = new Label();
        private readonly Label _status = new Label();
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

            var filters = new Panel { Dock = DockStyle.Top, Height = 190 };
            AddLabelled(filters, new Label { AutoSize = true, Text = "Nguồn" }, _source, 10, 6, 300);
            AddLabelled(filters, _nameLabel, _name, 330, 6, 220);
            AddLabelled(filters, _declLabel, _declaration, 560, 6, 200);
            AddLabelled(filters, _codeLabel, _code, 770, 6, 200);
            AddLabelled(filters, new Label { AutoSize = true, Text = "Từ ngày" }, _from, 10, 58, 130);
            AddLabelled(filters, new Label { AutoSize = true, Text = "Đến ngày" }, _to, 150, 58, 130);
            AddLabelled(filters, new Label { AutoSize = true, Text = "MST ủy quyền (nếu có)" }, _authorized, 330, 58, 220);

            AddLabelled(filters, new Label { AutoSize = true, Text = "Mã xác nhận (nhập lại cho mỗi lần tìm)" }, _captchaBox, 330, 110, 160);
            _captchaImage.SetBounds(10, 124, 140, 44);
            _refresh.SetBounds(160, 124, 80, 44);
            _refresh.Values.Text = "Đổi mã";
            _refresh.Click += async (s, e) => { await RunBusyAsync(async () => { await _session.RefreshCaptchaAsync(_cancellation.Token); ShowCaptcha(); }); };
            _search.SetBounds(510, 126, 100, 36);
            _search.Values.Text = "Tìm";
            _search.Click += async (s, e) => await SearchAsync();
            AcceptButton = _search;
            filters.Controls.Add(_captchaImage);
            filters.Controls.Add(_refresh);
            filters.Controls.Add(_search);

            _status.SetBounds(10, 170, 980, 20);
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
            FormClosed += (s, e) => _cancellation.Cancel();
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
            _status.Text = (dvc ? string.Empty : "Cổng chỉ hỗ trợ tờ khai nộp trước 01/07/2025. ") + _notice;
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
            string captcha = _captchaBox.Text.Trim();
            if (captcha.Length == 0)
            {
                _status.ForeColor = Color.Firebrick;
                _status.Text = "Nhập mã xác nhận rồi bấm lại.";
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
            _grid.Columns.Add("stt", "STT");
            _grid.Columns["stt"].FillWeight = 6;
            foreach (string column in page.Columns) _grid.Columns.Add(column, column);
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
                return;
            }
            _summary.Text = "Trang " + page.PageNumber + "/" + Math.Max(1, page.PageCount) + " — Tổng số bản ghi: " + page.TotalRecords;
            _prev.Enabled = page.PageNumber > 1;
            _next.Enabled = page.PageNumber < page.PageCount;
            _go.Enabled = _pageBox.Enabled = page.PageCount > 1;
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
