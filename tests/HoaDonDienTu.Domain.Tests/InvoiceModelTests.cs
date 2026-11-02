using System;
using System.IO;
using System.Linq;
using HoaDonDienTu.Domain.Invoices;
using NUnit.Framework;

namespace HoaDonDienTu.Domain.Tests
{
    [TestFixture]
    public class InvoiceModelTests
    {
        private static NormalizedInvoice Sold(string number = "40")
        {
            return new NormalizedInvoice
            {
                Direction = InvoiceDirection.Sold,
                SellerTaxCode = "0109213133",
                FormSymbol = "1",
                InvoiceSymbol = "C26TBN",
                InvoiceNumber = number,
                IssueDate = new DateTime(2026, 10, 31),
                BuyerTaxCode = "0101654633",
                BuyerName = "Người mua"
            };
        }

        // ---- Mã trạng thái ----

        [TestCase("1", InvoiceStatus.New)]
        [TestCase("2", InvoiceStatus.Replacement)]
        [TestCase("3", InvoiceStatus.Adjustment)]
        [TestCase("4", InvoiceStatus.Replaced)]
        [TestCase("5", InvoiceStatus.Adjusted)]
        [TestCase("6", InvoiceStatus.Cancelled)]
        [TestCase("0", InvoiceStatus.Unknown)]
        [TestCase("7", InvoiceStatus.Unknown)]
        [TestCase("x", InvoiceStatus.Unknown)]
        [TestCase(null, InvoiceStatus.Unknown)]
        public void Status_MapsPortalCodes(string raw, InvoiceStatus expected)
        {
            Assert.That(InvoiceCodes.StatusFrom(raw), Is.EqualTo(expected));
        }

        [TestCase("0", InvoiceCheckResult.TaxAuthorityReceived)]
        [TestCase("5", InvoiceCheckResult.CodeIssued)]
        [TestCase("8", InvoiceCheckResult.ReceivedFromCashRegister)]
        [TestCase("9", InvoiceCheckResult.Unknown)]
        [TestCase("-1", InvoiceCheckResult.Unknown)]
        [TestCase("", InvoiceCheckResult.Unknown)]
        public void CheckResult_MapsPortalCodes(string raw, InvoiceCheckResult expected)
        {
            Assert.That(InvoiceCodes.CheckResultFrom(raw), Is.EqualTo(expected));
        }

        // ---- Khóa chống trùng ----

        [Test]
        public void BusinessKey_UsesTheFourFieldCompositeKeyPlusDirection()
        {
            Assert.That(BusinessKey.Build(Sold()), Is.EqualTo("S|0109213133|1|C26TBN|40"));
        }

        [Test]
        public void BusinessKey_LeadingZerosOfTheNumberDoNotCreateADifferentInvoice()
        {
            Assert.That(BusinessKey.Build(Sold("0000040")), Is.EqualTo(BusinessKey.Build(Sold("40"))));
            Assert.That(BusinessKey.Build(Sold("0")), Does.EndWith("|0"));
            Assert.That(BusinessKey.Build(Sold("000")), Does.EndWith("|0"));
        }

        [Test]
        public void BusinessKey_SymbolIsCaseInsensitive_AndWhitespaceIsIgnored()
        {
            var a = Sold();
            var b = Sold();
            b.InvoiceSymbol = " c26tbn ";
            b.SellerTaxCode = " 0109213133 ";

            Assert.That(BusinessKey.Build(b), Is.EqualTo(BusinessKey.Build(a)));
        }

        [Test]
        public void BusinessKey_DifferentDirectionSellerOrFormAreDifferentInvoices()
        {
            var baseKey = BusinessKey.Build(Sold());
            var purchase = Sold();
            purchase.Direction = InvoiceDirection.Purchase;
            var otherSeller = Sold();
            otherSeller.SellerTaxCode = "0100100001";
            var otherForm = Sold();
            otherForm.FormSymbol = "2";

            Assert.That(new[] { BusinessKey.Build(purchase), BusinessKey.Build(otherSeller), BusinessKey.Build(otherForm) }, Has.None.EqualTo(baseKey));
        }

