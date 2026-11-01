using System;
using System.Collections.Generic;
using HoaDonDienTu.Domain.Companies;

namespace HoaDonDienTu.Application.Authentication
{
    /// <summary>
    /// Dữ liệu phiên thu từ trình duyệt đăng nhập (cookie/header/token). Không giả định token nằm ở đâu;
    /// cấu trúc cụ thể được chốt sau khi có ADR WebView + API discovery (G-01, G-02).
    /// Chứa bí mật: không log, không serialize, ToString đã che.
    /// </summary>
    public sealed class PortalSessionMaterial
    {
        public PortalSessionMaterial(
            IDictionary<string, string> cookies,
            IDictionary<string, string> headers,
            string bearerToken)
        {
            Cookies = new Dictionary<string, string>(cookies ?? new Dictionary<string, string>());
            Headers = new Dictionary<string, string>(headers ?? new Dictionary<string, string>());
            BearerToken = bearerToken;
        }

        public IDictionary<string, string> Cookies { get; private set; }
        public IDictionary<string, string> Headers { get; private set; }
        public string BearerToken { get; private set; }

        public override string ToString()
        {
            return "PortalSessionMaterial(***)";
        }
    }

    /// <summary>Phiên đã được xác minh MST. Chỉ tồn tại trong bộ nhớ.</summary>
    public sealed class PortalSession
    {
        public PortalSession(CompanyId companyId, TaxCode verifiedTaxCode, PortalSessionMaterial material, DateTime establishedAtUtc)
        {
            if (verifiedTaxCode == null) throw new ArgumentNullException("verifiedTaxCode");
            if (material == null) throw new ArgumentNullException("material");
            CompanyId = companyId;
            VerifiedTaxCode = verifiedTaxCode;
            Material = material;
            EstablishedAtUtc = establishedAtUtc;
        }

        public CompanyId CompanyId { get; private set; }
        public TaxCode VerifiedTaxCode { get; private set; }
        public PortalSessionMaterial Material { get; private set; }
        public DateTime EstablishedAtUtc { get; private set; }

        public override string ToString()
        {
            return "PortalSession(" + VerifiedTaxCode + ", ***)";
        }
    }

    /// <summary>Ảnh chụp cho UI/log: tuyệt đối không chứa token, cookie hay header.</summary>
    public sealed class PortalSessionSnapshot
    {
        public PortalSessionSnapshot(CompanyId companyId, TaxCode taxCode, DateTime establishedAtUtc, int generation)
        {
            CompanyId = companyId;
            TaxCode = taxCode;
            EstablishedAtUtc = establishedAtUtc;
            Generation = generation;
        }

        public CompanyId CompanyId { get; private set; }
        public TaxCode TaxCode { get; private set; }
        public DateTime EstablishedAtUtc { get; private set; }

        /// <summary>Tăng mỗi lần Set; client API gắn với đúng một generation.</summary>
        public int Generation { get; private set; }
    }
}
