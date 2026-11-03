using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.Xml;
using System.Xml;

namespace HoaDonDienTu.Application.Invoices
{
    /// <summary>Kết quả kiểm tra chữ ký số của người bán trong file XML hóa đơn.</summary>
    public enum InvoiceSignatureStatus
    {
        /// <summary>XML không có chữ ký số của người bán.</summary>
        None,

        /// <summary>Chữ ký toàn vẹn: giá trị băm của nội dung được ký khớp và chữ ký đúng với khóa công khai trong chứng thư đính kèm.</summary>
        Valid,

        /// <summary>Có chữ ký nhưng không khớp: nội dung đã bị sửa sau khi ký hoặc chữ ký sai.</summary>
        Invalid,

        /// <summary>Có chữ ký nhưng không kiểm được (thuật toán lạ, tham chiếu ngoài, cấu trúc hỏng).</summary>
        NotChecked
    }

    /// <summary>
    /// Kiểm chữ ký XML-DSig của người bán (<c>DSCKS/NBan/Signature</c>) trên chính file đã lưu bằng <see cref="SignedXml"/>: băm từng tham chiếu và kiểm chữ ký bằng khóa trong
    /// chứng thư đính kèm. Chỉ nhận tham chiếu nội bộ (<c>#id</c>) và phép biến đổi chuẩn hóa/chữ ký bao; mọi thứ khác (XSLT, địa chỉ ngoài) không được thực thi và cho kết quả
    /// <see cref="InvoiceSignatureStatus.NotChecked"/>. <b>Không</b> kiểm chuỗi chứng thư, thời hạn hay thu hồi: kết quả "hợp lệ" nghĩa là nội dung không bị sửa sau khi ký.
    /// </summary>
    public static class InvoiceSignatureVerifier
    {
        private const string SignatureNamespace = "http://www.w3.org/2000/09/xmldsig#";
        private const string RsaSha256Uri = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256";
        private static readonly object RegisterGate = new object();
        private static bool _registered;

        public static InvoiceSignatureStatus VerifySeller(byte[] xml)
        {
            if (xml == null || xml.Length == 0) return InvoiceSignatureStatus.NotChecked;
            try
            {
                var document = new XmlDocument { PreserveWhitespace = true, XmlResolver = null };
                var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
                using (var reader = XmlReader.Create(new MemoryStream(xml), settings)) document.Load(reader);

                var signature = FindSellerSignature(document);
                if (signature == null) return InvoiceSignatureStatus.None;

                RegisterAlgorithms();
                var signed = new SignedXml(document);
                signed.LoadXml(signature);
                if (!OnlyLocalReferencesAndSafeTransforms(signed)) return InvoiceSignatureStatus.NotChecked;
                return signed.CheckSignature() ? InvoiceSignatureStatus.Valid : InvoiceSignatureStatus.Invalid;
            }
            catch (CryptographicException)
            {
                return InvoiceSignatureStatus.NotChecked;
            }
            catch (XmlException)
            {
                return InvoiceSignatureStatus.NotChecked;
            }
            catch (InvalidOperationException)
            {
                return InvoiceSignatureStatus.NotChecked;
            }
            catch (ArgumentException)
            {
                return InvoiceSignatureStatus.NotChecked;
            }
        }

        private static XmlElement FindSellerSignature(XmlDocument document)
        {
            var root = document.DocumentElement;
            if (root == null) return null;
            foreach (var signatures in root.ChildNodes.OfType<XmlElement>().Where(e => e.LocalName == "DSCKS"))
            {
                foreach (var seller in signatures.ChildNodes.OfType<XmlElement>().Where(e => e.LocalName == "NBan"))
                {
                    var signature = seller.ChildNodes.OfType<XmlElement>().FirstOrDefault(e => e.LocalName == "Signature" && e.NamespaceURI == SignatureNamespace);
                    if (signature != null) return signature;
                }
            }

            return null;
        }

        private static bool OnlyLocalReferencesAndSafeTransforms(SignedXml signed)
        {
            foreach (Reference reference in signed.SignedInfo.References)
            {
                if (string.IsNullOrEmpty(reference.Uri) || !reference.Uri.StartsWith("#", StringComparison.Ordinal)) return false;
                for (var i = 0; i < reference.TransformChain.Count; i++)
                {
                    var transform = reference.TransformChain[i];
                    if (!(transform is XmlDsigEnvelopedSignatureTransform) && !(transform is XmlDsigC14NTransform) && !(transform is XmlDsigExcC14NTransform)) return false;
                }
            }

            return true;
        }

        /// <summary>.NET Framework 4.5.2 chưa tự nhận RSA-SHA256 của XML-DSig; đăng ký một lần cho tiến trình.</summary>
        private static void RegisterAlgorithms()
        {
            lock (RegisterGate)
            {
                if (_registered) return;
                CryptoConfig.AddAlgorithm(typeof(RsaSha256SignatureDescription), RsaSha256Uri);
                _registered = true;
            }
        }

        public sealed class RsaSha256SignatureDescription : SignatureDescription
        {
            public RsaSha256SignatureDescription()
            {
                KeyAlgorithm = typeof(RSACryptoServiceProvider).FullName;
                DigestAlgorithm = typeof(SHA256Managed).FullName;
                FormatterAlgorithm = typeof(RSAPKCS1SignatureFormatter).FullName;
                DeformatterAlgorithm = typeof(RSAPKCS1SignatureDeformatter).FullName;
            }

            public override AsymmetricSignatureDeformatter CreateDeformatter(AsymmetricAlgorithm key)
            {
                var deformatter = new RSAPKCS1SignatureDeformatter(key);
                deformatter.SetHashAlgorithm("SHA256");
                return deformatter;
            }

            public override AsymmetricSignatureFormatter CreateFormatter(AsymmetricAlgorithm key)
            {
                var formatter = new RSAPKCS1SignatureFormatter(key);
                formatter.SetHashAlgorithm("SHA256");
                return formatter;
            }
        }
    }
}
