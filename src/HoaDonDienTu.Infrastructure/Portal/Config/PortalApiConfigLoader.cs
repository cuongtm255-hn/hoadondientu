using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace HoaDonDienTu.Infrastructure.Portal.Config
{
    public sealed class PortalApiConfigException : Exception
    {
        public PortalApiConfigException(IList<string> errors)
            : base("Cấu hình API cổng Thuế không hợp lệ: " + string.Join("; ", errors))
        {
            Errors = new List<string>(errors).AsReadOnly();
        }

        public IList<string> Errors { get; private set; }
    }

    public static class PortalApiConfigLoader
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            // Tên khóa của dictionary là dữ liệu (tên tham số, tên trường), không đổi kiểu chữ.
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy { ProcessDictionaryKeys = false }
            },
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Error
        };

        /// <summary>Đọc file cấu hình; thiếu file thì trả về bản nháp mặc định. File sai cấu trúc ném <see cref="PortalApiConfigException"/>.</summary>
        public static PortalApiConfig LoadOrDefault(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return PortalApiConfigDefaults.Create();
            return Parse(File.ReadAllText(path, Encoding.UTF8));
        }

        public static PortalApiConfig Parse(string json)
        {
            PortalApiConfig config;
            try
            {
                config = JsonConvert.DeserializeObject<PortalApiConfig>(json, Settings);
            }
            catch (JsonException ex)
            {
                throw new PortalApiConfigException(new[] { "Không đọc được JSON: " + ex.Message });
            }

            if (config == null) throw new PortalApiConfigException(new[] { "File cấu hình rỗng." });
            var errors = PortalApiConfigValidator.Validate(config);
            if (errors.Count > 0) throw new PortalApiConfigException(errors);
            return config;
        }

        public static string ToJson(PortalApiConfig config)
        {
            return JsonConvert.SerializeObject(config, Settings);
        }
    }
}
