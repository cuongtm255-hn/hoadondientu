#if DEBUG
using System;
using System.Collections.Generic;
using System.Threading;
using HoaDonDienTu.Application.Authentication;
using HoaDonDienTu.Application.Portal;
using HoaDonDienTu.Domain.Invoices;
using HoaDonDienTu.Infrastructure.Development.Mock;
using HoaDonDienTu.Infrastructure.Portal;
using HoaDonDienTu.Infrastructure.Portal.Config;

namespace HoaDonDienTu.Infrastructure.Tests.Portal
{
    /// <summary>Client thật chạy trên cổng giả có hình dạng API thật, cùng một <see cref="PortalApiConfig"/>.</summary>
    internal sealed class TestPortal : IDisposable
    {
        public const string TaxCode = "0101234567";

        public TestPortal(string taxCode = TaxCode, string token = null, PortalApiConfig config = null, string userAgent = null, IDictionary<string, string> cookies = null)
        {
            Config = config ?? PortalApiConfigDefaults.Create();
            Handler = new MockPortalHandler(Config);
            Client = NewClient(token ?? MockInvoiceDataset.TokenFor(taxCode), userAgent, cookies);
            Dataset = Handler.DatasetFor(taxCode);
        }

        public PortalApiConfig Config { get; private set; }
        public MockPortalHandler Handler { get; private set; }
        public PortalHttpApiClient Client { get; private set; }
        public MockInvoiceDataset Dataset { get; private set; }

        public PortalHttpApiClient NewClient(string token, string userAgent = null, IDictionary<string, string> cookies = null)
        {
            return new PortalHttpApiClient(Config, new PortalSessionMaterial(cookies, null, token, userAgent), Handler);
        }

        /// <summary>Đọc hết các trang của một đoạn bằng cách đi theo con trỏ <c>state</c> cổng trả về (không suy từ số lượng).</summary>
        public IList<PortalInvoiceDto> SearchAll(InvoiceDirection direction, DateTime from, DateTime to, InvoiceSearchFilter filter = null, int? pageSize = null)
        {
            var result = new List<PortalInvoiceDto>();
            string cursor = null;
            var calls = 0;
            do
            {
                var request = new InvoiceSearchRequest(direction, from, to) { Cursor = cursor, PageSize = pageSize, Filter = filter ?? new InvoiceSearchFilter() };
                var page = Client.SearchAsync(request, CancellationToken.None).GetAwaiter().GetResult();
                result.AddRange(page.Items);
                cursor = page.NextCursor;
                if (++calls > 200) throw new InvalidOperationException("Phân trang không dừng.");
            }
            while (cursor != null);

            Pages = calls;
            return result;
        }

        public int Pages { get; private set; }

        public void Dispose()
        {
            Client.Dispose();
        }
    }
}
#endif
