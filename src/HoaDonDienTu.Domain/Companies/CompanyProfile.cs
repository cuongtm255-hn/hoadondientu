using System;

namespace HoaDonDienTu.Domain.Companies
{
    public struct CompanyId : IEquatable<CompanyId>
    {
        private readonly Guid _value;

        public CompanyId(Guid value)
        {
            _value = value;
        }

        public Guid Value
        {
            get { return _value; }
        }

        public static CompanyId NewId()
        {
            return new CompanyId(Guid.NewGuid());
        }

        public bool Equals(CompanyId other)
        {
            return _value == other._value;
        }

        public override bool Equals(object obj)
        {
            return obj is CompanyId && Equals((CompanyId)obj);
        }

        public override int GetHashCode()
        {
            return _value.GetHashCode();
        }

        public override string ToString()
        {
            return _value.ToString("D");
        }
    }

    /// <summary>Thông tin tối thiểu của một doanh nghiệp/MST; chi tiết bổ sung ở DD-02.</summary>
    public sealed class CompanyProfile
    {
        public CompanyProfile(CompanyId id, string displayName, TaxCode taxCode)
        {
            if (taxCode == null) throw new ArgumentNullException("taxCode");
            Id = id;
            DisplayName = displayName ?? string.Empty;
            TaxCode = taxCode;
        }

        public CompanyId Id { get; private set; }
        public string DisplayName { get; private set; }
        public TaxCode TaxCode { get; private set; }
    }
}
