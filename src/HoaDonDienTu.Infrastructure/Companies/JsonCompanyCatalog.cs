using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.Infrastructure.Persistence;
using Newtonsoft.Json.Linq;

namespace HoaDonDienTu.Infrastructure.Companies
{
    /// <summary>
    /// <c>companies.json</c>: danh mục định tuyến + số liệu tổng quan lưu đệm (DD-02 mục 6). Mọi ghi đi qua
    /// <see cref="AtomicJsonWriter"/> và giữ <c>.bak</c>; file hỏng không có bản .bak hợp lệ thì từ chối ghi để không tạo
    /// danh mục rỗng đè lên dữ liệu có thể khôi phục (NFR-REL-007). Không mở SQLite của bất kỳ MST nào.
    /// </summary>
    public sealed class JsonCompanyCatalog : ICompanyRegistry
    {
        private readonly string _path;
        private readonly AtomicJsonWriter _writer = new AtomicJsonWriter();
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);

        public JsonCompanyCatalog(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("path");
            _path = path;
        }

        public async Task<CatalogSnapshot> LoadAsync(CancellationToken cancellationToken)
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var state = ReadState();
                if (state.Status == AtomicReadStatus.NotFound) return CatalogSnapshot.Empty();
                if (state.Status == AtomicReadStatus.Corrupt) return new CatalogSnapshot(CatalogLoadStatus.Corrupt, null, false);

                var profiles = state.Items.Select(i => i.ToProfile()).ToList();
                var history = state.HasServiceHistory || state.Items.Count > 1;
                return new CatalogSnapshot(CatalogLoadStatus.Loaded, profiles, history, state.Status == AtomicReadStatus.RecoveredFromBackup);
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task<IList<CompanyCatalogItem>> GetItemsAsync(CancellationToken cancellationToken)
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var state = ReadState();
                if (state.Status == AtomicReadStatus.Corrupt)
                {
                    throw new CompanyCatalogException(CompanyCatalogErrorKind.CatalogCorrupt, "Danh mục doanh nghiệp bị hỏng.");
                }

