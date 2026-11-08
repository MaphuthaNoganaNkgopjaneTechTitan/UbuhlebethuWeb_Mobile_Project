using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UbuhlebethuConnectPro.Core.Models;
using UbuhlebethuConnectPro.WebApp.Data;

namespace UbuhlebethuConnectPro.Api.Controllers.Api
{
    [ApiController]
    [IgnoreAntiforgeryToken]
    [Route("api/v1/invoices")]
    [Authorize(AuthenticationSchemes = "Bearer")]
    public class InvoiceApiController : ControllerBase
    {
        private readonly UbuhlebethuConnectPro.WebApp.Data.ApplicationDbContext _dbContext;
        private static readonly CultureInfo ZaCulture = CultureInfo.GetCultureInfo("en-ZA");

        public InvoiceApiController(UbuhlebethuConnectPro.WebApp.Data.ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public class InvoiceDto
        {
            public Guid Id { get; set; }
            public decimal Amount { get; set; }
            public DateTime CreatedAt { get; set; }
            public bool IsPaid { get; set; }
            public Guid QuoteRequestId { get; set; }

            // Quote Info
            public string QuoteProjectDescription { get; set; } = string.Empty;
            public string QuoteClientName { get; set; } = string.Empty;
            public string QuoteReference { get; set; } = string.Empty;
            public string QuoteSiteLocation { get; set; } = string.Empty;
            public string QuoteProjectType { get; set; } = string.Empty;
        }

        // GET: api/v1/invoices/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<InvoiceDto>> GetInvoice(Guid id)
        {
            var invoice = await _dbContext.Invoices.FindAsync(id);
            if (invoice == null) return NotFound();

            var quote = await _dbContext.QuoteRequests.FindAsync(invoice.QuoteRequestId);
            if (quote == null) return NotFound("Associated quote not found");

            var currentUserName = User.Identity?.Name ?? string.Empty;
            bool isStaff = User.IsInRole("Admin") || User.IsInRole("SystemAdmin") || User.IsInRole("Office") || User.IsInRole("OfficeStaff") || User.IsInRole("Manager");
            bool isOwner = !string.IsNullOrEmpty(quote.CreatedBy) && string.Equals(quote.CreatedBy, currentUserName, StringComparison.OrdinalIgnoreCase);

            if (!isStaff && !isOwner)
            {
                return Forbid();
            }

            if (quote.EstimatedBudget > 0 && invoice.Amount != quote.EstimatedBudget)
            {
                invoice.Amount = quote.EstimatedBudget;
                await _dbContext.SaveChangesAsync();
            }

            return Ok(new InvoiceDto
            {
                Id = invoice.Id,
                Amount = invoice.Amount,
                CreatedAt = invoice.CreatedAt,
                IsPaid = invoice.IsPaid,
                QuoteRequestId = invoice.QuoteRequestId,
                QuoteProjectDescription = quote.ProjectDescription ?? string.Empty,
                QuoteClientName = quote.ClientName ?? string.Empty,
                QuoteReference = quote.Reference ?? string.Empty,
                QuoteSiteLocation = quote.SiteLocation ?? string.Empty,
                QuoteProjectType = quote.ProjectType ?? string.Empty
            });
        }

        // GET: api/v1/invoices/{id}/pdf (Direct PDF Stream)
        [HttpGet("{id}/pdf")]
        public async Task<IActionResult> GetInvoicePdf(Guid id)
        {
            var invoice = await _dbContext.Invoices.FindAsync(id);
            if (invoice == null) return NotFound("Invoice record not found");

            var quote = await _dbContext.QuoteRequests.FindAsync(invoice.QuoteRequestId);
            if (quote == null) return NotFound("Associated quote not found");

            var currentUserName = User.Identity?.Name ?? string.Empty;
            bool isStaff = User.IsInRole("Admin") || User.IsInRole("SystemAdmin") || User.IsInRole("Office") || User.IsInRole("OfficeStaff") || User.IsInRole("Manager");
            bool isOwner = !string.IsNullOrEmpty(quote.CreatedBy) && string.Equals(quote.CreatedBy, currentUserName, StringComparison.OrdinalIgnoreCase);

            if (!isStaff && !isOwner)
            {
                return Forbid();
            }

            if (quote.EstimatedBudget > 0 && invoice.Amount != quote.EstimatedBudget)
            {
                invoice.Amount = quote.EstimatedBudget;
                await _dbContext.SaveChangesAsync();
            }

            byte[] pdfBytes = GenerateNativeInvoicePdf(invoice, quote);
            string fileName = $"TaxInvoice_{(quote.Reference ?? invoice.Id.ToString().Substring(0, 8))}.pdf";

            return File(pdfBytes, "application/pdf", fileName);
        }

        // GET: api/v1/invoices/{id}/html
        [HttpGet("{id}/html")]
        public async Task<IActionResult> GetInvoiceHtml(Guid id)
        {
            var invoice = await _dbContext.Invoices.FindAsync(id);
            if (invoice == null) return NotFound();

            var quote = await _dbContext.QuoteRequests.FindAsync(invoice.QuoteRequestId);
            if (quote == null) return NotFound("Associated quote not found");

            var currentUserName = User.Identity?.Name ?? string.Empty;
            bool isStaff = User.IsInRole("Admin") || User.IsInRole("SystemAdmin") || User.IsInRole("Office") || User.IsInRole("OfficeStaff") || User.IsInRole("Manager");
            bool isOwner = !string.IsNullOrEmpty(quote.CreatedBy) && string.Equals(quote.CreatedBy, currentUserName, StringComparison.OrdinalIgnoreCase);

            if (!isStaff && !isOwner)
            {
                return Forbid();
            }

            if (quote.EstimatedBudget > 0 && invoice.Amount != quote.EstimatedBudget)
            {
                invoice.Amount = quote.EstimatedBudget;
                await _dbContext.SaveChangesAsync();
            }

            var html = RenderInvoiceHtml(invoice, quote);
            var bytes = Encoding.UTF8.GetBytes(html);
            return File(bytes, "text/html", $"invoice-{id}.html");
        }

        private string RenderInvoiceHtml(Invoice invoice, QuoteRequest quote)
        {
            var encoder = System.Text.Encodings.Web.HtmlEncoder.Default;
            var invoiceId = encoder.Encode(invoice.Id.ToString());
            var client = encoder.Encode(quote.ClientName ?? "Unknown");
            var reference = encoder.Encode(quote.Reference ?? "N/A");
            var projectType = encoder.Encode(quote.ProjectType ?? "N/A");
            var siteLocation = encoder.Encode(quote.SiteLocation ?? "N/A");
            var projectDescription = encoder.Encode(quote.ProjectDescription ?? string.Empty);
            var amount = encoder.Encode("R " + invoice.Amount.ToString("N2", ZaCulture));
            var created = encoder.Encode(invoice.CreatedAt.ToLocalTime().ToString("g"));

            return $@"<!doctype html>
<html>
<head><meta charset='utf-8'><title>Invoice {invoiceId}</title></head>
<body>
<h2>Ubuhlebethu ConnectPro - Invoice #{invoiceId}</h2>
<p><strong>Client:</strong> {client}</p>
<p><strong>Reference:</strong> {reference}</p>
<p><strong>Project Type:</strong> {projectType}</p>
<p><strong>Site Location:</strong> {siteLocation}</p>
<p><strong>Description:</strong> {projectDescription}</p>
<p><strong>Amount:</strong> {amount}</p>
<p><strong>Date:</strong> {created}</p>
<hr />
<p>This is a prototype invoice (HTML). For production, deliver PDF or signed invoice.</p>
</body></html>";
        }

        private static byte[] GenerateNativeInvoicePdf(Invoice invoice, QuoteRequest quote)
        {
            decimal total = invoice.Amount;
            decimal subtotal = Math.Round(total / 1.15m, 2);
            decimal vat = total - subtotal;

            string client = EscapePdf(quote.ClientName ?? "Valued Client");
            string reference = EscapePdf(quote.Reference ?? invoice.Id.ToString().Substring(0, 8));
            string location = EscapePdf(quote.SiteLocation ?? "Gauteng, South Africa");
            string projectType = EscapePdf(quote.ProjectType ?? "Commercial");
            string desc = EscapePdf(string.IsNullOrWhiteSpace(quote.ProjectDescription) ? "Construction Contract Works" : quote.ProjectDescription);
            if (desc.Length > 70) desc = desc.Substring(0, 67) + "...";

            string status = invoice.IsPaid ? "PAID" : "UNPAID";
            string issueDate = invoice.CreatedAt.ToString("dd MMM yyyy");
            string invoiceNumber = "INV-" + invoice.Id.ToString().Substring(0, 8).ToUpperInvariant();

            var sb = new StringBuilder();
            // Header Top Bar
            sb.AppendLine("0.608 0.110 0.110 rg");
            sb.AppendLine("0 820 595 22 re f");

            // Company Letterhead
            sb.AppendLine("BT /F2 20 Tf 0.608 0.110 0.110 rg 40 780 Td (UBUHLEBETHU) Tj ET");
            sb.AppendLine("BT /F1 9 Tf 0.3 0.3 0.3 rg 40 766 Td (BUSINESS ENTERPRISE CC - CIDB 8GB PE · REG 2002/095900/23) Tj ET");
            sb.AppendLine("BT /F1 8 Tf 0.4 0.4 0.4 rg 40 754 Td (195 Corlett Drive, Bramley, 2018 | Tel: 011 887 7240 | info@ubuhlebethu.co.za) Tj ET");

            // Invoice Title & Meta
            sb.AppendLine("BT /F2 18 Tf 0.608 0.110 0.110 rg 420 780 Td (TAX INVOICE) Tj ET");
            sb.AppendLine($"BT /F2 10 Tf 0.1 0.1 0.1 rg 420 764 Td ({invoiceNumber}) Tj ET");
            sb.AppendLine($"BT /F1 9 Tf 0.3 0.3 0.3 rg 420 750 Td (Date: {issueDate}) Tj ET");
            sb.AppendLine($"BT /F2 10 Tf {(invoice.IsPaid ? "0.086 0.639 0.290" : "0.863 0.149 0.149")} rg 420 736 Td (STATUS: {status}) Tj ET");

            // Dividing Rule
            sb.AppendLine("0.8 0.8 0.8 RG 1 w 40 722 m 555 722 l S");

            // Bill To & Details
            sb.AppendLine("BT /F2 10 Tf 0.608 0.110 0.110 rg 40 700 Td (BILLED TO:) Tj ET");
            sb.AppendLine($"BT /F2 11 Tf 0.1 0.1 0.1 rg 40 684 Td ({client}) Tj ET");
            sb.AppendLine($"BT /F1 9 Tf 0.3 0.3 0.3 rg 40 670 Td (Site Location: {location}) Tj ET");
            sb.AppendLine($"BT /F1 9 Tf 0.3 0.3 0.3 rg 40 656 Td (Project Category: {projectType}) Tj ET");

            sb.AppendLine("BT /F2 10 Tf 0.608 0.110 0.110 rg 330 700 Td (PROJECT DETAILS:) Tj ET");
            sb.AppendLine($"BT /F1 9 Tf 0.1 0.1 0.1 rg 330 684 Td (Quotation Ref: {reference}) Tj ET");
            sb.AppendLine($"BT /F1 9 Tf 0.3 0.3 0.3 rg 330 670 Td (Payment Terms: Net 30 Days) Tj ET");
            sb.AppendLine($"BT /F1 9 Tf 0.3 0.3 0.3 rg 330 656 Td (Currency: South African Rands [ZAR]) Tj ET");

            // Table Header Background & Text
            sb.AppendLine("0.608 0.110 0.110 rg 40 610 515 22 re f");
            sb.AppendLine("BT /F2 9 Tf 1 1 1 rg 48 617 Td (#) Tj ET");
            sb.AppendLine("BT /F2 9 Tf 1 1 1 rg 75 617 Td (Description of Works & Construction Services) Tj ET");
            sb.AppendLine("BT /F2 9 Tf 1 1 1 rg 390 617 Td (Qty) Tj ET");
            sb.AppendLine("BT /F2 9 Tf 1 1 1 rg 460 617 Td (Amount) Tj ET");

            // Table Row 1
            sb.AppendLine("0.96 0.96 0.96 rg 40 570 515 36 re f");
            sb.AppendLine("0.85 0.85 0.85 RG 1 w 40 570 m 555 570 l S");
            sb.AppendLine("BT /F1 9 Tf 0.1 0.1 0.1 rg 48 588 Td (1) Tj ET");
            sb.AppendLine($"BT /F2 9 Tf 0.1 0.1 0.1 rg 75 590 Td ({desc}) Tj ET");
            sb.AppendLine($"BT /F1 8 Tf 0.4 0.4 0.4 rg 75 578 Td (Site: {location}) Tj ET");
            sb.AppendLine("BT /F1 9 Tf 0.1 0.1 0.1 rg 390 588 Td (1 Lot) Tj ET");
            sb.AppendLine($"BT /F2 9 Tf 0.1 0.1 0.1 rg 460 588 Td (R {subtotal.ToString("N2", ZaCulture)}) Tj ET");

            // Banking Details Box
            sb.AppendLine("0.97 0.97 0.97 rg 40 460 250 85 re f");
            sb.AppendLine("0.85 0.85 0.85 RG 1 w 40 460 250 85 re S");
            sb.AppendLine("BT /F2 9 Tf 0.608 0.110 0.110 rg 50 530 Td (BANKING PAYMENT DETAILS) Tj ET");
            sb.AppendLine("BT /F1 8 Tf 0.2 0.2 0.2 rg 50 515 Td (Bank: First National Bank [FNB]) Tj ET");
            sb.AppendLine("BT /F1 8 Tf 0.2 0.2 0.2 rg 50 502 Td (Account Name: Ubuhlebethu Business Enterprise cc) Tj ET");
            sb.AppendLine("BT /F1 8 Tf 0.2 0.2 0.2 rg 50 489 Td (Account No: 62000000000  ·  Branch: 250655) Tj ET");
            sb.AppendLine($"BT /F2 8 Tf 0.1 0.1 0.1 rg 50 474 Td (Payment Reference: {reference}) Tj ET");

            // Totals Column
            sb.AppendLine($"BT /F1 9 Tf 0.3 0.3 0.3 rg 340 530 Td (Subtotal [excl. VAT]:) Tj ET");
            sb.AppendLine($"BT /F2 9 Tf 0.1 0.1 0.1 rg 460 530 Td (R {subtotal.ToString("N2", ZaCulture)}) Tj ET");

            sb.AppendLine($"BT /F1 9 Tf 0.3 0.3 0.3 rg 340 510 Td (VAT [15%]:) Tj ET");
            sb.AppendLine($"BT /F2 9 Tf 0.1 0.1 0.1 rg 460 510 Td (R {vat.ToString("N2", ZaCulture)}) Tj ET");

            sb.AppendLine("0.8 0.8 0.8 RG 1 w 340 500 m 555 500 l S");

            sb.AppendLine("0.98 0.92 0.92 rg 335 465 220 28 re f");
            sb.AppendLine("BT /F2 11 Tf 0.608 0.110 0.110 rg 345 476 Td (TOTAL DUE:) Tj ET");
            sb.AppendLine($"BT /F2 11 Tf 0.608 0.110 0.110 rg 450 476 Td (R {total.ToString("N2", ZaCulture)}) Tj ET");

            // Footer
            sb.AppendLine("0.8 0.8 0.8 RG 0.5 w 40 80 m 555 80 l S");
            sb.AppendLine("BT /F1 8 Tf 0.4 0.4 0.4 rg 120 65 Td (Thank you for your business. Certified 100% Black Female Owned CIDB 8GB PE Enterprise.) Tj ET");
            sb.AppendLine("BT /F1 7 Tf 0.5 0.5 0.5 rg 180 52 Td (Ubuhlebethu Business Enterprise cc · www.ubuhlebethu.co.za · ISO Compliant) Tj ET");

            string streamContent = sb.ToString();
            byte[] streamBytes = Encoding.ASCII.GetBytes(streamContent);

            using var ms = new MemoryStream();
            using var writer = new StreamWriter(ms, Encoding.ASCII, 1024, leaveOpen: true);

            writer.Write("%PDF-1.4\n");
            writer.Flush();

            var offsets = new long[7];

            offsets[1] = ms.Position;
            writer.Write("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
            writer.Flush();

            offsets[2] = ms.Position;
            writer.Write("2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n");
            writer.Flush();

            offsets[3] = ms.Position;
            writer.Write("3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595.28 841.89] /Resources << /Font << /F1 4 0 R /F2 5 0 R >> >> /Contents 6 0 R >>\nendobj\n");
            writer.Flush();

            offsets[4] = ms.Position;
            writer.Write("4 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n");
            writer.Flush();

            offsets[5] = ms.Position;
            writer.Write("5 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>\nendobj\n");
            writer.Flush();

            offsets[6] = ms.Position;
            writer.Write($"6 0 obj\n<< /Length {streamBytes.Length} >>\nstream\n");
            writer.Flush();
            ms.Write(streamBytes, 0, streamBytes.Length);
            writer.Write("\nendstream\nendobj\n");
            writer.Flush();

            long xrefOffset = ms.Position;
            writer.Write("xref\n0 7\n0000000000 65535 f \n");
            for (int i = 1; i <= 6; i++)
            {
                writer.Write($"{offsets[i]:D10} 00000 n \n");
            }
            writer.Write($"trailer\n<< /Size 7 /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF");
            writer.Flush();

            return ms.ToArray();
        }

        private static string EscapePdf(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            return input.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
        }
    }
}