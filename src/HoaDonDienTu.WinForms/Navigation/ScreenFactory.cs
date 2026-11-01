using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using HoaDonDienTu.Application.Navigation;
using HoaDonDienTu.WinForms.Shell;

namespace HoaDonDienTu.WinForms.Navigation
{
    /// <summary>Khai báo cách tạo một screen; feature DD-02…DD-11 đăng ký descriptor của mình.</summary>
    public sealed class ScreenDescriptor
    {
        public ScreenDescriptor(ScreenId id, string title, string subtitle, Func<object, IScreen> create)
        {
            Id = id;
            Title = title;
            Subtitle = subtitle;
            Create = create;
        }

        public ScreenId Id { get; private set; }
        public string Title { get; private set; }
        public string Subtitle { get; private set; }

        /// <summary>Null nghĩa là dùng màn hình tạm.</summary>
        public Func<object, IScreen> Create { get; private set; }
    }

    /// <summary>Screen tạm cho tới khi feature tương ứng được implement.</summary>
    public sealed class PlaceholderScreen : IScreen
    {
        private readonly PlaceholderScreenControl _view;

        public PlaceholderScreen(ScreenId id, string title, string subtitle)
        {
            Id = id;
            _view = new PlaceholderScreenControl(title, subtitle);
        }

        public ScreenId Id { get; private set; }

        public Control View
        {
            get { return _view; }
        }

        public Task<bool> CanCloseAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        public void CancelOperations()
        {
        }

        public void Dispose()
        {
            _view.Dispose();
        }
    }

    public sealed class ScreenFactory : IScreenFactory
    {
        private readonly Dictionary<ScreenId, ScreenDescriptor> _descriptors = new Dictionary<ScreenId, ScreenDescriptor>();

        public ScreenFactory(IEnumerable<ScreenDescriptor> descriptors)
        {
            if (descriptors == null) throw new ArgumentNullException("descriptors");
            foreach (var descriptor in descriptors)
            {
                _descriptors[descriptor.Id] = descriptor;
            }
        }

        /// <summary>Danh sách mặc định: mọi screen là placeholder với tiêu đề theo menu.</summary>
        public static ScreenFactory CreateDefault(params ScreenDescriptor[] overrides)
        {
            var descriptors = new List<ScreenDescriptor>
            {
                Placeholder(ScreenId.Initializing, "Đang khởi tạo...", "Đang xác định chế độ làm việc"),
                Placeholder(ScreenId.Dashboard, "Tổng quan", "SCR-02 · chưa implement"),
                Placeholder(ScreenId.Download, "Tải hóa đơn", "SCR-03 · chưa implement"),
                Placeholder(ScreenId.DownloadProgress, "Tiến độ tải", "SCR-04 · chưa implement"),
                Placeholder(ScreenId.InvoiceArchive, "Kho hóa đơn", "SCR-05 · chưa implement"),
                Placeholder(ScreenId.InvoiceDetail, "Chi tiết hóa đơn", "SCR-06 · chưa implement"),
                Placeholder(ScreenId.InvoiceViewer, "Xem hóa đơn", "SCR-07 · chưa implement"),
                Placeholder(ScreenId.DownloadHistory, "Lịch sử tải", "SCR-08 · chưa implement"),
                Placeholder(ScreenId.Settings, "Cài đặt", "SCR-09 · chưa implement"),
                Placeholder(ScreenId.CompanyList, "Doanh nghiệp", "SCR-10 · chưa implement"),
                Placeholder(ScreenId.CompanyEdit, "Thông tin doanh nghiệp", "SCR-11 · chưa implement"),
                Placeholder(ScreenId.License, "Bản quyền", "SCR-12 · chưa implement"),
                Placeholder(ScreenId.Recovery, "Cần khôi phục", "Dữ liệu được giữ nguyên; xem hướng dẫn khôi phục")
            };

            // Descriptor của feature đã implement thay thế placeholder cùng ScreenId (đăng ký sau thắng).
            descriptors.AddRange(overrides);
            return new ScreenFactory(descriptors);
        }

        public IScreen Create(ScreenId id, object parameter)
        {
            ScreenDescriptor descriptor;
            if (_descriptors.TryGetValue(id, out descriptor))
            {
                if (descriptor.Create != null) return descriptor.Create(parameter);
                return new PlaceholderScreen(id, descriptor.Title, descriptor.Subtitle);
            }

            return new PlaceholderScreen(id, id.ToString(), "Chưa có màn hình");
        }

        private static ScreenDescriptor Placeholder(ScreenId id, string title, string subtitle)
        {
            return new ScreenDescriptor(id, title, subtitle, null);
        }
    }
}