        [Test]
        public void BusinessKey_NonNumericNumbersAreKeptAsIs()
        {
            Assert.That(BusinessKey.Build(Sold("A-12")), Does.EndWith("|A-12"));
        }

        // ---- Định danh đối tác ----

        [Test]
        public void IndividualBuyer_UsesIdNumber_WhenThereIsNoTaxCode()
        {
            var invoice = Sold();
            invoice.BuyerTaxCode = null;
            invoice.BuyerIdNumber = "001092008840";

            Assert.That(invoice.BuyerIdentifier, Is.EqualTo("001092008840"));
            Assert.That(invoice.CounterpartyIdentifier, Is.EqualTo("001092008840"));
        }

        [Test]
        public void Counterparty_IsTheSeller_ForPurchases_AndTheBuyer_ForSales()
        {
            var sold = Sold();
            var purchase = Sold();
            purchase.Direction = InvoiceDirection.Purchase;

            Assert.That(sold.CounterpartyIdentifier, Is.EqualTo("0101654633"));
            Assert.That(purchase.CounterpartyIdentifier, Is.EqualTo("0109213133"));
        }

        // ---- Tên file ----

        [Test]
        public void FileName_FollowsTheDocumentedPattern()
        {
            Assert.That(InvoiceFileNamer.FileName(Sold(), 1), Is.EqualTo("20261031_0101654633_1_C26TBN_40_v1.xml"));
            Assert.That(InvoiceFileNamer.FileName(Sold(), 3), Does.EndWith("_v3.xml"));
        }

        [Test]
        public void RelativeFolder_SplitsByYearMonthDirection()
        {
            var sold = Sold();
            var purchase = Sold();
            purchase.Direction = InvoiceDirection.Purchase;

            Assert.That(InvoiceFileNamer.RelativeFolder(sold), Is.EqualTo(Path.Combine("2026", "10", "BanRa", "XML")));
            Assert.That(InvoiceFileNamer.RelativeFolder(purchase), Is.EqualTo(Path.Combine("2026", "10", "MuaVao", "XML")));
        }

        [Test]
        public void FileName_ReplacesCharactersThatAreInvalidOnWindows()
        {
            var invoice = Sold();
            invoice.InvoiceSymbol = "C26/T:B*N?";
            invoice.InvoiceNumber = "1 2\\3";

            var name = InvoiceFileNamer.FileName(invoice, 1);

            Assert.That(name.IndexOfAny(Path.GetInvalidFileNameChars()), Is.EqualTo(-1));
            Assert.That(name, Does.Contain("C26_T_B_N_").And.Contain("_1_2_3_"));
        }

        [Test]
        public void FileName_MissingPartsGetPlaceholders_AndLongPartsAreTruncated()
        {
            var invoice = Sold();
            invoice.BuyerTaxCode = null;
            invoice.InvoiceSymbol = new string('X', 200);

            var name = InvoiceFileNamer.FileName(invoice, 1);

            Assert.That(name, Does.Contain("_NA_"));
            Assert.That(name.Length, Is.LessThan(120));
        }

        [Test]
        public void CollisionSuffix_GoesBeforeTheExtension()
        {
            Assert.That(InvoiceFileNamer.WithCollisionSuffix("a_v1.xml", 2), Is.EqualTo("a_v1_2.xml"));
        }

        [Test]
        public void PathLimit_IsChecked()
        {
            var shortRoot = @"C:\Kho\0109213133";
            var longRoot = @"C:\" + new string('a', 240);
            var relative = InvoiceFileNamer.RelativePath(Sold(), 1);

            Assert.That(InvoiceFileNamer.FitsPathLimit(shortRoot, relative), Is.True);
            Assert.That(InvoiceFileNamer.FitsPathLimit(longRoot, relative), Is.False);
        }
    }
}
