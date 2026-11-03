using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HoaDonDienTu.Application.Companies;
using HoaDonDienTu.Application.Invoices;
using HoaDonDienTu.Application.Navigation;
using HoaDonDienTu.Domain.Companies;
using HoaDonDienTu.Domain.Invoices;
using HoaDonDienTu.WinForms.Common;
using HoaDonDienTu.WinForms.Detail;
using NUnit.Framework;

namespace HoaDonDienTu.WinForms.Tests
{
    [TestFixture]
    public class InvoiceDetailPresenterTests : CompanyUiTestBase
    {
        private static readonly Guid InvoiceId = Guid.NewGuid();

        private FakeDetailService _service;
        private FakeDetailView _detailView;
        private FakeViewerView _viewerView;
        private FakePicker _picker;
        private InvoiceDetailPresenter _detail;
        private InvoiceViewerPresenter _viewer;
        private string _tempDir;

        [SetUp]
        public void SetUp()
        {
            Context.Current = new CompanyContext(CompanyId.NewId(), TaxCode.Parse("0101234567"), "Công ty", @"C:\Kho\0101234567", null);
            _service = new FakeDetailService { Detail = Sample() };
            _service.Xml[1] = XmlView(1, "<HDon><a>1</a></HDon>");
            _service.Xml[2] = XmlView(2, "<HDon><a>2</a></HDon>");
            _detailView = new FakeDetailView();
            _viewerView = new FakeViewerView();
            _picker = new FakePicker();
            _tempDir = Path.Combine(Path.GetTempPath(), "hddt-viewer-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (_detail != null) _detail.Dispose();
            if (_viewer != null) _viewer.Dispose();
            try
            {
                Directory.Delete(_tempDir, true);
            }
            catch (IOException)
            {
            }
        }

        private static InvoiceDetail Sample()
        {
            return new InvoiceDetail
            {
                InvoiceId = InvoiceId,
                Invoice = new NormalizedInvoice
                {
                    Direction = InvoiceDirection.Purchase, InvoiceNumber = "0000128", InvoiceSymbol = "C26TAA", FormSymbol = "1", IssueDate = new DateTime(2026, 10, 31),
                    PortalInvoiceId = "d2240f37", SellerTaxCode = "0109988776", SellerName = "Công ty ABC", BuyerTaxCode = "0101234567", BuyerName = "Công ty TNHH Mẫu",
                    BuyerAddress = "Hà Nội, Việt Nam", StatusRaw = "1", CheckResultRaw = "5", TotalBeforeTax = 30000000m, TotalTax = 3000000m, TotalDiscount = 0m,
                    TotalPayment = 33000000m, Currency = "VND"
                },
                FirstSeenAtUtc = new DateTime(2026, 10, 31, 2, 41, 0, DateTimeKind.Utc),
                LastSeenAtUtc = new DateTime(2026, 11, 1, 3, 2, 0, DateTimeKind.Utc),
                Versions = new List<InvoiceVersionDetail>
                {
                    new InvoiceVersionDetail
                    {
                        VersionNo = 2, CreatedAtUtc = new DateTime(2026, 11, 1, 3, 2, 0, DateTimeKind.Utc), Sha256 = "8a2f00000000000000000000000000000000000000000000000000000000d91c",
                        SizeBytes = 100, IsCurrent = true, FileState = VersionFileState.Ok, SessionId = Guid.NewGuid(), SessionCode = "DL-2026-1101-001", SessionPeriod = "Tháng 10/2026"
                    },
                    new InvoiceVersionDetail
                    {
                        VersionNo = 1, CreatedAtUtc = new DateTime(2026, 10, 31, 2, 41, 0, DateTimeKind.Utc), Sha256 = "11111111", SizeBytes = 90, IsCurrent = false,
                        FileState = VersionFileState.Ok, SessionId = Guid.NewGuid()
                    }
                }
            };
        }

        private static InvoiceXmlView XmlView(int versionNo, string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            var tree = new XmlTreeNode { Name = "HDon" };
            tree.Children.Add(new XmlTreeNode { Name = "a", Value = versionNo.ToString() });
            return new InvoiceXmlView { InvoiceId = InvoiceId, VersionNo = versionNo, Sha256 = "8a2f00000000000000000000000000000000000000000000000000000000d91c", RawText = text, Bytes = bytes, SizeBytes = bytes.Length, Tree = tree, HashMatches = true };
        }

        private InvoiceDetailPresenter OpenDetail()
        {
            _detail = new InvoiceDetailPresenter(_detailView, _service, Context, Nav, Runner, Errors, InvoiceId);
            _detail.Initialize();
            return _detail;
        }

        private FakeRenderer _renderer;
        private FakeSavePicker _pdfPicker;
        private FakePrinter _printer;

        private InvoiceViewerPresenter OpenViewer(int version = 2, bool withPrintTools = true)
        {
            _renderer = new FakeRenderer();
            _pdfPicker = new FakeSavePicker();
            _printer = new FakePrinter();
            var tools = withPrintTools ? new InvoicePrintTools(_renderer, _pdfPicker, _printer) : null;
            _viewer = new InvoiceViewerPresenter(_viewerView, _service, Context, Nav, _picker, Runner, Errors, InvoiceId, version, tools);
            _viewer.Initialize();
            return _viewer;
        }

        private static bool WaitFor(Func<bool> condition)
        {
            return UiWait.Until(condition);
        }

        // ---- SCR-06 ----

        [Test]
        public void Detail_ShowsAllFieldsInVietnamese()
        {
            OpenDetail();

            Assert.That(WaitFor(() => _detailView.Result != null), Is.True);
            var r = _detailView.Result;
            Assert.That(r.Title, Is.EqualTo("Chi tiết hóa đơn 0000128"));
            Assert.That(r.Subtitle, Is.EqualTo("Mua vào · Ký hiệu C26TAA · Ngày lập 31/10/2026"));
            Assert.That(r.Badge, Is.Not.Empty);
            Assert.That(r.Info.Select(f => f.Label), Is.EqualTo(new[] { "Ký hiệu mẫu số", "Ký hiệu hóa đơn", "Số hóa đơn", "Ngày lập", "Trạng thái", "Kết quả kiểm tra", "Mã hóa đơn trên cổng" }));
            Assert.That(r.Info.Single(f => f.Label == "Ngày lập").Value, Is.EqualTo("31/10/2026"));
            Assert.That(r.Seller.Select(f => f.Value), Is.EqualTo(new[] { "0109988776", "Công ty ABC" }));
            Assert.That(r.Buyer.Select(f => f.Value), Is.EqualTo(new[] { "0101234567", "Công ty TNHH Mẫu", "Hà Nội, Việt Nam" }));
            Assert.That(r.Values.Select(f => f.Value), Is.EqualTo(new[] { "30.000.000 VND", "3.000.000 VND", "0 VND", "33.000.000 VND" }));
            Assert.That(r.Values.Last().Emphasized, Is.True);
        }

        [Test]
        public void Detail_ShowsTheFileCardsVersionsAndSource()
        {
            OpenDetail();

            Assert.That(WaitFor(() => _detailView.Result != null), Is.True);
            var r = _detailView.Result;
            Assert.That(r.CanViewXml, Is.True);
            Assert.That(r.XmlSub, Is.EqualTo("SHA-256: 8a2f…d91c · 2 phiên bản"));
            Assert.That(r.XmlProblem, Is.False);
            Assert.That(r.PdfText, Is.EqualTo("Dựng từ XML"));
            Assert.That(r.Versions.Select(v => v.Name), Is.EqualTo(new[] { "v2", "v1" }));
            Assert.That(r.Versions.Select(v => v.Status), Is.EqualTo(new[] { "Hiện hành", "Bản cũ" }));
            Assert.That(r.Versions[0].Session, Is.EqualTo("Phiên DL-2026-1101-001"));
            Assert.That(r.Versions[1].Session, Is.EqualTo("Không rõ phiên"));
            Assert.That(r.Source.Select(f => f.Label), Is.EqualTo(new[] { "Phiên", "Kỳ tải", "Lần thấy đầu", "Lần thấy cuối" }));
            Assert.That(r.Source[0].Value, Is.EqualTo("DL-2026-1101-001"));
        }

        [Test]
        public void Detail_MissingCurrentFile_DisablesViewingAndWarns()
        {
            _service.Detail.Versions[0].FileState = VersionFileState.Missing;

            OpenDetail();

            Assert.That(WaitFor(() => _detailView.Result != null), Is.True);
            var r = _detailView.Result;
            Assert.That(r.CanViewXml, Is.False);
            Assert.That(r.XmlText, Is.EqualTo("Thiếu file"));
            Assert.That(r.XmlProblem, Is.True);
            Assert.That(r.Versions[0].Status, Is.EqualTo("Thiếu file"));
            Assert.That(r.Versions[0].Problem, Is.True);
        }

        [Test]
        public void Detail_ForeignCurrencyShowsTheRate()
        {
            _service.Detail.Invoice.Currency = "USD";
            _service.Detail.Invoice.ExchangeRate = 25400.5m;

            OpenDetail();

            Assert.That(WaitFor(() => _detailView.Result != null), Is.True);
            Assert.That(_detailView.Result.Values.Last().Label, Is.EqualTo("Tỷ giá"));
            Assert.That(_detailView.Result.Values[0].Value, Does.EndWith(" USD"));
        }

        [Test]
        public void Detail_BuyerWithOnlyAnIdNumber_LabelsItAsCccd()
        {
            _service.Detail.Invoice.BuyerTaxCode = null;
            _service.Detail.Invoice.BuyerIdNumber = "001234567890";

            OpenDetail();

            Assert.That(WaitFor(() => _detailView.Result != null), Is.True);
            Assert.That(_detailView.Result.Buyer[0].Label, Is.EqualTo("CCCD người mua"));
            Assert.That(_detailView.Result.Buyer[0].Value, Is.EqualTo("001234567890"));
        }

        [Test]
        public void Detail_InvoiceNoLongerInTheStore_SaysSo()
        {
            _service.Detail = null;

            OpenDetail();

            Assert.That(WaitFor(() => _detailView.Message != null && _detailView.Message.Contains("không còn trong kho")), Is.True);
            Assert.That(_detailView.Result, Is.Null);
        }

        [Test]
        public void Detail_NoCompany_SaysSo()
        {
            Context.Current = null;

            OpenDetail();

            Assert.That(WaitFor(() => _detailView.Message != null), Is.True);
            Assert.That(_detailView.Message, Does.Contain("Chưa chọn doanh nghiệp"));
            Assert.That(_service.GetCalls, Is.EqualTo(0));
        }

        [Test]
        public void Detail_BackGoesToTheArchive_WithoutAFilterOfItsOwn()
        {
            OpenDetail();
            Assert.That(WaitFor(() => _detailView.Result != null), Is.True);

            _detailView.RaiseBack();

            Assert.That(WaitFor(() => Nav.Targets.Count == 1), Is.True);
            Assert.That(Nav.Targets[0], Is.EqualTo(ScreenId.InvoiceArchive));
            Assert.That(Nav.Parameters[0], Is.Null, "Bộ lọc do SCR-05 tự khôi phục");
        }

        [Test]
        public void Detail_ViewCurrentOpensTheViewerOnTheCurrentVersion()
        {
            OpenDetail();
            Assert.That(WaitFor(() => _detailView.Result != null), Is.True);

            _detailView.RaiseViewCurrent();
            Assert.That(WaitFor(() => Nav.Targets.Count == 1), Is.True);

            Assert.That(Nav.Targets, Is.EqualTo(new[] { ScreenId.InvoiceViewer }));
            var request = (InvoiceViewerRequest)Nav.Parameters[0];
            Assert.That(request.InvoiceId, Is.EqualTo(InvoiceId));
            Assert.That(request.VersionNo, Is.EqualTo(2));
        }

        [Test]
        public void Detail_ServiceFailure_IsShownToTheUser()
        {
            _service.Fail = true;

            OpenDetail();

            Assert.That(WaitFor(() => Errors.Shown.Count > 0), Is.True);
        }

        // ---- SCR-07 ----

        [Test]
        public void Viewer_ShowsTreeRawTextVersionsAndSha()
        {
            OpenViewer();

            Assert.That(WaitFor(() => _viewerView.Result != null), Is.True);
            var r = _viewerView.Result;
            Assert.That(r.Subtitle, Is.EqualTo("Hóa đơn 0000128 · C26TAA · Phiên bản XML v2"));
            Assert.That(r.ShaText, Is.EqualTo("SHA-256: 8a2f…d91c"));
            Assert.That(r.Tree.Text, Is.EqualTo("HDon"));
            Assert.That(r.Tree.Children[0].Text, Is.EqualTo("a: 2"));
            Assert.That(r.RawText, Is.EqualTo("<HDon><a>2</a></HDon>"));
            Assert.That(r.Versions.Select(v => v.Label), Is.EqualTo(new[] { "v2 (hiện hành)", "v1" }));
            Assert.That(r.SelectedVersion, Is.EqualTo(2));
            Assert.That(r.CanSaveCopy, Is.True);
            Assert.That(_viewerView.MessageText, Is.Empty);
        }

        [Test]
        public void Viewer_SwitchingTheVersionLoadsThatXml()
        {
            OpenViewer();
            Assert.That(WaitFor(() => _viewerView.Result != null), Is.True);

            _viewerView.RaiseVersion(1);

            Assert.That(WaitFor(() => _viewerView.Result.SelectedVersion == 1), Is.True);
            Assert.That(_viewerView.Result.RawText, Is.EqualTo("<HDon><a>1</a></HDon>"));
            Assert.That(_viewerView.Result.Tree.Children[0].Text, Is.EqualTo("a: 1"));
        }

        [Test]
        public void Viewer_ProblemsAreShownAsErrors_AndMissingFilesCannotBeSaved()
        {
            _service.Xml[2] = new InvoiceXmlView { InvoiceId = InvoiceId, VersionNo = 2, Sha256 = "x", Problem = "File XML của phiên bản 2 không còn trong thư mục kho." };

            OpenViewer();

            Assert.That(WaitFor(() => _viewerView.Result != null), Is.True);
            Assert.That(_viewerView.MessageText, Does.Contain("không còn trong thư mục kho"));
            Assert.That(_viewerView.MessageIsError, Is.True);
            Assert.That(_viewerView.Result.CanSaveCopy, Is.False);
            Assert.That(_viewerView.Result.Tree, Is.Null);
        }

        [Test]
        public void Viewer_TooLargeXml_CanStillBeSavedAsACopy_WithTheFullBytes()
        {
            var target = Path.Combine(_tempDir, "lon.xml");
            _picker.Result = target;
            _service.LargeBytes = Encoding.UTF8.GetBytes("<HDon>rất lớn</HDon>");
            _service.Xml[2] = new InvoiceXmlView { InvoiceId = InvoiceId, VersionNo = 2, Sha256 = "x", TooLarge = true, Problem = "File XML quá lớn để hiển thị. Hãy lưu bản sao." };
            OpenViewer();
            Assert.That(WaitFor(() => _viewerView.Result != null), Is.True);
            Assert.That(_viewerView.Result.CanSaveCopy, Is.True, "thông báo bảo lưu bản sao thì nút phải dùng được");

            _viewerView.RaiseSaveCopy();

            Assert.That(WaitFor(() => File.Exists(target)), Is.True);
            Assert.That(File.ReadAllBytes(target), Is.EqualTo(_service.LargeBytes));
        }

        // ---- Tab PDF (ADR 0009) ----

        [Test]
        public void Pdf_IsBuiltFromTheXml_AndShownAsPreviewPages_WithPrintAndSaveEnabled()
        {
            OpenViewer();

            Assert.That(WaitFor(() => _viewerView.Pdf != null && _viewerView.Pdf.CanPrintOrSave), Is.True);
            Assert.That(_viewerView.Pdf.Pages, Has.Count.EqualTo(2));
            Assert.That(_viewerView.Pdf.Notice, Is.Null);
            Assert.That(_service.PrintCalls, Is.EqualTo(new[] { 2 }));
            Assert.That(_viewerView.PdfHistory.First().Notice, Does.Contain("Đang dựng"), "báo đang dựng trước khi có trang");
        }

        [Test]
        public void Pdf_WhenTheFileCannotBeTrusted_ShowsTheReason_AndDisablesPrintAndSave()
        {
            _service.Print[2] = new InvoicePrintResult { Problem = "Nội dung file XML khác với lúc tải về (sai SHA-256) nên không dựng PDF/in." };

            OpenViewer();

            Assert.That(WaitFor(() => _viewerView.Pdf != null && _viewerView.Pdf.Notice != null && _viewerView.Pdf.Notice.Contains("sai SHA-256")), Is.True);
            Assert.That(_viewerView.Pdf.IsError, Is.True);
            Assert.That(_viewerView.Pdf.CanPrintOrSave, Is.False);
            Assert.That(_renderer.Builds, Is.EqualTo(0), "không dựng PDF sai chứng từ");
        }

        [Test]
        public void Pdf_RendererFailure_IsReportedNotThrown()
        {
            OpenViewer();
            Assert.That(WaitFor(() => _viewerView.Pdf != null && _viewerView.Pdf.CanPrintOrSave), Is.True);
            _viewer.Dispose();
            _service.Xml[1] = XmlView(1, "<HDon><a>1</a></HDon>");
            _viewerView = new FakeViewerView();
            OpenViewer(1);
            _renderer.Fail = true;
            _viewerView.RaiseVersion(1);

            Assert.That(WaitFor(() => _viewerView.Pdf != null && _viewerView.Pdf.IsError && _viewerView.Pdf.Notice.Contains("Không dựng được")), Is.True);
            Assert.That(_viewerView.Pdf.CanPrintOrSave, Is.False);
        }

        [Test]
        public void Pdf_WithoutPrintTools_SaysItIsUnavailable()
        {
            OpenViewer(2, false);

            Assert.That(WaitFor(() => _viewerView.Pdf != null), Is.True);
            Assert.That(_viewerView.Pdf.Notice, Does.Contain("chưa khả dụng"));
            Assert.That(_viewerView.Pdf.CanPrintOrSave, Is.False);
            Assert.That(_service.PrintCalls, Is.Empty);
        }

        [Test]
        public void Pdf_Save_WritesTheDocumentPdf_ToTheChosenPlace_WithAPdfExtension()
        {
            var target = Path.Combine(_tempDir, "hoa-don.pdf");
            OpenViewer();
            Assert.That(WaitFor(() => _viewerView.Pdf != null && _viewerView.Pdf.CanPrintOrSave), Is.True);
            _pdfPicker.Result = target;

            _viewerView.RaiseSavePdf();

            Assert.That(WaitFor(() => File.Exists(target)), Is.True);
            Assert.That(Encoding.ASCII.GetString(File.ReadAllBytes(target)), Is.EqualTo("%PDF-fake"));
            Assert.That(_pdfPicker.LastExtension, Is.EqualTo("pdf"));
            Assert.That(_pdfPicker.LastSuggested, Does.EndWith(".pdf"));
            Assert.That(WaitFor(() => _viewerView.MessageText != null && _viewerView.MessageText.Contains("Đã lưu PDF")), Is.True, _viewerView.MessageText);
        }

        [Test]
        public void Pdf_Save_CancelledDialog_WritesNothing()
        {
            OpenViewer();
            Assert.That(WaitFor(() => _viewerView.Pdf != null && _viewerView.Pdf.CanPrintOrSave), Is.True);
            _pdfPicker.Result = null;

            _viewerView.RaiseSavePdf();

            Assert.That(WaitFor(() => _pdfPicker.LastExtension == "pdf"), Is.True);
            Assert.That(Directory.GetFiles(_tempDir), Is.Empty);
        }

        [Test]
        public void Pdf_Print_SendsTheDocumentToThePrinter_NamedAfterTheInvoice()
        {
            OpenViewer();
            Assert.That(WaitFor(() => _viewerView.Pdf != null && _viewerView.Pdf.CanPrintOrSave), Is.True);

            _viewerView.RaisePrint();

            Assert.That(WaitFor(() => _printer.Printed == 1), Is.True);
            Assert.That(_printer.LastJob, Is.EqualTo("Hóa đơn 0000128"));
        }

        [Test]
        public void Pdf_PrintAndSave_DoNothingBeforeADocumentExists()
        {
            _service.Print[2] = new InvoicePrintResult { Problem = "không dựng được" };
            OpenViewer();
            Assert.That(WaitFor(() => _viewerView.Pdf != null && _viewerView.Pdf.IsError), Is.True);

            _viewerView.RaisePrint();
            _viewerView.RaiseSavePdf();

            Assert.That(_printer.Printed, Is.EqualTo(0));
            Assert.That(_pdfPicker.LastExtension, Is.Null);
        }

        [Test]
        public void Pdf_SwitchingTheVersion_BuildsThatVersion()
        {
            OpenViewer();
            Assert.That(WaitFor(() => _viewerView.Pdf != null && _viewerView.Pdf.CanPrintOrSave), Is.True);

            _viewerView.RaiseVersion(1);

            Assert.That(WaitFor(() => _service.PrintCalls.SequenceEqual(new[] { 2, 1 })), Is.True);
        }

        [Test]
        public void Viewer_BrokenXmlStillShowsTheRawText()
        {
            _service.Xml[2] = new InvoiceXmlView
            {
                InvoiceId = InvoiceId, VersionNo = 2, Sha256 = "x", RawText = "<HDon><broken>", Bytes = Encoding.UTF8.GetBytes("<HDon><broken>"),
                Problem = "XML không đọc được (file hỏng hoặc không đúng định dạng); chỉ xem được nội dung thô."
            };

            OpenViewer();

            Assert.That(WaitFor(() => _viewerView.Result != null), Is.True);
            Assert.That(_viewerView.Result.Tree, Is.Null);
            Assert.That(_viewerView.Result.RawText, Is.EqualTo("<HDon><broken>"));
            Assert.That(_viewerView.Result.CanSaveCopy, Is.True);
        }

        [Test]
        public void Viewer_SaveCopyWritesTheOriginalBytes_ToTheChosenPlace()
        {
            var target = Path.Combine(_tempDir, "ban-sao.xml");
            _picker.Result = target;
            OpenViewer();
            Assert.That(WaitFor(() => _viewerView.Result != null), Is.True);

            _viewerView.RaiseSaveCopy();

            Assert.That(WaitFor(() => File.Exists(target)), Is.True);
            Assert.That(File.ReadAllBytes(target), Is.EqualTo(Encoding.UTF8.GetBytes("<HDon><a>2</a></HDon>")));
            Assert.That(_picker.SuggestedName, Is.EqualTo("C26TAA_0000128_v2.xml"));
            Assert.That(WaitFor(() => _viewerView.MessageText.Contains("Đã lưu bản sao")), Is.True);
            Assert.That(_viewerView.MessageIsError, Is.False);
        }

        [Test]
        public void Viewer_CancelledSaveWritesNothing()
        {
            _picker.Result = null;
            OpenViewer();
            Assert.That(WaitFor(() => _viewerView.Result != null), Is.True);

            _viewerView.RaiseSaveCopy();

            Assert.That(WaitFor(() => _picker.Calls == 1), Is.True);
            Assert.That(Directory.GetFiles(_tempDir), Is.Empty);
        }

        [Test]
        public void Viewer_SaveFailureIsShownToTheUser()
        {
            _picker.Result = Path.Combine(_tempDir, "khong-co-thu-muc", "x.xml");
            OpenViewer();
            Assert.That(WaitFor(() => _viewerView.Result != null), Is.True);

            _viewerView.RaiseSaveCopy();

            Assert.That(WaitFor(() => Errors.Shown.Count > 0), Is.True);
        }

        [Test]
        public void Viewer_CloseGoesBackToTheDetailOfTheSameInvoice()
        {
            OpenViewer();
            Assert.That(WaitFor(() => _viewerView.Result != null), Is.True);

            _viewerView.RaiseClose();

            Assert.That(WaitFor(() => Nav.Targets.Count == 1), Is.True);
            Assert.That(Nav.Targets[0], Is.EqualTo(ScreenId.InvoiceDetail));
            Assert.That(((InvoiceDetailRequest)Nav.Parameters[0]).InvoiceId, Is.EqualTo(InvoiceId));
        }

        [Test]
        public void Viewer_InvoiceGone_SaysSo()
        {
            _service.Detail = null;

            OpenViewer();

            Assert.That(WaitFor(() => _viewerView.MessageText != null && _viewerView.MessageText.Contains("không còn trong kho")), Is.True);
            Assert.That(_viewerView.Result, Is.Null);
        }

        [Test]
        public void SuggestedName_ReplacesCharactersThatAreInvalidInFileNames()
        {
            var invoice = new NormalizedInvoice { InvoiceSymbol = "C26/TAA", InvoiceNumber = "00:01" };

            Assert.That(InvoiceViewerPresenter.SuggestedName(invoice, 3), Is.EqualTo("C26_TAA_00_01_v3.xml"));
        }

        // ---- Fakes ----

        private sealed class FakePicker : ISaveFilePicker
        {
            public string Result;
            public string SuggestedName;
            public int Calls;

            public string Pick(string suggestedFileName)
            {
                Calls++;
                SuggestedName = suggestedFileName;
                return Result;
            }
        }

        private sealed class FakeDetailService : IInvoiceDetailService
        {
            public InvoiceDetail Detail;
            public Dictionary<int, InvoiceXmlView> Xml = new Dictionary<int, InvoiceXmlView>();
            public bool Fail;
            public int GetCalls;

            public Task<InvoiceDetail> GetAsync(Guid invoiceId, CancellationToken cancellationToken)
            {
                GetCalls++;
                if (Fail) throw new InvalidOperationException("boom");
                return Task.FromResult(Detail);
            }

            public byte[] LargeBytes;

            public Task<byte[]> ReadBytesAsync(Guid invoiceId, int versionNo, CancellationToken cancellationToken)
            {
                return Task.FromResult(LargeBytes);
            }

            public Dictionary<int, InvoicePrintResult> Print = new Dictionary<int, InvoicePrintResult>();
            public List<int> PrintCalls = new List<int>();

            public Task<InvoicePrintResult> PreparePrintAsync(Guid invoiceId, int versionNo, CancellationToken cancellationToken)
            {
                PrintCalls.Add(versionNo);
                InvoicePrintResult result;
                if (!Print.TryGetValue(versionNo, out result)) result = new InvoicePrintResult { Model = new InvoicePrintModel { InvoiceNumber = "0000128" } };
                return Task.FromResult(result);
            }

            public Task<InvoiceXmlView> OpenXmlAsync(Guid invoiceId, int versionNo, CancellationToken cancellationToken)
            {
                InvoiceXmlView view;
                if (!Xml.TryGetValue(versionNo, out view)) throw new InvoiceStorageException("Không có phiên bản " + versionNo);
                return Task.FromResult(view);
            }
        }

        private sealed class FakeDetailView : IInvoiceDetailView
        {
            private readonly object _gate = new object();
            private InvoiceDetailViewModel _result;
            private string _message;

            public event EventHandler BackRequested;
            public event EventHandler ViewCurrentRequested;

            public InvoiceDetailViewModel Result { get { lock (_gate) { return _result; } } }
            public string Message { get { lock (_gate) { return _message; } } }

            public void ShowResult(InvoiceDetailViewModel model)
            {
                lock (_gate) { _result = model; }
            }

            public void ShowMessage(string text, bool isError)
            {
                lock (_gate) { _message = text; }
            }

            public void RaiseBack() { BackRequested(this, EventArgs.Empty); }
            public void RaiseViewCurrent() { ViewCurrentRequested(this, EventArgs.Empty); }
        }

        private sealed class FakeDocument : IInvoiceDocument
        {
            public int PageCount { get { return 2; } }
            public byte[] ToPdf() { return Encoding.ASCII.GetBytes("%PDF-fake"); }
            public byte[] RenderPagePng(int pageIndex, int dpi) { return new byte[] { 0x89, 0x50, 0x4E, 0x47, (byte)pageIndex }; }
        }

        private sealed class FakeRenderer : IInvoiceDocumentRenderer
        {
            public int Builds;
            public bool Fail;

            public IInvoiceDocument Build(InvoicePrintModel model)
            {
                Builds++;
                if (Fail) throw new InvalidOperationException("boom");
                return new FakeDocument();
            }
        }

        private sealed class FakeSavePicker : IExportSavePicker
        {
            public string Result;
            public string LastSuggested;
            public string LastExtension;

            public string Pick(string suggestedFileName, string extension, string filterDescription, string title, string initialDirectory)
            {
                LastSuggested = suggestedFileName;
                LastExtension = extension;
                return Result;
            }
        }

        private sealed class FakePrinter : IInvoicePrinter
        {
            public int Printed;
            public string LastJob;
            public bool Accept = true;

            public bool Print(IInvoiceDocument document, string jobName)
            {
                LastJob = jobName;
                if (Accept) Printed++;
                return Accept;
            }
        }

        private sealed class FakeViewerView : IInvoiceViewerView
        {
            private readonly object _gate = new object();
            private InvoiceViewerViewModel _result;
            private string _message;
            private bool _isError;

            public event EventHandler CloseRequested;
            public event EventHandler SaveCopyRequested;
            public event EventHandler SavePdfRequested;
            public event EventHandler PrintRequested;
            public event EventHandler<VersionRequestedEventArgs> VersionSelected;

            private InvoicePdfViewModel _pdf;
            public InvoicePdfViewModel Pdf { get { lock (_gate) { return _pdf; } } }
            public List<InvoicePdfViewModel> PdfHistory = new List<InvoicePdfViewModel>();

            public void ShowPdf(InvoicePdfViewModel model)
            {
                lock (_gate)
                {
                    _pdf = model;
                    PdfHistory.Add(model);
                }
            }

            public void RaiseSavePdf() { SavePdfRequested(this, EventArgs.Empty); }
            public void RaisePrint() { PrintRequested(this, EventArgs.Empty); }

            public InvoiceViewerViewModel Result { get { lock (_gate) { return _result; } } }
            public string MessageText { get { lock (_gate) { return _message; } } }
            public bool MessageIsError { get { lock (_gate) { return _isError; } } }

            public void ShowResult(InvoiceViewerViewModel model)
            {
                lock (_gate) { _result = model; }
            }

            public void ShowMessage(string text, bool isError)
            {
                lock (_gate)
                {
                    _message = text;
                    _isError = isError;
                }
            }

            public void RaiseClose() { CloseRequested(this, EventArgs.Empty); }
            public void RaiseSaveCopy() { SaveCopyRequested(this, EventArgs.Empty); }
            public void RaiseVersion(int no) { VersionSelected(this, new VersionRequestedEventArgs(no)); }
        }
    }
}
