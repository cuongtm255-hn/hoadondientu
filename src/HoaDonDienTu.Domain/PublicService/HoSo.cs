using System;
using System.Collections.Generic;

namespace HoaDonDienTu.Domain.PublicService
{
    /// <summary>Hai nguồn tra cứu hồ sơ đã nộp của cổng Dịch vụ công.</summary>
    public enum HoSoSource
    {
        /// <summary>Hồ sơ đã nộp trên Dịch vụ công.</summary>
        Dvc = 0,

        /// <summary>Hồ sơ (tờ khai) đã nộp trên thuế điện tử, chỉ trước 01/07/2025.</summary>
        ThueDienTu = 1
    }

    /// <summary>
    /// Điều kiện tìm hồ sơ đã nộp. <see cref="CaptchaText"/> chỉ sống trong bộ nhớ của MỘT lần gửi: không bao giờ được ghi nhật ký,
    /// lưu đĩa hay dùng lại (BR-DVC-03).
    /// </summary>
    public sealed class HoSoQuery
    {
        public HoSoSource Source { get; set; }

        public DateTime FromDate { get; set; }

        public DateTime ToDate { get; set; }

        /// <summary>MST ủy quyền (nếu có).</summary>
        public string AuthorizedTaxCode { get; set; }

        // Tab Dịch vụ công
        public string ProcedureType { get; set; }

        public string ProcedureName { get; set; }

        public string FileCode { get; set; }

        // Dùng chung hai tab
        public string Declaration { get; set; }

        // Tab thuế điện tử
        public string TransactionCode { get; set; }

        /// <summary>Trang cần lấy, bắt đầu từ 1.</summary>
        public int PageNumber { get; set; }

        /// <summary>Mã CAPTCHA người dùng vừa gõ cho lần gửi này.</summary>
        public string CaptchaText { get; set; }

        public HoSoQuery Clone()
        {
            return (HoSoQuery)MemberwiseClone();
        }

        /// <summary>Bản sao đã bỏ mã CAPTCHA, dùng để nhớ bộ lọc đã áp dụng.</summary>
        public HoSoQuery WithoutCaptcha()
        {
            HoSoQuery copy = Clone();
            copy.CaptchaText = null;
            return copy;
        }
    }

    /// <summary>Một dòng kết quả: các ô văn bản theo thứ tự cột của cổng (chưa biết cột cụ thể, nên không đặt tên trường).</summary>
    public sealed class HoSoRow
    {
        public HoSoRow(IList<string> cells)
        {
            Cells = new List<string>(cells ?? new string[0]).AsReadOnly();
        }

        public IList<string> Cells { get; private set; }
    }

    public sealed class HoSoPage
    {
        public const int DefaultPageSize = 10;

        public HoSoPage(IList<string> columns, IList<HoSoRow> rows, int totalRecords, int pageNumber, int pageCount, string emptyMessage)
        {
            Columns = new List<string>(columns ?? new string[0]).AsReadOnly();
            Rows = new List<HoSoRow>(rows ?? new HoSoRow[0]).AsReadOnly();
            TotalRecords = totalRecords;
            PageNumber = pageNumber;
            PageCount = pageCount;
            EmptyMessage = emptyMessage ?? string.Empty;
        }

        public IList<string> Columns { get; private set; }

        public IList<HoSoRow> Rows { get; private set; }

        public int TotalRecords { get; private set; }

        public int PageNumber { get; private set; }

        public int PageCount { get; private set; }

        /// <summary>Thông điệp của cổng khi không có dữ liệu (rỗng nếu có dòng).</summary>
        public string EmptyMessage { get; private set; }
    }

    /// <summary>Cổng đổi giao diện nên không đọc được kết quả hồ sơ.</summary>
    public sealed class HoSoParseException : Exception
    {
        public HoSoParseException(string message) : base(message)
        {
        }
    }
}
