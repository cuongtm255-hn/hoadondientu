using System;
using System.IO;
using System.Net;
using HoaDonDienTu.Application.Abstractions;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Licensing;

namespace HoaDonDienTu.Application.Common
{
    public static class CorrelationId
    {
        public static string New()
        {
            return Guid.NewGuid().ToString("N").Substring(0, 8);
        }
    }

    public interface IUserErrorMapper
    {
        UserFacingError Map(Exception exception, string operationName, string correlationId);
    }

    /// <summary>
    /// Quy đổi exception thành thông báo cho người dùng. Không đưa message gốc của exception vào thông báo
    /// vì có thể chứa dữ liệu nhạy cảm; chi tiết đã sanitize nằm trong log, tra bằng correlation ID.
    /// </summary>
    public sealed class UserErrorMapper : IUserErrorMapper
    {
        public UserFacingError Map(Exception exception, string operationName, string correlationId)
        {
            var operation = string.IsNullOrEmpty(operationName) ? "Thao tác" : operationName;

            if (exception is OperationCanceledException)
            {
                return Create("OPERATION_CANCELLED", ErrorCategory.Cancelled, operation,
                    "Thao tác đã được hủy.", "Bạn có thể thực hiện lại khi cần.", correlationId, false);
            }

            if (exception is EntitlementDeniedException)
            {
                return Create("LICENCE_DENIED", ErrorCategory.Licence, operation,
                    "Chế độ bản quyền hiện tại không cho phép thao tác này.",
                    "Kiểm tra hoặc gia hạn bản quyền ở mục Bản quyền.", correlationId, false);
            }

            var storage = exception as CompanyStorageException;
            if (storage != null)
            {
                var category = storage.Blocker == CompanySwitchBlocker.DatabaseInvalid || storage.Blocker == CompanySwitchBlocker.DatabaseLocked
                    ? ErrorCategory.Database
                    : ErrorCategory.Storage;
                return Create("COMPANY_STORAGE", category, operation,
                    "Không mở được kho dữ liệu của doanh nghiệp. Dữ liệu hiện có được giữ nguyên.",
                    "Kiểm tra thư mục dữ liệu của doanh nghiệp rồi thử lại.", correlationId, true);
            }

            if (exception is StorageGuardException)
            {
                return Create("STORAGE_GUARD", ErrorCategory.Storage, operation,
                    "Thao tác bị từ chối vì không khớp với doanh nghiệp đang mở. Không có dữ liệu nào được ghi.",
                    "Chọn đúng doanh nghiệp rồi thực hiện lại.", correlationId, false);
            }

            if (exception is WebException || IsNamed(exception, "HttpRequestException"))
            {
                return Create("NETWORK", ErrorCategory.Network, operation,
                    "Không kết nối được tới cổng Thuế.",
                    "Kiểm tra kết nối mạng rồi thử lại.", correlationId, true);
            }

            if (exception is IOException || exception is UnauthorizedAccessException)
            {
                return Create("FILE_ACCESS", ErrorCategory.Storage, operation,
                    "Không đọc hoặc ghi được file dữ liệu.",
                    "Kiểm tra quyền truy cập và dung lượng ổ đĩa rồi thử lại.", correlationId, true);
            }

            if (exception is ArgumentException || exception is FormatException)
            {
                return Create("VALIDATION", ErrorCategory.Validation, operation,
                    "Dữ liệu nhập không hợp lệ.",
                    "Kiểm tra lại thông tin đã nhập.", correlationId, false);
            }

            return Create("UNEXPECTED", ErrorCategory.Unexpected, operation,
                "Đã xảy ra lỗi không mong muốn.",
                "Thử lại; nếu lỗi lặp lại hãy cung cấp mã tham chiếu cho bộ phận hỗ trợ.", correlationId, true);
        }

        private static bool IsNamed(Exception exception, string typeName)
        {
            return exception != null && exception.GetType().Name == typeName;
        }

        private static UserFacingError Create(
            string code, ErrorCategory category, string title, string message, string action, string correlationId, bool canRetry)
        {
            return new UserFacingError(code, category, title, message, action, correlationId, canRetry);
        }
    }

    /// <summary>Hiển thị lỗi cho người dùng; presenter không gọi MessageBox trực tiếp.</summary>
    public interface IUserErrorPresenter
    {
        void Show(UserFacingError error);
    }

    public interface IConfirmationService
    {
        bool Confirm(string title, string message);
    }
}
