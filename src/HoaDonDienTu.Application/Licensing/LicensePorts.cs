using System.Threading;
using System.Threading.Tasks;

namespace HoaDonDienTu.Application.Licensing
{
    public enum LicenseLoadStatus
    {
        NotFound,
        Ok,

        /// <summary>File hỏng/giải mã không được. Không được tự đưa ứng dụng về Standard làm mất truy cập kho.</summary>
        Corrupt
    }

    public sealed class LicenseLoadResult
    {
        public LicenseLoadResult(LicenseLoadStatus status, StoredLicense license)
        {
            Status = status;
            License = license;
        }

        public LicenseLoadStatus Status { get; private set; }
        public StoredLicense License { get; private set; }
    }

    /// <summary>Lưu <c>license.dat</c> (DPAPI + ghi atomic) — implementation ở Infrastructure.</summary>
    public interface ILicenseStore
    {
        LicenseLoadResult Load();
        void Save(StoredLicense license);
        void Delete();
    }

    public interface IMachineIdentity
    {
        /// <summary>Mã máy đã băm; không bao giờ gửi hoặc ghi log mã chưa băm (NFR-SEC-008).</summary>
        string GetMachineIdHash();
    }

    public interface ILicenseSignatureVerifier
    {
        bool Verify(string payload, string signature);
    }

    /// <summary>Phản hồi của server cho activate/validate: grant đã ký hoặc trạng thái từ chối.</summary>
    public sealed class LicenseServerResponse
    {
        public LicenseServerResponse(LicenseGrant grant, string signature)
        {
            Grant = grant;
            Signature = signature;
        }

        public LicenseGrant Grant { get; private set; }
        public string Signature { get; private set; }
    }

    /// <summary>
    /// Contract với licence server (còn chờ gate G-06). Chỉ nhận key, mã máy đã băm và phiên bản ứng dụng (BR-025).
    /// Từ chối nghiệp vụ ném <see cref="LicenseException"/> với Failure tương ứng; lỗi mạng ném ServerUnavailable.
    /// </summary>
    public interface ILicenseServer
    {
        Task<LicenseServerResponse> ActivateAsync(string licenseKey, string machineIdHash, string appVersion, CancellationToken cancellationToken);
        Task<LicenseServerResponse> ValidateAsync(string licenseKey, string machineIdHash, string appVersion, CancellationToken cancellationToken);
        Task DeactivateAsync(string licenseKey, string machineIdHash, CancellationToken cancellationToken);
    }
}
