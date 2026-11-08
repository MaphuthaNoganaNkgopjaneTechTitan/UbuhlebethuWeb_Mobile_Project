using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using UbuhlebethuConnectPro.Core.Models;
using UbuhlebethuConnectPro.Core.Models.DTOs;
using UbuhlebethuConnectPro.WebApp.Models.DTOs;

namespace UbuhlebethuConnectPro.WebApp.ApiClient
{
    public class ConnectProApiClient : IConnectProApiClient
    {
        private readonly HttpClient _httpClient;
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
        };

        public ConnectProApiClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<LoginResponse?> LoginAsync(LoginRequest request)
        {
            var response = await _httpClient.PostAsJsonAsync("api/v1/auth/login", request);
            if ((int)response.StatusCode == 423)
            {
                throw new InvalidOperationException("LockedOut");
            }
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<LoginResponse>(_jsonOptions);
        }

        public async Task<LoginResponse?> RegisterAsync(RegisterRequest request)
        {
            var response = await _httpClient.PostAsJsonAsync("api/v1/auth/register", request);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                try
                {
                    using var doc = JsonDocument.Parse(errorContent);
                    if (doc.RootElement.TryGetProperty("error", out var errProp))
                    {
                        throw new InvalidOperationException(errProp.GetString());
                    }
                }
                catch (InvalidOperationException) { throw; }
                catch { }
                throw new InvalidOperationException("Registration failed: " + response.ReasonPhrase);
            }
            return await response.Content.ReadFromJsonAsync<LoginResponse>(_jsonOptions);
        }

        public async Task<RefreshResponse> RefreshAsync(RefreshRequest request)
        {
            var response = await _httpClient.PostAsJsonAsync("api/v1/auth/refresh", request);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<RefreshResponse>(_jsonOptions))!;
        }

        public async Task<List<QuoteDto>> GetQuotesAsync()
        {
            var response = await _httpClient.GetAsync("api/v1/quotes");
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<List<QuoteDto>>(_jsonOptions)) ?? new List<QuoteDto>();
        }

        public async Task<QuoteDto> GetQuoteAsync(Guid id)
        {
            var response = await _httpClient.GetAsync($"api/v1/quotes/{id}");
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<QuoteDto>(_jsonOptions))!;
        }

        public async Task<QuoteDto> CreateQuoteAsync(QuoteDtoForCreation quoteDto)
        {
            var response = await _httpClient.PostAsJsonAsync("api/v1/quotes", quoteDto);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<QuoteDto>(_jsonOptions))!;
        }

        public async Task<QuoteDto?> TrackProposalAsync(string reference, string email)
        {
            var response = await _httpClient.GetAsync($"api/v1/quotes/track?reference={Uri.EscapeDataString(reference)}&email={Uri.EscapeDataString(email)}");
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<QuoteDto>(_jsonOptions);
        }

        public async Task<List<QuoteDto>> GetPendingQuotesAsync()
        {
            var response = await _httpClient.GetAsync("api/v1/office/pending-quotes");
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<List<QuoteDto>>(_jsonOptions)) ?? new List<QuoteDto>();
        }

        public async Task<QuoteDto> GenerateDraftEstimateAsync(Guid id)
        {
            var response = await _httpClient.PostAsJsonAsync($"api/v1/office/generate-draft/{id}", new { });
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<QuoteDto>(_jsonOptions))!;
        }

        public async Task<QuoteDto> ApproveQuoteAsync(Guid id)
        {
            var response = await _httpClient.PostAsJsonAsync($"api/v1/office/approve/{id}", new { });
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<QuoteDto>(_jsonOptions))!;
        }

        public async Task<string> SendInvoiceAsync(Guid id)
        {
            var response = await _httpClient.PostAsJsonAsync($"api/v1/office/send-invoice/{id}", new { });
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<string>(_jsonOptions)) ?? string.Empty;
        }

        public async Task PayQuoteAsync(Guid id)
        {
            var response = await _httpClient.PostAsJsonAsync($"api/v1/quotes/{id}/pay", new { });
            response.EnsureSuccessStatusCode();
        }

        public async Task RejectQuoteAsync(Guid id, string? reason = null)
        {
            var response = await _httpClient.PostAsJsonAsync($"api/v1/quotes/{id}/reject", reason ?? string.Empty);
            response.EnsureSuccessStatusCode();
        }

        public async Task<List<UserDto>> GetUsersAsync(string? search = null, string? role = null, int page = 1, int pageSize = 20)
        {
            var response = await _httpClient.GetAsync($"api/v1/users?search={search ?? ""}&role={role ?? ""}&page={page}&pageSize={pageSize}");
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<List<UserDto>>(_jsonOptions)) ?? new List<UserDto>();
        }

        public async Task<UserDto> GetUserAsync(string id)
        {
            var response = await _httpClient.GetAsync($"api/v1/users/{id}");
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<UserDto>(_jsonOptions))!;
        }

        public async Task<UserDto> CreateUserAsync(CreateUserRequest request)
        {
            var response = await _httpClient.PostAsJsonAsync("api/v1/users", request);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<UserDto>(_jsonOptions))!;
        }

        public async Task<List<string>> GetAvailableRolesAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("api/v1/users/roles");
                if (response.IsSuccessStatusCode)
                {
                    var roles = await response.Content.ReadFromJsonAsync<List<string>>(_jsonOptions);
                    if (roles != null && roles.Any()) return roles;
                }
            }
            catch { }

            return new List<string> { "Admin", "Client", "FieldWorker", "Manager", "Office", "SiteManager" };
        }

        public async Task<UserDto> UpdateUserAsync(string id, UpdateUserRequest request)
        {
            var response = await _httpClient.PutAsJsonAsync($"api/v1/users/{id}", request);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<UserDto>(_jsonOptions))!;
        }

        public async Task<string> ResetPasswordAsync(string id, ResetPasswordRequest request)
        {
            var response = await _httpClient.PostAsJsonAsync($"api/v1/users/{id}/reset-password", request);
            response.EnsureSuccessStatusCode();
            var res = await response.Content.ReadFromJsonAsync<MessageResponse>(_jsonOptions);
            return res?.Message ?? "TempPassword123!";
        }

        public async Task ChangePasswordAsync(string id, ChangePasswordRequest request)
        {
            var response = await _httpClient.PostAsJsonAsync($"api/v1/users/{id}/change-password", request);
            response.EnsureSuccessStatusCode();
        }

        public async Task DeactivateUserAsync(string id, DeactivateRequest request)
        {
            var response = await _httpClient.PostAsJsonAsync($"api/v1/users/{id}/deactivate", request);
            response.EnsureSuccessStatusCode();
        }

        public async Task ReactivateUserAsync(string id, ReactivateRequest request)
        {
            var response = await _httpClient.PostAsJsonAsync($"api/v1/users/{id}/reactivate", request);
            response.EnsureSuccessStatusCode();
        }

        public async Task HardDeleteUserAsync(string id, DeleteUserRequest request)
        {
            var response = await _httpClient.PostAsJsonAsync($"api/v1/users/{id}/delete", request);
            await ThrowIfRejectedAsync(response);
        }

        public async Task DeleteProjectsAsync(List<Guid> projectIds, string? reason = null)
        {
            var request = new DeleteProjectsRequest { ProjectIds = projectIds, Reason = reason };
            var response = await _httpClient.PostAsJsonAsync("api/v1/dashboard/jobs/delete", request);
            await ThrowIfRejectedAsync(response);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Audit Logs (Implements IConnectProApiClient returning AuditLogsListDto)
        // ─────────────────────────────────────────────────────────────────────
        public async Task<AuditLogsListDto> GetAuditLogsAsync(string? q = null, int page = 1, int pageSize = 20)
        {
            try
            {
                var response = await _httpClient.GetAsync($"api/v1/audit?q={Uri.EscapeDataString(q ?? "")}&page={page}&pageSize={pageSize}");
                if (!response.IsSuccessStatusCode)
                {
                    return new AuditLogsListDto { Page = page, PageSize = pageSize };
                }

                var content = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(content))
                {
                    return new AuditLogsListDto { Page = page, PageSize = pageSize };
                }

                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;

                var result = new AuditLogsListDto
                {
                    Page = page,
                    PageSize = pageSize,
                    Query = q
                };

                if (root.TryGetProperty("page", out var pProp)) result.Page = pProp.GetInt32();
                if (root.TryGetProperty("pageSize", out var psProp)) result.PageSize = psProp.GetInt32();
                if (root.TryGetProperty("totalPages", out var tpProp)) result.TotalPages = tpProp.GetInt32();
                if (root.TryGetProperty("totalCount", out var tcProp)) result.TotalCount = tcProp.GetInt32();

                JsonElement arrayElement;
                bool hasArray = false;

                if (root.ValueKind == JsonValueKind.Array)
                {
                    arrayElement = root;
                    hasArray = true;
                }
                else if (root.TryGetProperty("items", out arrayElement) ||
                         root.TryGetProperty("Items", out arrayElement) ||
                         root.TryGetProperty("auditLogs", out arrayElement))
                {
                    hasArray = arrayElement.ValueKind == JsonValueKind.Array;
                }

                if (hasArray)
                {
                    foreach (var item in arrayElement.EnumerateArray())
                    {
                        var log = new AuditLogDto();

                        if (item.TryGetProperty("id", out var idProp) || item.TryGetProperty("Id", out idProp))
                        {
                            if (idProp.ValueKind == JsonValueKind.String && Guid.TryParse(idProp.GetString(), out var gid))
                            {
                                log.Id = gid;
                            }
                            else if (idProp.ValueKind == JsonValueKind.Number)
                            {
                                byte[] bytes = new byte[16];
                                BitConverter.GetBytes(idProp.GetInt64()).CopyTo(bytes, 0);
                                log.Id = new Guid(bytes);
                            }
                        }

                        if (item.TryGetProperty("actionType", out var actProp) || item.TryGetProperty("ActionType", out actProp) ||
                            item.TryGetProperty("action", out actProp) || item.TryGetProperty("Action", out actProp))
                        {
                            log.ActionType = actProp.GetString() ?? string.Empty;
                        }

                        if (item.TryGetProperty("performedBy", out var perfProp) || item.TryGetProperty("PerformedBy", out perfProp) ||
                            item.TryGetProperty("userName", out perfProp) || item.TryGetProperty("UserName", out perfProp))
                        {
                            log.PerformedBy = perfProp.GetString() ?? string.Empty;
                        }

                        if (item.TryGetProperty("targetUserId", out var targetProp) || item.TryGetProperty("TargetUserId", out targetProp) ||
                            item.TryGetProperty("userId", out targetProp) || item.TryGetProperty("UserId", out targetProp))
                        {
                            log.TargetUserId = targetProp.GetString() ?? string.Empty;
                        }

                        if (item.TryGetProperty("reason", out var rProp) || item.TryGetProperty("Reason", out rProp))
                            log.Reason = rProp.GetString();

                        if (item.TryGetProperty("details", out var dProp) || item.TryGetProperty("Details", out dProp))
                            log.Details = dProp.GetString();

                        if (item.TryGetProperty("timestamp", out var tProp) || item.TryGetProperty("Timestamp", out tProp))
                        {
                            if (tProp.TryGetDateTime(out var dt)) log.Timestamp = dt;
                        }

                        result.Items.Add(log);
                    }
                }

                if (result.TotalPages == 0 && result.Items.Count > 0)
                {
                    result.TotalPages = (int)Math.Ceiling((double)result.Items.Count / pageSize);
                }

                return result;
            }
            catch
            {
                return new AuditLogsListDto { Page = page, PageSize = pageSize, Query = q };
            }
        }

        public async Task<AuditLogsListDto> GetAuditLogsByUserIdAsync(string userId, int page = 1, int pageSize = 20)
        {
            return await GetAuditLogsAsync(userId, page, pageSize);
        }

        public async Task<FileContentResult> ExportAuditLogsAsync(string? q = null)
        {
            var response = await _httpClient.GetAsync($"api/v1/audit/export?q={q ?? ""}");
            response.EnsureSuccessStatusCode();
            var bytes = await response.Content.ReadAsByteArrayAsync();
            return new FileContentResult(bytes, "text/csv")
            {
                FileDownloadName = string.IsNullOrWhiteSpace(q)
                    ? "auditlogs.csv"
                    : $"auditlogs_filtered_{System.Net.WebUtility.UrlEncode(q)}.csv"
            };
        }

        public async Task<InvoiceDto> GetInvoice(Guid id)
        {
            var response = await _httpClient.GetAsync($"api/v1/invoices/{id}");
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<InvoiceDto>(_jsonOptions))!;
        }

        public async Task<FileContentResult> GetInvoiceHtml(Guid id)
        {
            var response = await _httpClient.GetAsync($"api/v1/invoices/{id}/html");
            response.EnsureSuccessStatusCode();
            var bytes = await response.Content.ReadAsByteArrayAsync();
            return new FileContentResult(bytes, "text/html")
            {
                FileDownloadName = $"invoice_{id}.html"
            };
        }

        public async Task<byte[]> GetInvoicePdfAsync(Guid id)
        {
            var response = await _httpClient.GetAsync($"api/v1/invoices/{id}/pdf");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync();
        }

        public async Task<UbuhlebethuConnectPro.Core.Models.DTOs.ManagerDashboardViewModel> GetDashboardStats()
        {
            var response = await _httpClient.GetAsync("api/v1/dashboard/stats");
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<UbuhlebethuConnectPro.Core.Models.DTOs.ManagerDashboardViewModel>(_jsonOptions))!;
        }

        public async Task<UserRolesDto> GetUserRolesAsync(string id)
        {
            var response = await _httpClient.GetAsync($"api/v1/users/{id}/roles");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<UserRolesDto>(_jsonOptions)
                ?? new UserRolesDto();
        }

        public async Task UpdateUserRolesAsync(string id, UpdateUserRolesRequest request)
        {
            var response = await _httpClient.PostAsJsonAsync($"api/v1/users/{id}/roles", request);
            await ThrowIfRejectedAsync(response);
        }

        public async Task<List<Job>> GetJobsAsync()
        {
            var response = await _httpClient.GetAsync("api/v1/dashboard/jobs");
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<List<Job>>(_jsonOptions)) ?? new List<Job>();
        }

        public async Task<Job> UpdateJobAsync(Job job)
        {
            var response = await _httpClient.PostAsJsonAsync($"api/v1/dashboard/jobs/{job.Id}", job);
            await ThrowIfRejectedAsync(response);
            return (await response.Content.ReadFromJsonAsync<Job>(_jsonOptions)) ?? job;
        }

        public async Task<Job> PayMilestoneAsync(Guid jobId, int? milestone = null)
        {
            var response = await _httpClient.PostAsJsonAsync($"api/v1/dashboard/jobs/{jobId}/milestone-payment", new { Milestone = milestone });
            await ThrowIfRejectedAsync(response);
            var result = await response.Content.ReadFromJsonAsync<MilestonePaymentResultDto>(_jsonOptions);
            return result?.Job ?? (await GetJobsAsync()).FirstOrDefault(j => j.Id == jobId)!;
        }

        public async Task<List<ProjectActivityDto>> GetJobActivitiesAsync(Guid jobId)
        {
            var response = await _httpClient.GetAsync($"api/v1/dashboard/jobs/{jobId}/activities");
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<List<ProjectActivityDto>>(_jsonOptions)) ?? new List<ProjectActivityDto>();
        }

        public async Task<ProjectActivityDto> AddJobActivityAsync(Guid jobId, ProjectActivityRequest request)
        {
            var response = await _httpClient.PostAsJsonAsync($"api/v1/dashboard/jobs/{jobId}/activities", request);
            await ThrowIfRejectedAsync(response);
            return (await response.Content.ReadFromJsonAsync<ProjectActivityDto>(_jsonOptions)) ?? new ProjectActivityDto();
        }

        public async Task<ProjectFileDto?> GetJobFileAsync(string activityId)
        {
            var response = await _httpClient.GetAsync($"api/v1/dashboard/activities/{Uri.EscapeDataString(activityId)}/file");
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ProjectFileDto>(_jsonOptions);
        }

        public async Task SetJobActivityStatusAsync(Guid jobId, string activityId, string status)
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"api/v1/dashboard/jobs/{jobId}/activities/{Uri.EscapeDataString(activityId)}/status", new { status });
            await ThrowIfRejectedAsync(response);
        }

        public async Task<List<SiteReportDto>> GetSiteReportsAsync(Guid projectId)
        {
            var response = await _httpClient.GetAsync($"api/v1/sitereports/project/{projectId}");
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<List<SiteReportDto>>(_jsonOptions)) ?? new List<SiteReportDto>();
        }

        public async Task<(Stream Stream, string ContentType)?> GetSiteReportPhotoAsync(Guid reportId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"api/v1/sitereports/{reportId}/photo");
                if (!response.IsSuccessStatusCode)
                    return null;

                var stream = await response.Content.ReadAsStreamAsync();
                var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
                return (stream, contentType);
            }
            catch
            {
                return null;
            }
        }

        public async Task<List<AttendanceDto>> GetAttendanceAsync(Guid projectId, DateTime? date = null)
        {
            var day = (date ?? DateTime.Today).ToString("yyyy-MM-dd");
            var response = await _httpClient.GetAsync($"api/v1/attendance?projectId={projectId}&date={day}");
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<List<AttendanceDto>>(_jsonOptions)) ?? new List<AttendanceDto>();
        }

        public async Task<List<SupportTicketDto>> GetSupportTicketsAsync()
        {
            var response = await _httpClient.GetAsync("api/v1/support/tickets");
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<List<SupportTicketDto>>(_jsonOptions)) ?? new List<SupportTicketDto>();
        }

        public async Task<SupportTicketDto> CreateSupportTicketAsync(string subject, string description)
        {
            var response = await _httpClient.PostAsJsonAsync("api/v1/support/tickets", new { subject, description });
            await ThrowIfRejectedAsync(response);
            return (await response.Content.ReadFromJsonAsync<SupportTicketDto>(_jsonOptions))!;
        }

        public async Task<bool> RespondToSupportTicketAsync(Guid id, string response)
        {
            var payload = new { Response = response };
            var res = await _httpClient.PostAsJsonAsync($"api/v1/support/tickets/{id}/respond", payload);
            return res.IsSuccessStatusCode;
        }

        private static async Task ThrowIfRejectedAsync(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode) return;
            if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                try
                {
                    var content = await response.Content.ReadAsStringAsync();
                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(content);
                        if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object && doc.RootElement.TryGetProperty("error", out var e) && e.GetString() is { Length: > 0 } msg)
                            throw new ApiRejectedException(msg);
                        if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.String && doc.RootElement.GetString() is { Length: > 0 } str)
                            throw new ApiRejectedException(str);
                        if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            var errors = doc.RootElement.EnumerateArray()
                                .Select(item => item.ValueKind == System.Text.Json.JsonValueKind.Object && item.TryGetProperty("description", out var d) ? d.GetString() : item.ToString())
                                .Where(s => !string.IsNullOrWhiteSpace(s));
                            var combined = string.Join("; ", errors);
                            if (!string.IsNullOrWhiteSpace(combined)) throw new ApiRejectedException(combined);
                        }
                    }
                }
                catch (System.Text.Json.JsonException) { }
            }
            response.EnsureSuccessStatusCode();
        }

        public async Task<List<QuoteRequest>> GetClientQuotesAsync(string username, DateTime? from, DateTime? to, string? status)
        {
            var query = new List<string>();

            if (from.HasValue)
                query.Add($"from={from.Value:yyyy-MM-dd}");

            if (to.HasValue)
                query.Add($"to={to.Value:yyyy-MM-dd}");

            if (!string.IsNullOrWhiteSpace(status))
                query.Add($"status={Uri.EscapeDataString(status)}");

            var url = $"api/v1/quotes/client/{Uri.EscapeDataString(username)}";

            if (query.Count > 0)
                url += "?" + string.Join("&", query);

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<List<QuoteRequest>>(_jsonOptions)) ?? new List<QuoteRequest>();
        }

        public async Task<List<NotificationDto>> GetUnreadNotificationsAsync(string username)
        {
            var response = await _httpClient.GetAsync($"api/v1/notifications/unread/{username}");
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<List<NotificationDto>>(_jsonOptions)) ?? new List<NotificationDto>();
        }

        public async Task MarkNotificationReadAsync(Guid id)
        {
            var response = await _httpClient.PostAsJsonAsync($"api/v1/notifications/{id}/read", new { });
            response.EnsureSuccessStatusCode();
        }

        public async Task SendToManagerAsync(Guid id, decimal? estimatedBudget)
        {
            var response = await _httpClient.PostAsJsonAsync($"api/v1/office/send-to-manager/{id}", new { EstimatedBudget = estimatedBudget });
            response.EnsureSuccessStatusCode();
        }

        public async Task ManagerAcceptQuoteAsync(Guid id)
        {
            var response = await _httpClient.PostAsJsonAsync($"api/v1/manager/accept-quote/{id}", new { });
            response.EnsureSuccessStatusCode();
        }

        public async Task ManagerRejectQuoteAsync(Guid id, string reason)
        {
            var response = await _httpClient.PostAsJsonAsync($"api/v1/manager/reject-quote/{id}", new { Reason = reason });
            response.EnsureSuccessStatusCode();
        }

        public async Task SendFeedbackAsync(Guid id, string feedback)
        {
            var response = await _httpClient.PostAsJsonAsync($"api/v1/office/send-feedback/{id}", new { Feedback = feedback });
            response.EnsureSuccessStatusCode();
        }

        public async Task<UbuhlebethuConnectPro.WebApp.ApiClient.ChatAskResponseDto?> ChatAskAsync(string question, string? provider = null)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/v1/chat/ask", new { Question = question, Provider = provider });
                if (!response.IsSuccessStatusCode) return null;
                return await response.Content.ReadFromJsonAsync<UbuhlebethuConnectPro.WebApp.ApiClient.ChatAskResponseDto>(_jsonOptions);
            }
            catch
            {
                return null;
            }
        }
        public async Task<UbuhlebethuConnectPro.WebApp.ApiClient.ConversationResponseDto?> ConversationAsync(UbuhlebethuConnectPro.WebApp.ApiClient.ConversationRequestDto request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/v1/chat/conversation", request);
                if (!response.IsSuccessStatusCode) return null;
                return await response.Content.ReadFromJsonAsync<UbuhlebethuConnectPro.WebApp.ApiClient.ConversationResponseDto>(_jsonOptions);
            }
            catch
            {
                return null;
            }
        }


        public async Task<UbuhlebethuConnectPro.WebApp.Models.DTOs.SystemHealthViewModel?> GetHealthCheckAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("healthz");
                return await response.Content.ReadFromJsonAsync<UbuhlebethuConnectPro.WebApp.Models.DTOs.SystemHealthViewModel>(_jsonOptions);
            }
            catch
            {
                return new UbuhlebethuConnectPro.WebApp.Models.DTOs.SystemHealthViewModel
                {
                    Status = "Unhealthy",
                    TotalDuration = "N/A",
                    CheckedAt = DateTime.UtcNow,
                    System = new UbuhlebethuConnectPro.WebApp.Models.DTOs.SystemInfoViewModel
                    {
                        Server = "UbuhleBethu-Api (Unreachable)",
                        Environment = "Unavailable",
                        Framework = ".NET 10.0",
                        Os = "Unknown",
                        Uptime = "Unavailable"
                    },
                    Entries = new Dictionary<string, UbuhlebethuConnectPro.WebApp.Models.DTOs.HealthEntryViewModel>
                    {
                        {
                            "connectivity",
                            new UbuhlebethuConnectPro.WebApp.Models.DTOs.HealthEntryViewModel
                            {
                                Status = "Unhealthy",
                                Description = "API host is unreachable or not responding to health probes.",
                                Duration = "0.0ms"
                            }
                        }
                    }
                };
            }
        }
    }
}
