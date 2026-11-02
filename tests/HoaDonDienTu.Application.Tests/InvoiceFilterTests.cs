using System.Linq;
using HoaDonDienTu.Application.Downloads;
using HoaDonDienTu.Application.Portal;
using HoaDonDienTu.Domain.Invoices;
using NUnit.Framework;

namespace HoaDonDienTu.Application.Tests
{
    [TestFixture]
    public class InvoiceFilterTests
    {
        [Test]
        public void EmptyFilter_IsValid()
        {
            Assert.That(InvoiceFilterValidator.Validate(new InvoiceSearchFilter()), Is.Empty);
            Assert.That(InvoiceFilterValidator.Validate(null), Is.Empty);
        }

        [TestCase("0109213133")]
        [TestCase("0100100006-001")]
        [TestCase("0100100006001")]
        public void TaxCode_ValidForms(string value)
        {
            Assert.That(InvoiceFilterValidator.Validate(new InvoiceSearchFilter { CounterpartyTaxCode = value }), Is.Empty);
        }

        [TestCase("123")]
        [TestCase("01092131330")]
        [TestCase("0109213133a")]
        [TestCase("0100100006-01")]
        public void TaxCode_InvalidForms(string value)
        {
            Assert.That(InvoiceFilterValidator.Validate(new InvoiceSearchFilter { CounterpartyTaxCode = value }), Has.Count.EqualTo(1));
        }

        [TestCase("036402421225", true)]
        [TestCase("001179037", true)]
        [TestCase("03640242122", false)]
        [TestCase("03640242122a", false)]
        public void IdNumber_NineOrTwelveDigits(string value, bool valid)
        {
            Assert.That(InvoiceFilterValidator.Validate(new InvoiceSearchFilter { BuyerIdNumber = value }).Count == 0, Is.EqualTo(valid));
        }

        [TestCase("323", true)]
        [TestCase("00000040", true)]
        [TestCase("123456789", false)]
        [TestCase("12ab", false)]
        public void InvoiceNumber_UpToEightDigits(string value, bool valid)
        {
            Assert.That(InvoiceFilterValidator.Validate(new InvoiceSearchFilter { InvoiceNumber = value }).Count == 0, Is.EqualTo(valid));
        }

        [TestCase("C26TBN", true)]
        [TestCase("121", true)]
        [TestCase("C26-TBN", false)]
        [TestCase("A;tthai==6", false)]
        public void InvoiceSymbol_LettersAndDigitsOnly(string value, bool valid)
        {
            Assert.That(InvoiceFilterValidator.Validate(new InvoiceSearchFilter { InvoiceSymbol = value }).Count == 0, Is.EqualTo(valid));
        }

        [TestCase("5", true)]
        [TestCase("6", true)]
        [TestCase("8", true)]
        [TestCase("0", false)]
        [TestCase("1", false)]
        [TestCase("7", false)]
        [TestCase("", true)]
        public void PurchaseCheckResult_OnlyAccepts5_6_8(string code, bool valid)
        {
            var errors = InvoiceFilterValidator.Validate(new InvoiceSearchFilter { CheckResultCode = code }, InvoiceDirection.Purchase);

            Assert.That(errors.Count == 0, Is.EqualTo(valid));
        }

        [Test]
        public void CheckResultOptions_PurchaseHasNoAllChoice_SoldHasAllNine()
        {
            Assert.That(PortalOptions.CheckResultsFor(InvoiceDirection.Purchase).Select(o => o.Code).Where(c => c.Length > 0), Is.EqualTo(new[] { "5", "6", "8" }));
            Assert.That(PortalOptions.CheckResultsFor(InvoiceDirection.Sold), Has.Count.EqualTo(10));
            Assert.That(PortalOptions.CheckResultPasses(InvoiceDirection.Purchase), Is.EqualTo(new[] { "5", "6", "8" }));
            Assert.That(PortalOptions.CheckResultPasses(InvoiceDirection.Sold), Is.Empty);
        }

