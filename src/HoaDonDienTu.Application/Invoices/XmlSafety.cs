using System.IO;
using System.Xml;

namespace HoaDonDienTu.Application.Invoices
{
    /// <summary>
    /// Kiểm tra nhanh, theo luồng, trước khi dùng bộ xử lý XML có đệ quy (chuẩn hóa C14N của chữ ký số, duyệt cây): XML lồng quá sâu có thể làm
    /// tràn ngăn xếp, mà tràn ngăn xếp không bắt được và làm chết cả ứng dụng (P6-03, NFR-SEC-003). Hóa đơn thật lồng khoảng 10 cấp.
    /// </summary>
    public static class XmlSafety
    {
        public const int MaxDepth = 128;

        /// <exception cref="XmlException">XML lồng quá <see cref="MaxDepth"/> cấp, có DTD hoặc không đọc được.</exception>
        public static void EnsureShallow(byte[] xml)
        {
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            using (var reader = XmlReader.Create(new MemoryStream(xml), settings))
            {
                while (reader.Read())
                {
                    if (reader.Depth > MaxDepth) throw new XmlException("XML lồng quá sâu.");
                }
            }
        }
    }
}
