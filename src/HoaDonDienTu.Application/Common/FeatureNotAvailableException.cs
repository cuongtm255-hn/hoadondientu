using System;

namespace HoaDonDienTu.Application.Common
{
    /// <summary>Chức năng chưa có trong bản này. Thông điệp an toàn để hiển thị trực tiếp cho người dùng.</summary>
    public sealed class FeatureNotAvailableException : Exception
    {
        public FeatureNotAvailableException(string message)
            : base(message)
        {
        }
    }
}
