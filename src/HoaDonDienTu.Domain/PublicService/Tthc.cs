using System;
using System.Collections.Generic;

namespace HoaDonDienTu.Domain.PublicService
{
    /// <summary>Một lựa chọn trong ô lọc (mã do cổng đặt, nhãn hiển thị).</summary>
    public sealed class TthcOption
    {
        public TthcOption(string code, string label)
        {
            Code = code ?? string.Empty;
            Label = label ?? string.Empty;
        }

        public string Code { get; private set; }

        public string Label { get; private set; }

        public override string ToString()
        {
            return Label;
        }
    }

    /// <summary>Danh sách lựa chọn của bốn ô lọc, đọc từ chính trang cổng (không mã hóa cứng).</summary>
    public sealed class TthcFilterOptions
    {
        public static readonly TthcFilterOptions Empty = new TthcFilterOptions(null, null, null, null);

        public TthcFilterOptions(
            IList<TthcOption> procedureTypes, IList<TthcOption> declarations, IList<TthcOption> levels, IList<TthcOption> taxpayerTypes)
        {
            ProcedureTypes = Freeze(procedureTypes);
            Declarations = Freeze(declarations);
            Levels = Freeze(levels);
            TaxpayerTypes = Freeze(taxpayerTypes);
        }

        public IList<TthcOption> ProcedureTypes { get; private set; }

        public IList<TthcOption> Declarations { get; private set; }

        public IList<TthcOption> Levels { get; private set; }

        public IList<TthcOption> TaxpayerTypes { get; private set; }

        private static IList<TthcOption> Freeze(IList<TthcOption> source)
        {
            return new List<TthcOption>(source ?? new TthcOption[0]).AsReadOnly();
        }
    }

    /// <summary>Điều kiện tìm danh mục TTHC. Mỗi trường lọc là mã lọc của cổng hoặc null/rỗng (không lọc).</summary>
    public sealed class TthcQuery
    {
        public string Keyword { get; set; }

        public string ProcedureType { get; set; }

        public string Declaration { get; set; }

        public string Level { get; set; }

        public string TaxpayerType { get; set; }

        /// <summary>Trang cần lấy, bắt đầu từ 1.</summary>
        public int PageNumber { get; set; }

        public TthcQuery Clone()
        {
            return (TthcQuery)MemberwiseClone();
        }
    }

    /// <summary>Một dòng của bảng danh mục: Mã, Tên thủ tục, Mức độ, Cơ quan thực hiện.</summary>
    public sealed class TthcSummary
    {
        public TthcSummary(string code, string name, string level, string agency)
        {
            Code = code ?? string.Empty;
            Name = name ?? string.Empty;
            Level = level ?? string.Empty;
            Agency = agency ?? string.Empty;
        }

        public string Code { get; private set; }

        public string Name { get; private set; }

        public string Level { get; private set; }

        public string Agency { get; private set; }
    }

    public sealed class TthcPage
    {
        public const int DefaultPageSize = 10;

        public TthcPage(IList<TthcSummary> items, int totalRecords, int totalProcedures, int pageNumber, int pageCount)
        {
            Items = new List<TthcSummary>(items ?? new TthcSummary[0]).AsReadOnly();
            TotalRecords = totalRecords;
            TotalProcedures = totalProcedures;
            PageNumber = pageNumber;
            PageCount = pageCount;
        }

        public IList<TthcSummary> Items { get; private set; }

        /// <summary>"Tổng số bản ghi" của cổng (một TTHC có thể có nhiều bản ghi theo cơ quan).</summary>
        public int TotalRecords { get; private set; }

        public int TotalProcedures { get; private set; }

        public int PageNumber { get; private set; }

        public int PageCount { get; private set; }
    }

    /// <summary>Chi tiết một TTHC: mỗi mục là văn bản thuần đã làm sạch (không HTML).</summary>
    public sealed class TthcDetail
    {
        public TthcDetail(string code, string name, IList<KeyValuePair<string, string>> sections)
        {
            Code = code ?? string.Empty;
            Name = name ?? string.Empty;
            Sections = new List<KeyValuePair<string, string>>(sections ?? new KeyValuePair<string, string>[0]).AsReadOnly();
        }

        public string Code { get; private set; }

        public string Name { get; private set; }

        /// <summary>Các mục theo thứ tự của cổng: Cấp thực hiện, Trình tự, Cách thức, Thành phần hồ sơ, Đối tượng, Cơ quan, Địa chỉ tiếp nhận, Kết quả, Điều kiện, Căn cứ pháp lý.</summary>
        public IList<KeyValuePair<string, string>> Sections { get; private set; }
    }

    /// <summary>Cổng đổi giao diện nên không đọc được dữ liệu (bộ phân tích không nhận ra mẫu).</summary>
    public sealed class TthcParseException : Exception
    {
        public TthcParseException(string message) : base(message)
        {
        }
    }
}
