using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Authentication;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Licensing;
using HoaDonDienTu.Application.Portal;
using HoaDonDienTu.Domain.Invoices;
using HoaDonDienTu.Domain.Licensing;

namespace HoaDonDienTu.Application.Downloads
{
    /// <summary>Kế hoạch tải đã chốt: chiều hóa đơn, kỳ, bộ lọc và các đoạn tháng sẽ chạy tuần tự (DD-04).</summary>
    public sealed class DownloadPlan
    {
        private DownloadPlan(InvoiceDirection direction, PeriodSpec period, InvoiceSearchFilter filter, IList<DateSegment> segments)
        {
            Direction = direction;
            Period = period;
            Filter = filter;
            Segments = segments;
        }

        public InvoiceDirection Direction { get; private set; }
        public PeriodSpec Period { get; private set; }
        public InvoiceSearchFilter Filter { get; private set; }
        public IList<DateSegment> Segments { get; private set; }

        /// <exception cref="ArgumentException">Kỳ không hợp lệ.</exception>
        public static DownloadPlan Create(InvoiceDirection direction, PeriodSpec period, InvoiceSearchFilter filter, IPeriodSplitter splitter)
        {
            if (splitter == null) throw new ArgumentNullException("splitter");
            var segments = splitter.Split(period);
            return new DownloadPlan(direction, period, Copy(filter), new List<DateSegment>(segments).AsReadOnly());
        }

        private static InvoiceSearchFilter Copy(InvoiceSearchFilter source)
        {
            // Sao chép để thay đổi bộ lọc trên UI sau đó không làm đổi kế hoạch đã tạo.
            var f = source ?? new InvoiceSearchFilter();
            return new InvoiceSearchFilter
            {
                CounterpartyTaxCode = f.CounterpartyTaxCode,
                BuyerIdNumber = f.BuyerIdNumber,
                StatusCode = f.StatusCode,
                CheckResultCode = f.CheckResultCode,
                InvoiceNumber = f.InvoiceNumber,
                FormSymbol = f.FormSymbol,
                InvoiceSymbol = f.InvoiceSymbol,
                ProxyInvoice = f.ProxyInvoice
            };
        }
    }

    public sealed class StorageProbeResult
    {
        public StorageProbeResult(bool writable, string reason)
        {
            Writable = writable;
            Reason = reason;
        }

        public bool Writable { get; private set; }
        public string Reason { get; private set; }

        public static StorageProbeResult Ok()
        {
            return new StorageProbeResult(true, null);
        }

        public static StorageProbeResult Failed(string reason)
        {
            return new StorageProbeResult(false, reason);
        }
    }

    /// <summary>Kiểm tra thư mục kho của doanh nghiệp có ghi được không (FR-SEARCH-010).</summary>
    public interface IStorageProbe
    {
        StorageProbeResult Check(string storageRoot);
    }

    /// <summary>Cho biết cổng Thuế hỗ trợ bộ lọc nào để UI chỉ hiện trường dùng được (FR-SEARCH-007).</summary>
    public interface IPortalCapabilities
    {
        /// <param name="direction">Chiều tra cứu: mỗi chiều có bộ trường riêng (CCCD người mua chỉ có ở bán ra).</param>
        /// <param name="filterField">Tên trường của <see cref="InvoiceSearchFilter"/>, ví dụ <c>CounterpartyTaxCode</c>.</param>
        bool SupportsFilter(HoaDonDienTu.Domain.Invoices.InvoiceDirection direction, string filterField);
    }

    /// <summary>Tạo phiên tải từ kế hoạch; implementation ở DD-05 (<c>IDownloadOrchestrator</c>).</summary>
    public interface IDownloadSessionStarter
    {
        Task StartAsync(DownloadPlan plan, CancellationToken cancellationToken);
    }

    public sealed class StartDecision
    {
        private StartDecision(bool canStart, string reason)
        {
            CanStart = canStart;
            Reason = reason;
        }

        public bool CanStart { get; private set; }

        /// <summary>Lý do khóa nút bắt đầu, hiển thị thành chữ cạnh nút; null khi được phép.</summary>
        public string Reason { get; private set; }

        public static StartDecision Allowed()
        {
            return new StartDecision(true, null);
        }

        public static StartDecision Denied(string reason)
        {
            return new StartDecision(false, reason);
        }
    }

    /// <summary>Điều kiện bật nút "Bắt đầu tải" (FR-SEARCH-010): doanh nghiệp, quyền, đăng nhập đúng MST, kỳ hợp lệ, thư mục ghi được.</summary>
    public sealed class DownloadStartPolicy
    {
        private readonly ICompanyContextAccessor _context;
        private readonly IPortalSessionStore _session;
        private readonly IEntitlementService _entitlement;

        public DownloadStartPolicy(ICompanyContextAccessor context, IPortalSessionStore session, IEntitlementService entitlement)
        {
            if (context == null) throw new ArgumentNullException("context");
            if (session == null) throw new ArgumentNullException("session");
            if (entitlement == null) throw new ArgumentNullException("entitlement");
            _context = context;
            _session = session;
            _entitlement = entitlement;
        }

        public StartDecision Evaluate(PeriodValidationResult period, StorageProbeResult storage)
        {
            if (!_context.HasCurrent) return StartDecision.Denied("Hãy chọn doanh nghiệp trước khi tải.");
            var company = _context.Current;

            if (!_entitlement.IsAllowed(AppFeature.NewDownload))
            {
                return StartDecision.Denied("Chế độ hiện tại không cho phép tải hóa đơn mới.");
            }

            var session = _session.GetSnapshot();
            if (session == null || !session.CompanyId.Equals(company.CompanyId) || !session.TaxCode.Equals(company.TaxCode))
            {
                return StartDecision.Denied("Cần đăng nhập cổng Thuế cho MST " + company.TaxCode.Value + ".");
            }

            if (period == null || !period.IsValid)
            {
                return StartDecision.Denied(period == null || period.Errors.Count == 0 ? "Chưa chọn kỳ." : period.Errors[0]);
            }

            if (storage == null || !storage.Writable)
            {
                return StartDecision.Denied("Thư mục lưu trữ không ghi được" + (storage == null || string.IsNullOrEmpty(storage.Reason) ? "." : ": " + storage.Reason));
            }

            return StartDecision.Allowed();
        }
    }
}
