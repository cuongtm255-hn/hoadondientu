using System;
using System.Linq;
using System.Net.Http;
using HoaDonDienTu.Application.Portal;
using HoaDonDienTu.Infrastructure.Portal.Config;

namespace HoaDonDienTu.Infrastructure.Portal
{
    public static class PortalErrorMapper
    {
        public static PortalErrorKind KindOf(PortalApiConfig config, int status)
        {
            string configured;
            PortalErrorKind parsed;
            if (config.Errors != null && config.Errors.StatusKinds != null
                && config.Errors.StatusKinds.TryGetValue(status, out configured)
                && Enum.TryParse(configured, false, out parsed))
            {
                return parsed;
            }

            if (status == 401 || status == 403) return PortalErrorKind.AuthExpired;
            if (status == 429) return PortalErrorKind.RateLimited;
            if (status == 408 || status >= 500) return PortalErrorKind.Transient;
            if (status == 400 || status == 422) return PortalErrorKind.FilterRejected;
            return PortalErrorKind.Fatal;
        }

        /// <summary>Thông báo chỉ có mã HTTP và loại lỗi; không đọc, không chép thân phản hồi.</summary>
        public static PortalApiException FromResponse(PortalApiConfig config, HttpResponseMessage response)
        {
            var status = (int)response.StatusCode;
            var kind = KindOf(config, status);
            return new PortalApiException(kind, "Cổng Thuế trả về HTTP " + status + " (" + kind + ").", status, RetryAfter(config, response));
        }

        private static TimeSpan? RetryAfter(PortalApiConfig config, HttpResponseMessage response)
        {
            var name = config.Errors == null || string.IsNullOrEmpty(config.Errors.RetryAfterHeader) ? "Retry-After" : config.Errors.RetryAfterHeader;
            System.Collections.Generic.IEnumerable<string> values;
            if (!response.Headers.TryGetValues(name, out values)) return null;

            var text = values.FirstOrDefault();
            int seconds;
            if (int.TryParse(text, out seconds) && seconds >= 0) return TimeSpan.FromSeconds(seconds);
            DateTimeOffset when;
            if (DateTimeOffset.TryParse(text, out when))
            {
                var delta = when - DateTimeOffset.UtcNow;
                return delta > TimeSpan.Zero ? delta : TimeSpan.Zero;
            }

            return null;
        }
    }
}
