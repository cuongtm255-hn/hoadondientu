using System;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Domain.Licensing;

namespace HoaDonDienTu.Application.Licensing
{
    public enum LicenseFailure
    {
        /// <summary>Key không tồn tại hoặc sai định dạng.</summary>
        KeyInvalid,
        KeyBoundToOtherMachine,
        Expired,
        Revoked,

        /// <summary>Chữ ký/mã máy trong phản hồi không hợp lệ.</summary>
        ResponseRejected,

        /// <summary>Mất mạng, timeout hoặc 5xx; không đồng nghĩa thu hồi (FR-LIC-005).</summary>
        ServerUnavailable,
        StorageFailed
    }

    public sealed class LicenseException : Exception
    {
        public LicenseException(LicenseFailure failure, string message)
            : base(message)
        {
            Failure = failure;
        }

        public LicenseFailure Failure { get; private set; }
    }

    /// <summary>Contract theo DD-03 mục 3.2. Lỗi nghiệp vụ ném <see cref="LicenseException"/>.</summary>
    public interface ILicenseService
    {
        Task<LicenseStatus> ValidateLocalAsync(CancellationToken cancellationToken);
        Task<LicenseStatus> ValidateOnlineAsync(CancellationToken cancellationToken);
        Task<LicenseStatus> ActivateAsync(string licenseKey, CancellationToken cancellationToken);
        Task<LicenseStatus> DeactivateAsync(CancellationToken cancellationToken);

        /// <summary>Kết quả đánh giá gần nhất (sau ValidateLocal/Online/Activate); chưa đánh giá thì là NotActivated.</summary>
        LicenseInfo CurrentInfo { get; }

        event EventHandler InfoChanged;
    }
}
