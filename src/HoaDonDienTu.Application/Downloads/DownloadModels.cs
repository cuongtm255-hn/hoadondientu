using System;
using System.Collections.Generic;
using HoaDonDienTu.Application.Portal;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.Domain.Downloads;
using HoaDonDienTu.Domain.Invoices;

namespace HoaDonDienTu.Application.Downloads
{
    public sealed class InvalidDownloadStateException : InvalidOperationException
    {
        public InvalidDownloadStateException(string message)
            : base(message)
        {
        }
    }

    /// <summary>
    /// Phiên tải (DD-05 mục 3.3). Kho và MST được gắn tại lúc tạo phiên và không đổi (FR-JOB-013).
    /// Trạng thái chỉ đổi qua <see cref="TransitionTo"/>.
    /// </summary>
    public sealed class DownloadSession
    {
        public DownloadSession(
            Guid id,
            CompanyId companyId,
            TaxCode taxCode,
            string storageRoot,
            InvoiceDirection direction,
            PeriodSpec period,
            InvoiceSearchFilter filter,
            DateTime createdAtUtc,
            string startedByUser)
        {
            Id = id;
            CompanyId = companyId;
            TaxCode = taxCode;
            StorageRoot = storageRoot;
            Direction = direction;
            Period = period;
            Filter = filter ?? new InvoiceSearchFilter();
            CreatedAtUtc = createdAtUtc;
            StartedByUser = startedByUser ?? string.Empty;
            State = DownloadSessionState.Draft;
        }

        public Guid Id { get; private set; }
        public CompanyId CompanyId { get; private set; }
        public TaxCode TaxCode { get; private set; }
        public string StorageRoot { get; private set; }
        public InvoiceDirection Direction { get; private set; }
        public PeriodSpec Period { get; private set; }
        public InvoiceSearchFilter Filter { get; private set; }
        public DownloadSessionState State { get; private set; }
        public DateTime CreatedAtUtc { get; private set; }
        public DateTime? StartedAtUtc { get; private set; }
        public DateTime? FinishedAtUtc { get; private set; }
        public string StartedByUser { get; private set; }

        public void TransitionTo(DownloadSessionState next, DateTime nowUtc)
        {
            if (!DownloadStateMachine.CanTransition(State, next))
            {
                throw new InvalidDownloadStateException("Không thể chuyển phiên tải từ " + State + " sang " + next + ".");
            }

            State = next;
            if (next == DownloadSessionState.Running)
            {
                if (!StartedAtUtc.HasValue) StartedAtUtc = nowUtc;
                FinishedAtUtc = null;
            }
            else if (DownloadStateMachine.IsFinished(next))
            {
                FinishedAtUtc = nowUtc;
            }
        }

        public DownloadSession Clone()
        {
            var copy = new DownloadSession(Id, CompanyId, TaxCode, StorageRoot, Direction, Period, Filter, CreatedAtUtc, StartedByUser);
            copy.State = State;
            copy.StartedAtUtc = StartedAtUtc;
            copy.FinishedAtUtc = FinishedAtUtc;
            return copy;
        }
    }

    /// <summary>Một đoạn tháng của phiên cùng điểm checkpoint để tiếp tục không tải lại phần đã xong.</summary>
    public sealed class DownloadSegment
    {
        public DownloadSegment(Guid id, Guid sessionId, int index, DateSegment range)
        {
            Id = id;
            SessionId = sessionId;
            Index = index;
            Range = range;
            State = DownloadSegmentState.Pending;
        }

        public Guid Id { get; private set; }
        public Guid SessionId { get; private set; }
        public int Index { get; private set; }
        public DateSegment Range { get; private set; }
        public DownloadSegmentState State { get; private set; }

        /// <summary>Cursor của trang sẽ lấy tiếp; null là trang đầu.</summary>
        public string Cursor { get; set; }

