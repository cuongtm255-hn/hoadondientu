using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HoaDonDienTu.Application.Exports
{
    public enum ExportKind
    {
        Excel = 0,
        Zip = 1
    }

    /// <summary>Một lần xuất đã hoàn tất, lưu trong <c>company.db</c> (DD-09 mục 5). Không chứa nội dung hóa đơn, chỉ phạm vi, nơi lưu và kết quả.</summary>
    public sealed class ExportRecord
    {
        public Guid Id { get; set; }
        public ExportKind Kind { get; set; }
        public string TargetPath { get; set; }

        /// <summary>Mô tả phạm vi như dòng ghi trong file Excel (toàn bộ kết quả đang lọc hay các hóa đơn đã chọn).</summary>
        public string ScopeText { get; set; }

        public int ExportedCount { get; set; }

        /// <summary>Số hóa đơn bị bỏ qua khi xuất ZIP (XML thiếu hoặc lỗi).</summary>
        public int SkippedCount { get; set; }

        /// <summary>Mỗi hóa đơn bị bỏ qua một dòng, tối đa <see cref="MaxSkippedLines"/> dòng; phần còn lại chỉ được đếm trong <see cref="SkippedCount"/>.</summary>
        public string SkippedSummary { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        public const int MaxSkippedLines = 50;
    }

    public interface IExportHistoryRepository
    {
        Task AddAsync(ExportRecord record, CancellationToken cancellationToken);

        /// <summary>Các lần xuất gần nhất của doanh nghiệp đang mở, mới nhất trước.</summary>
        Task<IList<ExportRecord>> ListRecentAsync(int max, CancellationToken cancellationToken);
    }

    /// <summary>Lưu trong bộ nhớ cho thử nghiệm và chế độ giả; mất khi tắt ứng dụng.</summary>
    public sealed class InMemoryExportHistoryRepository : IExportHistoryRepository
    {
        private readonly object _gate = new object();
        private readonly List<ExportRecord> _records = new List<ExportRecord>();

        public Task AddAsync(ExportRecord record, CancellationToken cancellationToken)
        {
            if (record == null) throw new ArgumentNullException("record");
            lock (_gate) _records.Add(record);
            return Task.FromResult(0);
        }

        public Task<IList<ExportRecord>> ListRecentAsync(int max, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                IList<ExportRecord> list = _records.OrderByDescending(r => r.CreatedAtUtc).Take(Math.Max(0, max)).ToList();
                return Task.FromResult(list);
            }
        }
    }
}