        [Test]
        public void Codes_MustBeThePortalCodes()
        {
            Assert.That(InvoiceFilterValidator.Validate(new InvoiceSearchFilter { StatusCode = "7" }), Has.Count.EqualTo(1));
            Assert.That(InvoiceFilterValidator.Validate(new InvoiceSearchFilter { CheckResultCode = "9" }), Has.Count.EqualTo(1));
            Assert.That(InvoiceFilterValidator.Validate(new InvoiceSearchFilter { FormSymbol = "10" }), Has.Count.EqualTo(1));
            Assert.That(InvoiceFilterValidator.Validate(new InvoiceSearchFilter { StatusCode = "6", CheckResultCode = "8", FormSymbol = "9" }), Is.Empty);
        }

        [Test]
        public void Options_MatchTheDropdownsOfThePortal()
        {
            Assert.That(PortalOptions.InvoiceStatuses.Select(o => o.Code), Is.EqualTo(new[] { "", "1", "2", "3", "4", "5", "6" }));
            Assert.That(PortalOptions.CheckResults.Select(o => o.Code), Is.EqualTo(new[] { "", "0", "1", "2", "3", "4", "5", "6", "7", "8" }));
            Assert.That(PortalOptions.FormSymbols.Select(o => o.Code), Is.EqualTo(new[] { "", "1", "2", "3", "4", "5", "6", "7", "8", "9" }));
            Assert.That(PortalOptions.StatusLabel("1"), Is.EqualTo("Hóa đơn mới"));
            Assert.That(PortalOptions.StatusLabel("6"), Is.EqualTo("Hóa đơn đã bị hủy"));
            Assert.That(PortalOptions.CheckResultLabel("5"), Is.EqualTo("Đã cấp mã hóa đơn"));
            Assert.That(PortalOptions.CheckResultLabel("0"), Is.EqualTo("Cục Thuế đã nhận"));
            Assert.That(PortalOptions.StatusLabel(null), Is.Empty);
            Assert.That(PortalOptions.StatusLabel("99"), Is.EqualTo("99"), "Mã lạ giữ nguyên thay vì mất thông tin");
        }

        [Test]
        public void AllEntryIsFirst_AndHasNoCode()
        {
            Assert.That(PortalOptions.InvoiceStatuses[0].Label, Is.EqualTo("Tất cả"));
            Assert.That(PortalOptions.InvoiceStatuses[0].Code, Is.Empty);
        }

        [Test]
        public void Plan_CopiesEveryFilterField_SoLaterUiEditsCannotChangeIt()
        {
            var filter = new InvoiceSearchFilter
            {
                CounterpartyTaxCode = "0109213133", BuyerIdNumber = "036402421225", StatusCode = "1", CheckResultCode = "5",
                InvoiceNumber = "40", FormSymbol = "1", InvoiceSymbol = "C26TBN", ProxyInvoice = true
            };

            var plan = DownloadPlan.Create(InvoiceDirection.Sold, PeriodSpec.ForMonth(2026, 10), filter, new PeriodSplitter());
            filter.BuyerIdNumber = "changed";

            Assert.That(plan.Filter.BuyerIdNumber, Is.EqualTo("036402421225"));
            Assert.That(plan.Filter.CounterpartyTaxCode, Is.EqualTo("0109213133"));
            Assert.That(plan.Filter.StatusCode, Is.EqualTo("1"));
            Assert.That(plan.Filter.CheckResultCode, Is.EqualTo("5"));
            Assert.That(plan.Filter.InvoiceNumber, Is.EqualTo("40"));
            Assert.That(plan.Filter.FormSymbol, Is.EqualTo("1"));
            Assert.That(plan.Filter.InvoiceSymbol, Is.EqualTo("C26TBN"));
            Assert.That(plan.Filter.ProxyInvoice, Is.True);
        }
    }
}
