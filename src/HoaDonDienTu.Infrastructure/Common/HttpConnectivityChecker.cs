using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Backup;

namespace HoaDonDienTu.Infrastructure.Common
{
    public sealed class ConnectivityTarget
    {
        public ConnectivityTarget(string name, string url)
        {
            Name = name;
            Url = url;
        }

        public string Name { get; private set; }
        public string Url { get; private set; }
    }

    /// <summary>
    /// Gửi một yêu cầu HEAD tới địa chỉ gốc của từng máy chủ để biết có kết nối được không. Không gửi thông tin đăng nhập hay token,
    /// không gọi API hóa đơn; mã trạng thái bất kỳ (kể cả 401/403/404) vẫn nghĩa là máy chủ đã trả lời. Chỉ dùng ở bản Release (ADR 0005).
    /// </summary>
    public sealed class HttpConnectivityChecker : IConnectivityChecker
    {
        private readonly IList<ConnectivityTarget> _targets;
        private readonly TimeSpan _timeout;

        public HttpConnectivityChecker(IList<ConnectivityTarget> targets, TimeSpan? timeout = null)
        {
            if (targets == null) throw new ArgumentNullException("targets");
            _targets = targets;
            _timeout = timeout ?? TimeSpan.FromSeconds(8);
        }

        public async Task<IList<ConnectivityResult>> CheckAsync(CancellationToken cancellationToken)
        {
            var results = new List<ConnectivityResult>();
            foreach (var target in _targets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                results.Add(await CheckOneAsync(target, cancellationToken).ConfigureAwait(false));
            }

            return results;
        }

        private async Task<ConnectivityResult> CheckOneAsync(ConnectivityTarget target, CancellationToken token)
        {
            Uri uri;
            if (string.IsNullOrWhiteSpace(target.Url) || !Uri.TryCreate(target.Url, UriKind.Absolute, out uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            {
                return new ConnectivityResult { Name = target.Name, Reachable = false, Detail = "Chưa cấu hình địa chỉ" };
            }

            var watch = Stopwatch.StartNew();
            try
            {
                using (var client = new HttpClient { Timeout = _timeout })
                using (var request = new HttpRequestMessage(HttpMethod.Head, new Uri(uri.GetLeftPart(UriPartial.Authority))))
                using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false))
                {
                    return new ConnectivityResult { Name = target.Name, Reachable = true, Detail = "Kết nối được (" + watch.ElapsedMilliseconds + " ms, mã " + (int)response.StatusCode + ")" };
                }
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                return new ConnectivityResult { Name = target.Name, Reachable = false, Detail = "Quá thời gian chờ " + (int)_timeout.TotalSeconds + " giây" };
            }
            catch (HttpRequestException ex)
            {
                return new ConnectivityResult { Name = target.Name, Reachable = false, Detail = "Không kết nối được: " + Root(ex).Message };
            }
        }

        private static Exception Root(Exception ex)
        {
            while (ex.InnerException != null) ex = ex.InnerException;
            return ex;
        }
    }
}
