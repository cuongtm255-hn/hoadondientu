using System.Collections.Generic;

namespace HoaDonDienTu.Domain.Downloads
{
    /// <summary>Trạng thái phiên tải theo SRS 9.3 / DD-05 mục 3.1.</summary>
    public enum DownloadSessionState
    {
        Draft,
        Running,
        Completed,
        CompletedWithErrors,
        PausedByUser,
        WaitingForLogin,
        Cancelled,

        /// <summary>Chỉ khi không thể tiếp tục (cấu hình/dữ liệu không hợp lệ). Lỗi mạng và hết token không dẫn tới đây.</summary>
        Failed
    }

    /// <summary>Trạng thái một đoạn tháng (DD-05 mục 3.2).</summary>
    public enum DownloadSegmentState
    {
        Pending,
        Running,
        Completed,
        Failed,
        Paused,
        Skipped
    }

    /// <summary>Các chuyển trạng thái hợp lệ; mọi thay đổi trạng thái đều phải đi qua đây.</summary>
    public static class DownloadStateMachine
    {
        private static readonly Dictionary<DownloadSessionState, DownloadSessionState[]> Session =
            new Dictionary<DownloadSessionState, DownloadSessionState[]>
            {
                { DownloadSessionState.Draft, new[] { DownloadSessionState.Running, DownloadSessionState.Cancelled } },
                {
                    DownloadSessionState.Running,
                    new[]
                    {
                        DownloadSessionState.Completed, DownloadSessionState.CompletedWithErrors, DownloadSessionState.PausedByUser,
                        DownloadSessionState.WaitingForLogin, DownloadSessionState.Cancelled, DownloadSessionState.Failed
                    }
                },
                { DownloadSessionState.PausedByUser, new[] { DownloadSessionState.Running, DownloadSessionState.Cancelled } },
                { DownloadSessionState.WaitingForLogin, new[] { DownloadSessionState.Running, DownloadSessionState.Cancelled } },

                // Thử lại đoạn lỗi đưa phiên đã kết thúc có lỗi quay về Running.
                { DownloadSessionState.CompletedWithErrors, new[] { DownloadSessionState.Running } },
                { DownloadSessionState.Completed, new DownloadSessionState[0] },
                { DownloadSessionState.Cancelled, new DownloadSessionState[0] },
                { DownloadSessionState.Failed, new DownloadSessionState[0] }
            };

        private static readonly Dictionary<DownloadSegmentState, DownloadSegmentState[]> Segment =
            new Dictionary<DownloadSegmentState, DownloadSegmentState[]>
            {
                { DownloadSegmentState.Pending, new[] { DownloadSegmentState.Running, DownloadSegmentState.Skipped } },
                {
                    DownloadSegmentState.Running,
                    new[] { DownloadSegmentState.Completed, DownloadSegmentState.Failed, DownloadSegmentState.Paused, DownloadSegmentState.Skipped }
                },
                { DownloadSegmentState.Paused, new[] { DownloadSegmentState.Running, DownloadSegmentState.Skipped } },
                { DownloadSegmentState.Failed, new[] { DownloadSegmentState.Pending, DownloadSegmentState.Skipped } },
                { DownloadSegmentState.Completed, new DownloadSegmentState[0] },
                { DownloadSegmentState.Skipped, new DownloadSegmentState[0] }
            };

        public static bool CanTransition(DownloadSessionState from, DownloadSessionState to)
        {
            return System.Array.IndexOf(Session[from], to) >= 0;
        }

        public static bool CanTransition(DownloadSegmentState from, DownloadSegmentState to)
        {
            return System.Array.IndexOf(Segment[from], to) >= 0;
        }

        public static bool IsFinished(DownloadSessionState state)
        {
            return state == DownloadSessionState.Completed
                || state == DownloadSessionState.CompletedWithErrors
                || state == DownloadSessionState.Cancelled
                || state == DownloadSessionState.Failed;
        }
    }
}