        /// <summary>Số hóa đơn của trang hiện tại đã xử lý xong; tiếp tục sẽ bỏ qua đúng số này.</summary>
        public int ItemOffset { get; set; }

        public int PageIndex { get; set; }
        public int ReceivedCount { get; set; }
        public int XmlSavedCount { get; set; }
        public int ErrorCount { get; set; }
        public int RetryCount { get; set; }

        /// <summary>Mô tả ngắn đã làm sạch của lỗi gần nhất.</summary>
        public string LastError { get; set; }

        public void TransitionTo(DownloadSegmentState next)
        {
            if (!DownloadStateMachine.CanTransition(State, next))
            {
                throw new InvalidDownloadStateException("Không thể chuyển đoạn tải từ " + State + " sang " + next + ".");
            }

            State = next;
        }

        public DownloadSegment Clone()
        {
            var copy = new DownloadSegment(Id, SessionId, Index, Range);
            copy.State = State;
            copy.Cursor = Cursor;
            copy.ItemOffset = ItemOffset;
            copy.PageIndex = PageIndex;
            copy.ReceivedCount = ReceivedCount;
            copy.XmlSavedCount = XmlSavedCount;
            copy.ErrorCount = ErrorCount;
            copy.RetryCount = RetryCount;
            copy.LastError = LastError;
            return copy;
        }
    }

    public enum DownloadEventLevel
    {
        Info,
        Warning,
        Error
    }

    public sealed class DownloadEvent
    {
        public DownloadEvent(Guid sessionId, Guid? segmentId, DateTime timeUtc, DownloadEventLevel level, string code, string message)
        {
            EventId = Guid.NewGuid();
            SessionId = sessionId;
            SegmentId = segmentId;
            TimeUtc = timeUtc;
            Level = level;
            Code = code;
            Message = message;
        }

        public Guid EventId { get; private set; }
        public Guid SessionId { get; private set; }
        public Guid? SegmentId { get; private set; }
        public DateTime TimeUtc { get; private set; }
        public DownloadEventLevel Level { get; private set; }
        public string Code { get; private set; }

        /// <summary>Đã làm sạch: không chứa token, header hay nội dung phản hồi.</summary>
        public string Message { get; private set; }
    }

    /// <summary>Ảnh chụp tiến độ gửi cho UI qua IProgress.</summary>
    public sealed class DownloadProgress
    {
        public DownloadProgress(
            Guid sessionId,
            DownloadSessionState state,
            int segmentIndex,
            int segmentCount,
            string currentSegment,
            int received,
            int saved,
            int errors)
        {
            SessionId = sessionId;
            State = state;
            SegmentIndex = segmentIndex;
            SegmentCount = segmentCount;
            CurrentSegment = currentSegment;
            ReceivedCount = received;
            XmlSavedCount = saved;
            ErrorCount = errors;
        }

        public Guid SessionId { get; private set; }
        public DownloadSessionState State { get; private set; }

        /// <summary>Chỉ số đoạn đang chạy, tính từ 0; -1 khi chưa/không còn đoạn nào chạy.</summary>
        public int SegmentIndex { get; private set; }

        public int SegmentCount { get; private set; }
        public string CurrentSegment { get; private set; }
        public int ReceivedCount { get; private set; }
        public int XmlSavedCount { get; private set; }
        public int ErrorCount { get; private set; }
    }

    public sealed class DownloadSessionSummary
    {
        public DownloadSessionSummary(DownloadSession session, int segmentCount, int completedSegments, int received, int saved, int errors)
        {
            Session = session;
            SegmentCount = segmentCount;
            CompletedSegments = completedSegments;
            ReceivedCount = received;
            XmlSavedCount = saved;
            ErrorCount = errors;
        }

        public DownloadSession Session { get; private set; }
        public int SegmentCount { get; private set; }
        public int CompletedSegments { get; private set; }
        public int ReceivedCount { get; private set; }
        public int XmlSavedCount { get; private set; }
        public int ErrorCount { get; private set; }
    }
}