                // Trạng thái thư mục chỉ kiểm tra sự tồn tại (rẻ); không mở kho để dựng danh sách.
                return state.Items
                    .Select(i => i.Status == CompanyCatalogStatus.Active && !Directory.Exists(i.StoragePath) ? i.WithStatus(CompanyCatalogStatus.PathMissing) : i)
                    .ToList();
            }
            finally
            {
                _gate.Release();
            }
        }

        public Task AddAsync(CompanyCatalogItem item, CancellationToken cancellationToken)
        {
            return Mutate(cancellationToken, state =>
            {
                if (state.Items.Any(i => i.TaxCode.Equals(item.TaxCode)))
                {
                    throw new CompanyCatalogException(CompanyCatalogErrorKind.DuplicateTaxCode, "MST đã có trong danh mục.");
                }

                // Có từ hai doanh nghiệp trở lên nghĩa là từng chạy ServiceMode; cờ này không bị xóa khi ngừng quản lý.
                if (state.Items.Count >= 1) state.HasServiceHistory = true;
                state.Items.Add(item);
            });
        }

        public Task UpdateAsync(CompanyCatalogItem item, CancellationToken cancellationToken)
        {
            return Mutate(cancellationToken, state =>
            {
                var index = state.Items.FindIndex(i => i.Id.Equals(item.Id));
                if (index < 0) throw new CompanyCatalogException(CompanyCatalogErrorKind.NotFound, "Không tìm thấy doanh nghiệp trong danh mục.");
                state.Items[index] = item;
            });
        }

        public Task RemoveAsync(CompanyId id, CancellationToken cancellationToken)
        {
            return Mutate(cancellationToken, state =>
            {
                var removed = state.Items.RemoveAll(i => i.Id.Equals(id));
                if (removed == 0) throw new CompanyCatalogException(CompanyCatalogErrorKind.NotFound, "Không tìm thấy doanh nghiệp trong danh mục.");
            });
        }

        private async Task Mutate(CancellationToken cancellationToken, Action<State> change)
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var state = ReadState();
                if (state.Status == AtomicReadStatus.Corrupt)
                {
                    throw new CompanyCatalogException(CompanyCatalogErrorKind.CatalogCorrupt, "Danh mục doanh nghiệp bị hỏng; không ghi đè để tránh mất dữ liệu.");
                }

                change(state);
                _writer.Write(_path, Serialize(state), IsValidCatalog);
            }
            finally
            {
                _gate.Release();
            }
        }

        private State ReadState()
        {
            var read = _writer.Read(_path, IsValidCatalog);
            if (read.Status == AtomicReadStatus.NotFound) return new State { Status = AtomicReadStatus.NotFound, Items = new List<CompanyCatalogItem>() };
            if (read.Status == AtomicReadStatus.Corrupt) return new State { Status = AtomicReadStatus.Corrupt, Items = new List<CompanyCatalogItem>() };

            var json = JObject.Parse(read.Content);
            var state = new State
            {
                Status = read.Status,
                HasServiceHistory = (bool?)json["hasServiceHistory"] ?? false,
                Items = ((JArray)json["companies"]).Select(ParseItem).ToList()
            };
            return state;
        }

        private static CompanyCatalogItem ParseItem(JToken token)
        {
            CompanyCatalogStatus status;
            if (!Enum.TryParse((string)token["status"], false, out status)) status = CompanyCatalogStatus.Active;
            return new CompanyCatalogItem(
                new CompanyId(Guid.Parse((string)token["id"])),
                TaxCode.Parse((string)token["taxCode"]),
                (string)token["displayName"],
                (string)token["storagePath"],
                Date(token["lastOpenedAt"]),
                Date(token["lastDownloadedAt"]),
                (long?)token["cachedInvoiceCount"] ?? 0,
                (long?)token["cachedStorageBytes"] ?? 0,
                status);
        }

        private static string Serialize(State state)
        {
            var root = new JObject
            {
                { "schemaVersion", 1 },
                { "hasServiceHistory", state.HasServiceHistory },
                { "companies", new JArray(state.Items.Select(i => new JObject
                    {
                        { "id", i.Id.ToString() },
                        { "taxCode", i.TaxCode.Value },
                        { "displayName", i.DisplayName },
                        { "storagePath", i.StoragePath },
                        { "lastOpenedAt", i.LastOpenedAt.HasValue ? (JToken)i.LastOpenedAt.Value.ToString("o", CultureInfo.InvariantCulture) : JValue.CreateNull() },
                        { "lastDownloadedAt", i.LastDownloadedAt.HasValue ? (JToken)i.LastDownloadedAt.Value.ToString("o", CultureInfo.InvariantCulture) : JValue.CreateNull() },
                        { "cachedInvoiceCount", i.CachedInvoiceCount },
                        { "cachedStorageBytes", i.CachedStorageBytes },
                        { "status", i.Status.ToString() }
                    })) }
            };
            return root.ToString(Newtonsoft.Json.Formatting.Indented);
        }

        private static DateTime? Date(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null) return null;
            return DateTime.Parse((string)token, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }

        /// <summary>Hợp lệ khi đúng schemaVersion và mọi bản ghi đọc được; ném lỗi parse được coi là không hợp lệ.</summary>
        private static bool IsValidCatalog(string text)
        {
            var json = JObject.Parse(text);
            if ((int?)json["schemaVersion"] != 1) return false;
            var companies = json["companies"] as JArray;
            if (companies == null) return false;
            foreach (var token in companies) ParseItem(token);
            return true;
        }

        private sealed class State
        {
            public AtomicReadStatus Status;
            public bool HasServiceHistory;
            public List<CompanyCatalogItem> Items;
        }
    }
}
