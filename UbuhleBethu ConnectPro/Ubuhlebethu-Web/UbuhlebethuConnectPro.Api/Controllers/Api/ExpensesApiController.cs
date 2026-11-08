using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UbuhlebethuConnectPro.Core.Models;
using UbuhlebethuConnectPro.WebApp.Data;

namespace UbuhlebethuConnectPro.Api.Controllers.Api
{
    public class ExpenseDto
    {
        public string Id { get; set; } = string.Empty;
        public string ManagerName { get; set; } = string.Empty;
        public string ItemDescription { get; set; } = string.Empty;
        public double Amount { get; set; }
        public string Status { get; set; } = "APPROVED";
        public string? ProjectId { get; set; }
    }

    [ApiController]
    [IgnoreAntiforgeryToken]
    [Route("api/v1/expenses")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class ExpensesApiController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public ExpensesApiController(ApplicationDbContext db)
        {
            _db = db;
        }

        [HttpGet("")]
        public async Task<IActionResult> GetExpenses([FromQuery] string? projectId = null)
        {
            var query = _db.ExpenseRecords.AsQueryable();
            if (!string.IsNullOrWhiteSpace(projectId) && Guid.TryParse(projectId, out var parsedProjGuid))
            {
                query = query.Where(e => e.ProjectId == parsedProjGuid);
            }

            var list = await query.OrderByDescending(e => e.CreatedAt).ToListAsync();
            return Ok(list.Select(e => new ExpenseDto
            {
                Id = e.Id.ToString(),
                ManagerName = e.ManagerName,
                ItemDescription = e.ItemDescription,
                Amount = (double)e.Amount,
                Status = e.Status,
                ProjectId = e.ProjectId.ToString()
            }));
        }


        [HttpPost("")]
        public async Task<IActionResult> CreateExpense(
            [FromBody] ExpenseDto dto)
        {
            if (dto == null ||
                !Guid.TryParse(dto.Id, out var expenseId) ||
                expenseId == Guid.Empty)
            {
                return BadRequest(new
                {
                    error = "A valid expense ID is required."
                });
            }

            if (string.IsNullOrWhiteSpace(dto.ItemDescription) ||
                double.IsNaN(dto.Amount) ||
                double.IsInfinity(dto.Amount) ||
                dto.Amount <= 0)
            {
                return BadRequest(new
                {
                    error = "A valid description and amount are required."
                });
            }

            Guid projGuid = Guid.Empty;
            if (!string.IsNullOrWhiteSpace(dto.ProjectId))
            {
                Guid.TryParse(dto.ProjectId, out projGuid);
            }

            var existing = await _db.ExpenseRecords
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == expenseId);

            if (existing != null)
            {
                if (existing.ItemDescription != dto.ItemDescription ||
                    existing.Amount != (decimal)dto.Amount ||
                    existing.ProjectId != projGuid)
                {
                    return Conflict(new
                    {
                        error = "Expense ID belongs to a different record."
                    });
                }

                return Ok(new ExpenseDto
                {
                    Id = existing.Id.ToString(),
                    ManagerName = existing.ManagerName,
                    ItemDescription = existing.ItemDescription,
                    Amount = (double)existing.Amount,
                    Status = existing.Status,
                    ProjectId = existing.ProjectId.ToString()
                });
            }

            var exp = new ExpenseRecord
            {
                Id = expenseId,
                ManagerName = string.IsNullOrWhiteSpace(dto.ManagerName)
                    ? "Site Manager"
                    : dto.ManagerName,
                ItemDescription = dto.ItemDescription,
                Amount = (decimal)dto.Amount,
                Status = string.IsNullOrWhiteSpace(dto.Status)
                    ? "APPROVED"
                    : dto.Status,
                ProjectId = projGuid,
                CreatedAt = DateTime.UtcNow
            };

            _db.ExpenseRecords.Add(exp);

            await _db.SaveChangesAsync();

            return Ok(new ExpenseDto
            {
                Id = exp.Id.ToString(),
                ManagerName = exp.ManagerName,
                ItemDescription = exp.ItemDescription,
                Amount = (double)exp.Amount,
                Status = exp.Status,
                ProjectId = exp.ProjectId.ToString()
            });
        }


        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateExpense(string id, [FromBody] ExpenseDto dto)
        {
            if (!Guid.TryParse(id, out var expGuid)) return BadRequest();

            var exp = await _db.ExpenseRecords.FindAsync(expGuid);
            if (exp == null) return NotFound();

            exp.Status = dto.Status;
            await _db.SaveChangesAsync();

            return Ok(new ExpenseDto
            {
                Id = exp.Id.ToString(),
                ManagerName = exp.ManagerName,
                ItemDescription = exp.ItemDescription,
                Amount = (double)exp.Amount,
                Status = exp.Status,
                ProjectId = exp.ProjectId.ToString()
            });
        }
    }
}
