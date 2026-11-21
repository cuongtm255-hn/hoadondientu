namespace HoaDonDienTu.Domain.PublicService
{
    /// <summary>Thông tin đăng nhập cổng Dịch vụ công do người dùng gõ. Chỉ sống trong bộ nhớ, không bao giờ ghi ra nhật ký hay đĩa.</summary>
    public sealed class DvcCredentials
    {
        public DvcCredentials(string userName, string password)
        {
            UserName = userName ?? string.Empty;
            Password = password ?? string.Empty;
        }

        public string UserName { get; private set; }

        public string Password { get; private set; }

        /// <summary>Không lộ giá trị: dùng để ghi nhật ký thì chỉ thấy nhãn.</summary>
        public override string ToString()
        {
            return "DvcCredentials(***)";
        }
    }

    public enum DvcLoginFailure
    {
        None = 0,
        MissingInput,
        WrongPassword,
        WrongCaptcha,
        Locked,
        AnotherTab,
        Network,
        Unknown
    }

    public sealed class DvcLoginResult
    {
        private DvcLoginResult(bool success, DvcLoginFailure failure, string organizationName, string portalMessage)
        {
            Success = success;
            Failure = failure;
            OrganizationName = organizationName;
            PortalMessage = portalMessage;
        }

        /// <summary>
        /// Thông báo lỗi nguyên văn của cổng (trường <c>desc</c>), đã cắt ngắn và bỏ ký tự điều khiển; chỉ có khi thất bại và dùng để hiển thị khi chưa
        /// phân loại được lỗi. Không chứa dữ liệu người dùng nhập.
        /// </summary>
        public string PortalMessage { get; private set; }

        public bool Success { get; private set; }

        public DvcLoginFailure Failure { get; private set; }

        /// <summary>Tên tổ chức do cổng trả, để người dùng đối chiếu (FR-DVC-AUTH-004).</summary>
        public string OrganizationName { get; private set; }

        public static DvcLoginResult Ok(string organizationName)
        {
            return new DvcLoginResult(true, DvcLoginFailure.None, organizationName, null);
        }

        public static DvcLoginResult Fail(DvcLoginFailure failure)
        {
            return new DvcLoginResult(false, failure, null, null);
        }

        public static DvcLoginResult Fail(DvcLoginFailure failure, string portalMessage)
        {
            return new DvcLoginResult(false, failure, null, portalMessage);
        }
    }
}
