
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UbuhlebethuConnectPro.Core.Models;
using UbuhlebethuConnectPro.WebApp.Data;

namespace UbuhlebethuConnectPro.Api.Controllers.Api
{
    public class AssignedTaskDto
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ProjectId { get; set; } = string.Empty;
        public string TaskTitle { get; set; } = string.Empty;
        public string AssignedWorker { get; set; } = string.Empty;
        public bool IsCompleted { get; set; } = false;
    }

    [ApiController]
    [IgnoreAntiforgeryToken]
    [Route("api/v1/tasks")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class TasksApiController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        // Existing in-memory task storage.
        private static readonly ConcurrentDictionary<string, AssignedTaskDto>
            _taskStore = new();

        public TasksApiController(ApplicationDbContext db)
        {
            _db = db;
        }

        // Check whether the authenticated user is an Admin or Site Manager.
        private bool IsManager()
        {
            return User.IsInRole("Admin")
                || User.IsInRole("SiteManager")
                || User.IsInRole("Site Manager");
        }

        // Compare a task's assignment with the authenticated username.
        private static bool IsAssignedTo(
            AssignedTaskDto task,
            string username)
        {
            return !string.IsNullOrWhiteSpace(username)
                && string.Equals(
                    task.AssignedWorker?.Trim(),
                    username.Trim(),
                    StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsAssignedTo(
            MaterialTask task,
            string username)
        {
            return !string.IsNullOrWhiteSpace(username)
                && string.Equals(
                    task.AssignedTo?.Trim(),
                    username.Trim(),
                    StringComparison.OrdinalIgnoreCase);
        }

        private static AssignedTaskDto ToDto(MaterialTask task)
        {
            return new AssignedTaskDto
            {
                Id = task.Id.ToString(),
                ProjectId = task.ProjectId.ToString(),
                TaskTitle = task.TaskName,
                AssignedWorker = task.AssignedTo ?? string.Empty,
                IsCompleted = task.Completed
            };
        }

        // GET api/v1/tasks?projectId=...
        [HttpGet]
        public async Task<IActionResult> GetTasks(
            [FromQuery] string? projectId = null)
        {
            var username = User.FindFirstValue(ClaimTypes.Name);

            if (string.IsNullOrWhiteSpace(username))
                return Unauthorized();

            var isManager = IsManager();

            var result = new List<AssignedTaskDto>();

            // Retrieve active tasks from the database.
            if (Guid.TryParse(projectId, out var projectGuid))
            {
                var query = _db.MaterialTasks
                    .AsNoTracking()
                    .Where(t => !t.IsDeleted &&
                                t.ProjectId == projectGuid);

                if (!isManager)
                {
                    query = query.Where(t =>
                        t.AssignedTo != null &&
                        t.AssignedTo.ToLower() ==
                            username.ToLower());
                }

                var databaseTasks = await query.ToListAsync();
                result.AddRange(databaseTasks.Select(ToDto));
            }

            // Include tasks from the existing in-memory store.
            var memoryTasks = _taskStore.Values.Where(t =>
                string.IsNullOrWhiteSpace(projectId) ||
                string.Equals(
                    t.ProjectId,
                    projectId,
                    StringComparison.OrdinalIgnoreCase));

            if (!isManager)
            {
                memoryTasks = memoryTasks.Where(t =>
                    IsAssignedTo(t, username));
            }

            foreach (var task in memoryTasks)
            {
                if (!result.Any(r => r.Id == task.Id))
                {
                    result.Add(task);
                }
            }

            return Ok(result);
        }

        // POST api/v1/tasks
        [HttpPost]
        public async Task<IActionResult> AssignTask(
            [FromBody] AssignedTaskDto dto)
        {
            if (!IsManager())
                return Forbid();

            if (dto == null)
                return BadRequest("Task data is required.");

            if (string.IsNullOrWhiteSpace(dto.TaskTitle) ||
                string.IsNullOrWhiteSpace(dto.AssignedWorker))
            {
                return BadRequest(
                    "Task title and assigned worker are required.");
            }

            if (!Guid.TryParse(dto.ProjectId, out var projectGuid))
                return BadRequest("A valid project ID is required.");

            if (string.IsNullOrWhiteSpace(dto.Id))
                dto.Id = Guid.NewGuid().ToString();

            if (!Guid.TryParse(dto.Id, out var taskGuid))
                return BadRequest("A valid task ID is required.");

            // Prevent accidental overwriting of existing assignments.
            if (_taskStore.ContainsKey(dto.Id) ||
                await _db.MaterialTasks.AnyAsync(t => t.Id == taskGuid))
            {
                return Conflict("A task with this ID already exists.");
            }

            var entity = new MaterialTask
            {
                Id = taskGuid,
                ProjectId = projectGuid,
                TaskName = dto.TaskTitle.Trim(),
                Quantity = 1,
                Unit = "Task",
                Completed = dto.IsCompleted,
                DateRequired = DateTime.UtcNow,
                AssignedTo = dto.AssignedWorker.Trim()
            };

            _db.MaterialTasks.Add(entity);
            await _db.SaveChangesAsync();

            var savedTask = ToDto(entity);
            _taskStore[savedTask.Id] = savedTask;

            return Ok(savedTask);
        }

        // PUT api/v1/tasks/{id}/complete
        [HttpPut("{id}/complete")]
        public async Task<IActionResult> CompleteTask(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return BadRequest("Task ID is required.");

            var username = User.FindFirstValue(ClaimTypes.Name);

            if (string.IsNullOrWhiteSpace(username))
                return Unauthorized();

            // Check the database first.
            if (Guid.TryParse(id, out var taskGuid))
            {
                var entity = await _db.MaterialTasks
                    .FirstOrDefaultAsync(t =>
                        t.Id == taskGuid && !t.IsDeleted);

                if (entity != null)
                {
                    if (!IsManager() &&
                        !IsAssignedTo(entity, username))
                    {
                        return Forbid();
                    }

                    entity.Completed = true;
                    await _db.SaveChangesAsync();

                    var updated = ToDto(entity);
                    _taskStore[updated.Id] = updated;

                    return Ok(updated);
                }
            }

            // Support tasks that exist only in memory.
            if (_taskStore.TryGetValue(id, out var memoryTask))
            {
                if (!IsManager() &&
                    !IsAssignedTo(memoryTask, username))
                {
                    return Forbid();
                }

                memoryTask.IsCompleted = true;
                _taskStore[id] = memoryTask;

                return Ok(memoryTask);
            }

            // Never create a dummy task for an unknown ID.
            return NotFound(new
            {
                error = "Task not found."
            });
        }
    }
}
