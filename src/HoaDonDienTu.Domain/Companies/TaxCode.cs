using System;
using System.Linq;

namespace HoaDonDienTu.Domain.Companies
{
    /// <summary>MST: 10 chữ số, hoặc 10 chữ số + "-" + 3 chữ số cho đơn vị phụ thuộc.</summary>
    public sealed class TaxCode : IEquatable<TaxCode>
    {
        private TaxCode(string value)
        {
            Value = value;
        }

        public string Value { get; private set; }

        public static bool TryParse(string input, out TaxCode taxCode)
        {
            taxCode = null;
            var text = (input ?? string.Empty).Trim();
            var main = text;
            if (text.Length == 14 && text[10] == '-')
            {
                main = text.Substring(0, 10) + text.Substring(11);
            }
            else if (text.Length != 10)
            {
                return false;
            }

            if (main.Length != 10 && main.Length != 13) return false;
            if (!main.All(c => c >= '0' && c <= '9')) return false;

            taxCode = new TaxCode(text);
            return true;
        }

        public static TaxCode Parse(string input)
        {
            TaxCode result;
            if (!TryParse(input, out result))
            {
                throw new FormatException("MST không hợp lệ: " + input);
            }

            return result;
        }

        public bool Equals(TaxCode other)
        {
            return other != null && string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as TaxCode);
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value;
        }
    }
}
