using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace UbuhlebethuConnectPro.Api.Controllers.Api
{
    public class ExpenseItem
    {
        public string ManagerName { get; set; } = string.Empty;
        public string ItemDescription { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Status { get; set; } = "PENDING";
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ProjectId { get; set; } = string.Empty;
    }

    [ApiController]
    [IgnoreAntiforgeryToken]
    [Route("api/v1/expenses-legacy")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class ExpenseApiController : ControllerBase
    {
        private static readonly ConcurrentDictionary<string, ExpenseItem> _expenses =
            new ConcurrentDictionary<string, ExpenseItem>();

        static ExpenseApiController()
        {
            var item1 = new ExpenseItem { ManagerName = "Site Manager", ItemDescription = "Shri Material Supply", Amount = 3500.00M, Status = "PENDING", ProjectId = "" };
            var item2 = new ExpenseItem { ManagerName = "Site Manager", ItemDescription = "Field Refreshments", Amount = 300.00M, Status = "PENDING", ProjectId = "" };
            var item3 = new ExpenseItem { ManagerName = "Site Manager", ItemDescription = "Casual Labor Wage", Amount = 500.00M, Status = "PENDING", ProjectId = "" };

            _expenses[item1.Id] = item1;
            _expenses[item2.Id] = item2;
            _expenses[item3.Id] = item3;
        }

        [HttpGet]
        public IActionResult GetExpenses([FromQuery] string? projectId = null)
        {
            if (!string.IsNullOrEmpty(projectId))
            {
                var filtered = _expenses.Values
                    .Where(e => string.IsNullOrEmpty(e.ProjectId) || string.Equals(e.ProjectId, projectId, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                return Ok(filtered);
            }
            return Ok(_expenses.Values.ToList());
        }

        [HttpPost]
        public IActionResult SubmitExpense([FromBody] ExpenseItem expense)
        {
            if (expense == null) return BadRequest("Expense data is required.");

            // Find existing by ID or matching description and amount
            var existing = _expenses.Values.FirstOrDefault(e =>
                (!string.IsNullOrEmpty(expense.Id) && e.Id == expense.Id) ||
                (e.ItemDescription == expense.ItemDescription && e.Amount == expense.Amount));

            if (existing != null)
            {
                existing.Status = expense.Status;
                _expenses[existing.Id] = existing;
                return Ok(existing);
            }

            if (string.IsNullOrEmpty(expense.Id))
            {
                expense.Id = Guid.NewGuid().ToString();
            }

            _expenses[expense.Id] = expense;
            return Ok(expense);
        }
    }
}
