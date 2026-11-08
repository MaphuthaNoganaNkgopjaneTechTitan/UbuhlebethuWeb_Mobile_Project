using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UbuhlebethuConnectPro.Core.Models;
using UbuhlebethuConnectPro.WebApp.Data;

namespace UbuhlebethuConnectPro.Api.Controllers.Api
{
    public class MaterialSyncDto
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ProjectId { get; set; } = string.Empty;
        public string MaterialName { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
        public int Quantity { get; set; } = 1;
        public double UnitCost { get; set; } = 0.0;
        public bool IsScanned { get; set; }
    }

    public class RegisterMaterialRequest
    {
        public string Barcode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal UnitCost { get; set; }
        public string Unit { get; set; } = "Units";
        public string? ProjectId { get; set; }
    }

    [ApiController]
    [IgnoreAntiforgeryToken]
    [Route("api/v1/materials")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class MaterialsApiController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<MaterialsApiController> _logger;

        public MaterialsApiController(ApplicationDbContext db, ILogger<MaterialsApiController> logger)
        {
            _db = db;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetMaterials([FromQuery] string? projectId = null)
        {
            var query = _db.Materials.Where(m => !m.IsDeleted);

            if (!string.IsNullOrEmpty(projectId) && Guid.TryParse(projectId, out var pGuid))
            {
                query = query.Where(m => m.ProjectId == null || m.ProjectId == pGuid);
            }

            var list = await query.OrderByDescending(m => m.CreatedAt).ToListAsync();
            return Ok(list);
        }

        [HttpGet("lookup")]
        public async Task<IActionResult> LookupMaterial([FromQuery] string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode))
            {
                return BadRequest(new { error = "Barcode parameter is required." });
            }

            var cleanBarcode = barcode.Trim();
            var material = await _db.Materials
                .FirstOrDefaultAsync(m => !m.IsDeleted && m.Barcode == cleanBarcode);

            if (material == null)
            {
                return NotFound(new { error = "Item not found / not registered", barcode = cleanBarcode });
            }

            return Ok(new
            {
                id = material.Id,
                barcode = material.Barcode,
                name = material.Name,
                unitCost = material.UnitCost,
                unit = material.Unit,
                projectId = material.ProjectId,
                registeredBy = material.RegisteredBy,
                createdAt = material.CreatedAt
            });
        }

        [HttpPost("register")]
        public async Task<IActionResult> RegisterMaterial([FromBody] RegisterMaterialRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Barcode) || string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(new { error = "Barcode and Name are required." });
            }

            var cleanBarcode = request.Barcode.Trim();
            var cleanName = request.Name.Trim();
            var username = User.Identity?.Name ?? "SiteManager";

            Guid? pGuid = null;
            if (!string.IsNullOrWhiteSpace(request.ProjectId) && Guid.TryParse(request.ProjectId, out var parsed))
            {
                pGuid = parsed;
            }

            var existing = await _db.Materials.FirstOrDefaultAsync(m => !m.IsDeleted && m.Barcode == cleanBarcode);
            if (existing != null)
            {
                existing.Name = cleanName;
                existing.UnitCost = request.UnitCost;
                existing.Unit = string.IsNullOrWhiteSpace(request.Unit) ? "Units" : request.Unit.Trim();
                if (pGuid.HasValue) existing.ProjectId = pGuid;
                existing.RegisteredBy = username;
                await _db.SaveChangesAsync();

                _logger.LogInformation("Updated registered material: {Barcode} -> {Name} (R{Cost})", cleanBarcode, cleanName, request.UnitCost);
                return Ok(existing);
            }

            var newMaterial = new Material
            {
                Id = Guid.NewGuid(),
                Barcode = cleanBarcode,
                Name = cleanName,
                UnitCost = request.UnitCost,
                Unit = string.IsNullOrWhiteSpace(request.Unit) ? "Units" : request.Unit.Trim(),
                ProjectId = pGuid,
                RegisteredBy = username,
                CreatedAt = DateTime.UtcNow
            };

            _db.Materials.Add(newMaterial);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Registered new material: {Barcode} -> {Name} (R{Cost}) by {User}", cleanBarcode, cleanName, request.UnitCost, username);
            return Ok(newMaterial);
        }

        [HttpPost("sync")]
        public async Task<IActionResult> SyncMaterials([FromBody] List<MaterialSyncDto> items)
        {
            if (items == null || items.Count == 0)
            {
                return Ok(new { success = true, count = 0, message = "No materials to sync" });
            }

            int syncedCount = 0;
            foreach (var item in items)
            {
                if (string.IsNullOrWhiteSpace(item.Barcode) && string.IsNullOrWhiteSpace(item.MaterialName))
                    continue;

                syncedCount++;

                // If barcode exists in registered materials, ensure material is synced
                if (!string.IsNullOrWhiteSpace(item.Barcode))
                {
                    var cleanBarcode = item.Barcode.Trim();
                    var existingMat = await _db.Materials.FirstOrDefaultAsync(m => !m.IsDeleted && m.Barcode == cleanBarcode);
                    if (existingMat == null && !string.IsNullOrWhiteSpace(item.MaterialName) && item.MaterialName != "Scanned Material")
                    {
                        var newMat = new Material
                        {
                            Id = Guid.NewGuid(),
                            Barcode = cleanBarcode,
                            Name = item.MaterialName.Trim(),
                            UnitCost = (decimal)item.UnitCost,
                            RegisteredBy = User.Identity?.Name ?? "WorkerSync",
                            CreatedAt = DateTime.UtcNow
                        };
                        _db.Materials.Add(newMat);
                    }
                }

                // Also record in MaterialTasks if assigned to a project
                if (Guid.TryParse(item.ProjectId, out var projectGuid))
                {
                    var entityId = Guid.TryParse(item.Id, out var parsedId) ? parsedId : Guid.NewGuid();
                    var existing = await _db.MaterialTasks.FindAsync(entityId);
                    if (existing == null)
                    {
                        var newTask = new MaterialTask
                        {
                            Id = entityId,
                            ProjectId = projectGuid,
                            TaskName = item.MaterialName,
                            Quantity = item.Quantity,
                            Unit = "Units",
                            Completed = item.IsScanned,
                            DateRequired = DateTime.UtcNow
                        };
                        _db.MaterialTasks.Add(newTask);
                    }
                    else
                    {
                        existing.Quantity = item.Quantity;
                        existing.Completed = item.IsScanned;
                    }
                }
            }

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save material sync changes to database");
            }

            return Ok(new
            {
                success = true,
                count = syncedCount,
                syncedItems = items
            });
        }
    }
}
