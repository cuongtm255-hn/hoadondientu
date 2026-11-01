#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using HoaDonDienTu.Application.Authentication;
using HoaDonDienTu.Application.Portal;
using HoaDonDienTu.Domain.Invoices;
using HoaDonDienTu.Infrastructure.Development.Mock;
using HoaDonDienTu.Infrastructure.Portal;
using HoaDonDienTu.Infrastructure.Portal.Config;

namespace HoaDonDienTu.Infrastructure.Tests.Portal
{
    /// <summary>Các "hình dạng API" khác nhau, chỉ khác nhau ở cấu hình; client và mock dùng chung một mã nguồn.</summary>
    internal static class Variants
    {
        public const string Default = "Cursor+Query";
        public const string Page = "Page+Query";
        public const string PostBody = "Post+Body+Base64";

        public static readonly string[] All = { Default, Page, PostBody };

        public static PortalApiConfig Create(string name)
        {
            var c = PortalApiConfigDefaults.Create();
            switch (name)
            {
                case Page:
                    c.Search.Pagination.Mode = "Page";
                    c.Search.Pagination.StartPage = 1;
                    c.Search.Response.NextCursorPath = null;
                    c.Search.Response.TotalPath = "meta.count";
                    break;

                case PostBody:
                    c.Auth.HeaderName = "X-Portal-Token";
                    c.Auth.Scheme = string.Empty;
                    c.Search.Purchase.Method = "POST";
                    c.Search.Sold.Method = "POST";
                    c.Search.ParameterLocation = "Body";
                    c.Search.DateFormat = "dd/MM/yyyy";
                    c.Search.FromParam = "dateFrom";
                    c.Search.ToParam = "dateTo";
                    c.Search.Pagination.PageSizeParam = "limit";
                    c.Search.Pagination.CursorParam = "next";
                    c.Search.Response.ItemsPath = "result.invoices";
                    c.Search.Response.NextCursorPath = "result.next";
                    c.Search.Response.TotalPath = "result.count";
                    c.Search.Response.DateFormats = new List<string> { "dd/MM/yyyy" };
                    c.Search.Response.Fields["PortalInvoiceId"] = "invoiceId";
                    c.Search.Response.Fields["InvoiceNumber"] = "no";
                    c.Search.Response.Fields["IssueDate"] = "date";
                    c.Search.Response.Fields["SellerTaxCode"] = "from.mst";
                    c.Search.Response.Fields["BuyerTaxCode"] = "to.mst";
                    c.Download.Endpoint = new EndpointConfig { Method = "POST", Path = "/api/v1/invoices/export" };
                    c.Download.ParameterLocation = "Body";
                    c.Download.Parameters = new Dictionary<string, string> { { "invoiceId", "PortalInvoiceId" }, { "seller", "SellerTaxCode" } };
                    c.Download.ResponseMode = "JsonBase64";
                    c.Download.ContentPath = "file.content";
                    break;
            }

            var errors = PortalApiConfigValidator.Validate(c);
            if (errors.Count > 0) throw new InvalidOperationException("Variant " + name + " không hợp lệ: " + string.Join("; ", errors));
            return c;
        }
    }

    internal sealed class TestPortal : IDisposable
    {
        public const string TaxCode = "0101234567";

        public TestPortal(string variant = Variants.Default, string taxCode = TaxCode, string token = null)
        {
            Config = Variants.Create(variant);
            Handler = new MockPortalHandler(Config);
            Client = NewClient(token ?? MockInvoiceDataset.TokenFor(taxCode));
            Dataset = Handler.DatasetFor(taxCode);
        }

        public PortalApiConfig Config { get; private set; }
        public MockPortalHandler Handler { get; private set; }
        public PortalHttpApiClient Client { get; private set; }
        public MockInvoiceDataset Dataset { get; private set; }

        public PortalHttpApiClient NewClient(string token)
        {
            return new PortalHttpApiClient(Config, new PortalSessionMaterial(null, null, token), Handler);
        }

        /// <summary>Đọc hết các trang của một đoạn bằng cách đi theo con trỏ cổng trả về (không suy từ số lượng).</summary>
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
