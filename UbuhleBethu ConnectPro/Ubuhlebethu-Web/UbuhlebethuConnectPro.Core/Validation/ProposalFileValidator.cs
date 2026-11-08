using System.IO.Compression;

namespace UbuhlebethuConnectPro.Core.Validation
{
    public sealed record ProposalFileCheck(bool IsValid, string? Error, string ContentType, string SafeFileName);

    /// <summary>
    /// Server-side validation of an uploaded business proposal. Never trusts the browser's MIME type:
    /// the extension must be allowed AND the first bytes must match the format (PDF / OLE .doc / OOXML .docx).
    /// </summary>
    public static class ProposalFileValidator
    {
        public const long MaxBytes = 10 * 1024 * 1024; // 10 MB
        public static readonly string[] AllowedExtensions = { ".pdf", ".doc", ".docx" };

        public static ProposalFileCheck Validate(string? fileName, byte[] content)
        {
            var safe = SanitizeFileName(fileName);
            if (content == null || content.Length == 0) return Fail("The uploaded file is empty.", safe);
            if (content.LongLength > MaxBytes) return Fail("The file is larger than 10 MB.", safe);

            var ext = Path.GetExtension(safe).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext)) return Fail("Only PDF, DOC or DOCX files are accepted.", safe);

            switch (ext)
            {
                case ".pdf":
                    if (!StartsWith(content, "%PDF-"u8)) return Fail("This file is not a valid PDF.", safe);
                    return new ProposalFileCheck(true, null, "application/pdf", safe);
                case ".doc":
                    if (!StartsWith(content, new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }))
                        return Fail("This file is not a valid Word (.doc) document.", safe);
                    return new ProposalFileCheck(true, null, "application/msword", safe);
                default:
                    if (!StartsWith(content, new byte[] { 0x50, 0x4B, 0x03, 0x04 }) || !LooksLikeDocx(content))
                        return Fail("This file is not a valid Word (.docx) document.", safe);
                    return new ProposalFileCheck(true, null,
                        "application/vnd.openxmlformats-officedocument.wordprocessingml.document", safe);
            }
        }

        public static string SanitizeFileName(string? fileName)
        {
            var name = Path.GetFileName((fileName ?? "proposal").Replace('\\', '/'));
            var cleaned = new string(name.Where(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_' or ' ').ToArray()).Trim();
            if (cleaned.Length == 0) cleaned = "proposal";
            if (cleaned.Length > 120)
            {
                var ext = Path.GetExtension(cleaned);
                cleaned = cleaned[..(120 - ext.Length)] + ext;
            }
            return cleaned;
        }

        private static bool LooksLikeDocx(byte[] content)
        {
            try
            {
                using var ms = new MemoryStream(content, writable: false);
                using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
                return zip.Entries.Any(e => e.FullName == "word/document.xml");
            }
            catch (InvalidDataException) { return false; }
        }

        private static bool StartsWith(byte[] data, ReadOnlySpan<byte> prefix) =>
            data.Length >= prefix.Length && data.AsSpan(0, prefix.Length).SequenceEqual(prefix);

        private static ProposalFileCheck Fail(string error, string name) => new(false, error, "application/octet-stream", name);
    }
}
