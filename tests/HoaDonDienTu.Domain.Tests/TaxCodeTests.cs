using HoaDonDienTu.Domain.Companies;
using NUnit.Framework;

namespace HoaDonDienTu.Domain.Tests
{
    [TestFixture]
    public class TaxCodeTests
    {
        [TestCase("0101234567")]
        [TestCase("0101234567-001")]
        [TestCase(" 0101234567 ")]
        public void TryParse_Valid(string input)
        {
            TaxCode code;
            Assert.That(TaxCode.TryParse(input, out code), Is.True);
            Assert.That(code.Value, Is.EqualTo(input.Trim()));
        }

        [TestCase("")]
        [TestCase(null)]
        [TestCase("123")]
        [TestCase("01012345AB")]
        [TestCase("0101234567-01")]
        [TestCase("0101234567_001")]
        public void TryParse_Invalid(string input)
        {
            TaxCode code;
            Assert.That(TaxCode.TryParse(input, out code), Is.False);
        }
    }
}
