namespace HoaDonDienTu.Infrastructure.Tests.Portal
{
    /// <summary>
    /// Phản hồi tra cứu rút gọn từ mẫu thật trong sample-api-web.md mục 2 (hóa đơn bán ra, tên trường và kiểu dữ liệu giữ nguyên:
    /// <c>shdon</c> là số, <c>khmshdon</c> là số, <c>tdlap</c> là thời điểm UTC của 0 giờ ngày lập giờ Việt Nam).
    /// </summary>
    internal static class RealSamples
    {
        public const string SoldSearchResponse =
            "{\"datas\":["
            + "{\"nbmst\":\"0109213133\",\"khmshdon\":1,\"khhdon\":\"C26TBN\",\"shdon\":40,\"cqt\":\"0155\",\"dvtte\":\"VND\",\"hdon\":\"01\","
            + "\"id\":\"d2240f37-51af-48d9-98f1-f01d4a0810ff\",\"nbten\":\"CÔNG TY TNHH BAY NGAY\",\"nmmst\":\"0101654633\","
            + "\"nmten\":\"CÔNG TY CỔ PHẦN CÔNG NGHỆ SINH HỌC THÚ Y\",\"nmdchi\":\"Số 19, ngõ 37, phố Đông Các, Phường Đống Đa, Thành phố Hà Nội, Việt Nam\","
            + "\"nmcmnd\":null,\"tdlap\":\"2026-10-30T17:00:00Z\",\"tgia\":1.0,\"tgtcthue\":2873316.0,\"tgtthue\":229865.0,\"tgtttbso\":3103181.0,"
            + "\"ttcktmai\":null,\"tgtphi\":null,\"tthai\":1,\"ttxly\":5,\"unhiem\":null,\"hdonLquans\":null},"
            + "{\"nbmst\":\"0109213133\",\"khmshdon\":1,\"khhdon\":\"C26TBN\",\"shdon\":39,\"dvtte\":\"VND\","
            + "\"id\":\"ec72e1a9-89cc-48ba-a6a1-f4bacc0ac040\",\"nbten\":\"CÔNG TY TNHH BAY NGAY\",\"nmmst\":null,"
            + "\"nmten\":\"Nguyễn Văn Mai\",\"nmdchi\":\"Thái Phù, Xã Sóc Sơn, Thành phố Hà Nội, Việt Nam\",\"nmcmnd\":\"001092008840\","
            + "\"tdlap\":\"2026-10-29T17:00:00Z\",\"tgia\":1.0,\"tgtcthue\":11451006.0,\"tgtthue\":916080.0,\"tgtttbso\":12367086.0,"
            + "\"tthai\":1,\"ttxly\":5,\"unhiem\":1}"
            + "],\"total\":2,\"state\":null,\"time\":133}";
    }
}
