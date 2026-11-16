using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Groups;

namespace HoaDonDienTu.WinForms.Groups
{
    /// <summary>
    /// Shell nhóm Hóa đơn: bọc MainShellForm. Ẩn/hiện chứ không đóng, nên phiên đăng nhập cổng Thuế được giữ khi đổi nhóm.
    /// Form do AppApplicationContext sở hữu và hủy, nên Dispose ở đây không làm gì.
    /// </summary>
    public sealed class InvoiceGroupShell : IGroupShell
    {
        private readonly Form _form;
        private readonly ICompanyContextManager _context;
        private readonly IActiveJobService _jobs;

        public InvoiceGroupShell(Form form, ICompanyContextManager context, IActiveJobService jobs)
        {
            _form = form;
            _context = context;
            _jobs = jobs;
        }

        public void Show()
        {
            if (!_form.Visible) _form.Show();
            _form.Activate();
        }

        public void Hide()
        {
            _form.Hide();
        }

        public bool CanLeave(out string reason)
        {
            reason = null;
            if (!_context.HasCurrent) return true;
            var id = _context.Current.CompanyId;
            // Chạy ngoài luồng UI để .Result không bị kẹt đồng bộ hóa.
            bool running = Task.Run(() => _jobs.HasRunningDownloadAsync(id, default(System.Threading.CancellationToken))).Result;
            if (running) reason = "Đang tải hóa đơn. Đổi nhóm sẽ tạm dừng việc tải.";
            return !running;
        }

        public void StopRunningTasks()
        {
            if (!_context.HasCurrent) return;
            var id = _context.Current.CompanyId;
            Task.Run(() => _jobs.PauseAsync(id, default(System.Threading.CancellationToken))).Wait();
        }

        public void Dispose()
        {
        }
    }

    /// <summary>Shell nhóm Dịch vụ công (hiện là chỗ giữ chỗ). Ẩn/hiện để giữ nguyên trạng thái khi đổi nhóm.</summary>
    public sealed class PublicServiceGroupShell : IGroupShell
    {
        private readonly PublicServiceStubForm _form;

        public PublicServiceGroupShell(PublicServiceStubForm form)
        {
            _form = form;
        }

        public void Show()
        {
            if (!_form.Visible) _form.Show();
            _form.Activate();
        }

        public void Hide()
        {
            _form.Hide();
        }

        public bool CanLeave(out string reason)
        {
            reason = null;
            return true;
        }

        public void StopRunningTasks()
        {
        }

        public void Dispose()
        {
            _form.Dispose();
        }
    }
}
