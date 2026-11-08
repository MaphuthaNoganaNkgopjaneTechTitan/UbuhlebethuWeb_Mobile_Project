using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UbuhlebethuConnectPro.Core.Models;
using UbuhlebethuConnectPro.WebApp.Data;
using UbuhlebethuConnectPro.Core.Repositories;

namespace UbuhlebethuConnectPro.Api.Controllers.Api
{
    [ApiController]
    [IgnoreAntiforgeryToken]
    [Route("api/v1")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class PaymentsApiController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IInvoiceRepository _invoiceRepo;

        public PaymentsApiController(ApplicationDbContext db, IInvoiceRepository invoiceRepo)
        {
            _db = db;
            _invoiceRepo = invoiceRepo;
        }

        public class ManualPaymentDto
        {
            public decimal Amount { get; set; }
            public PaymentMethod Method { get; set; }
            public string? Reference { get; set; }
        }

        // POST /api/v1/invoices/{id}/payments (Manual payment recording)
        [HttpPost("invoices/{id:guid}/payments")]
        [Authorize(Roles = "Admin,Office")]
        public async Task<IActionResult> RecordManualPayment(Guid id, [FromBody] ManualPaymentDto request)
        {
            var invoice = await _invoiceRepo.GetByIdAsync(id);
            if (invoice == null) return NotFound("Invoice not found.");

            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                InvoiceId = id,
                Amount = request.Amount,
                PaymentMethod = request.Method,
                PaymentStatus = PaymentStatus.Verified, // Manual entry implies verified
                PaymentReference = request.Reference,
                PaidAt = DateTime.UtcNow,
                RecordedBy = User.Identity?.Name,
                VerifiedBy = User.Identity?.Name,
                VerifiedAt = DateTime.UtcNow
            };

            await _invoiceRepo.AddPaymentAsync(payment);
            
            invoice.PaidAmount += request.Amount;
            invoice.UpdateStatus();
            
            await _db.SaveChangesAsync(); // Save both payment and invoice

            return Ok(new { message = "Payment recorded successfully", paymentId = payment.Id, newInvoiceStatus = invoice.Status.ToString() });
        }

        // POST /api/v1/invoices/{id}/proof (Client uploads proof of payment)
        [HttpPost("invoices/{id:guid}/proof")]
        [Authorize(Roles = "Client,Admin,Office")]
        public async Task<IActionResult> UploadProof(Guid id, IFormFile proof, [FromForm] decimal amount, [FromForm] string? reference)
        {
            var invoice = await _invoiceRepo.GetByIdAsync(id);
            if (invoice == null) return NotFound("Invoice not found.");

            if (proof == null || proof.Length == 0)
                return BadRequest("Proof file is required.");

            // Basic validation
            var ext = Path.GetExtension(proof.FileName).ToLowerInvariant();
            if (ext != ".pdf" && ext != ".jpg" && ext != ".jpeg" && ext != ".png")
                return BadRequest("Invalid file type. Only PDF and images are allowed.");

            // Save file (this is a simplified logic. In prod, you'd use a cloud storage or proper path)
            var fileName = $"{Guid.NewGuid()}{ext}";
            var dir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "proofs");
            Directory.CreateDirectory(dir);
            var filePath = Path.Combine(dir, fileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await proof.CopyToAsync(stream);
            }

            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                InvoiceId = id,
                Amount = amount,
                PaymentMethod = PaymentMethod.EFT, // Defaulting to EFT for proof uploads
                PaymentStatus = PaymentStatus.Pending,
                PaymentReference = reference,
                ProofPath = $"/uploads/proofs/{fileName}",
                PaidAt = DateTime.UtcNow,
                RecordedBy = User.Identity?.Name
            };

            await _invoiceRepo.AddPaymentAsync(payment);
            await _db.SaveChangesAsync();

            return Ok(new { message = "Proof uploaded successfully. Awaiting verification.", paymentId = payment.Id });
        }

        // PATCH /api/v1/payments/{id}/verify (Office/QS verifies proof)
        [HttpPatch("payments/{id:guid}/verify")]
        [Authorize(Roles = "Admin,Office")]
        public async Task<IActionResult> VerifyPayment(Guid id, [FromQuery] bool approve = true)
        {
            var payment = await _db.Payments.FindAsync(id);
            if (payment == null) return NotFound("Payment not found.");

            if (payment.PaymentStatus != PaymentStatus.Pending)
                return BadRequest("Payment is not in a pending state.");

            if (approve)
            {
                payment.PaymentStatus = PaymentStatus.Verified;
                payment.VerifiedBy = User.Identity?.Name;
                payment.VerifiedAt = DateTime.UtcNow;

                var invoice = await _invoiceRepo.GetByIdAsync(payment.InvoiceId);
                if (invoice != null)
                {
                    invoice.PaidAmount += payment.Amount;
                    invoice.UpdateStatus();
                }
            }
            else
            {
                payment.PaymentStatus = PaymentStatus.Rejected;
                payment.VerifiedBy = User.Identity?.Name;
                payment.VerifiedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();

            return Ok(new { message = $"Payment {(approve ? "verified" : "rejected")}." });
        }
    }
}
