using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using UbuhlebethuConnectPro.Core.Models;
using UbuhlebethuConnectPro.Core.Models.DTOs;
using UbuhlebethuConnectPro.WebApp.Models.DTOs;

namespace UbuhlebethuConnectPro.WebApp.ApiClient
{
    // ─────────────────────────────────────────────────────────────────────────
    // DTOs used by the API client
    // ─────────────────────────────────────────────────────────────────────────

    public class UserDto
    {
        public string Id { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public IList<string> Roles { get; set; } = new List<string>();
        public bool IsLockedOut { get; set; }
        public DateTimeOffset? LockoutEnd { get; set; }
    }

    public class UserRolesDto
    {
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public IList<string> CurrentRoles { get; set; } = new List<string>();
        public IList<string> AvailableRoles { get; set; } = new List<string>();
    }

    public class SupportTicketDto
    {
        public Guid Id { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? Response { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class AttendanceDto
    {
        public int Id { get; set; }
        public string WorkerName { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public bool IsPresent { get; set; }
        public double DailyRate { get; set; }
        public string CheckInTime { get; set; } = string.Empty;
        public string CheckOutTime { get; set; } = string.Empty;
        public bool IsOffSite { get; set; }
        public double? DistanceFromSiteMeters { get; set; }
    }

    public class SiteReportDto
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public string ReportedBy { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int ProgressPercent { get; set; }
        public string ConstructionStage { get; set; } = string.Empty;
        public string? PhotoPath { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public DateTime LoggedAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool IsOffSite { get; set; }
        public double? DistanceFromSiteMeters { get; set; }
    }

    public class AuditLogDto
    {
        public Guid Id { get; set; }
        public bool IsDeleted { get; set; }
        public string ActionType { get; set; } = string.Empty;
        public string PerformedBy { get; set; } = string.Empty;
        public string TargetUserId { get; set; } = string.Empty;
        public string? Reason { get; set; }
        public string? Details { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class AuditLogsListDto
    {
        public IList<AuditLogDto> Items { get; set; } = new List<AuditLogDto>();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public int TotalCount { get; set; }
        public string? Query { get; set; }
    }

    public class DeactivateRequest
    {
        public string? Reason { get; set; }
    }

    public class ReactivateRequest
    {
        public string? Reason { get; set; }
    }

    public class DeleteUserRequest
    {
        public string Reason { get; set; } = string.Empty;
    }

    public class DeleteProjectsRequest
    {
        public List<Guid> ProjectIds { get; set; } = new List<Guid>();
        public string? Reason { get; set; }
    }

    /// <summary>Minimal stub for QuoteDto returned by the API.</summary>
    public class QuoteDto
    {
        public Guid Id { get; set; }
        public string Reference { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string ClientName { get; set; } = string.Empty;
        public string ProjectType { get; set; } = string.Empty;
        public string SiteLocation { get; set; } = string.Empty;
        public string? ProjectDescription { get; set; }
        public decimal EstimatedBudget { get; set; }
        public DateTime RequestDate { get; set; }
        public bool IsPaid { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
        public Guid? InvoiceId { get; set; }
        public string? Company { get; set; }
        public string? ReferenceProject { get; set; }
        public bool HasDocument { get; set; }
    }

    public class ProposalStatusDto
    {
        public string Reference { get; set; } = string.Empty;
        public string ProjectOfInterest { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime SubmittedAt { get; set; }
        public string? Note { get; set; }
    }

    /// <summary>Minimal stub for creating a quote via the API.</summary>
    public class QuoteDtoForCreation
    {
        public string ClientName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string ProjectType { get; set; } = string.Empty;
        public string SiteLocation { get; set; } = string.Empty;
        public string? ProjectDescription { get; set; }
        public decimal EstimatedBudget { get; set; }
        public decimal Budget { get; set; }
    }

    /// <summary>Invoice DTO returned by the API for preview/download.</summary>
    public class InvoiceDto
    {
        public Guid Id { get; set; }
        public Guid QuoteRequestId { get; set; }
        public decimal Amount { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsPaid { get; set; }
        public string QuoteProjectDescription { get; set; } = string.Empty;
        public string QuoteClientName { get; set; } = string.Empty;
        public string QuoteReference { get; set; } = string.Empty;
        public string QuoteSiteLocation { get; set; } = string.Empty;
        public string QuoteProjectType { get; set; } = string.Empty;
    }

    public class NotificationDto
    {
        public Guid Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class UpdateUserRolesRequest
    {
        public List<string> Roles { get; set; } = new List<string>();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Request & Response wrappers
    // ─────────────────────────────────────────────────────────────────────────

    public class CreateUserRequest
    {
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public List<string> Roles { get; set; } = new();
    }

    public class UpdateUserRequest
    {
        public string? UserName { get; set; }
        public string? Email { get; set; }
    }

    public class ResetPasswordRequest
    {
        public string? Reason { get; set; }
    }

    public class ChangePasswordRequest
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }

    public class MessageResponse
    {
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>A saved project item: daily log, milestone, purchase order, document or client contact.</summary>
    public class ProjectActivityDto
    {
        public string Id { get; set; } = string.Empty;
        public string ProjectId { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Body { get; set; }
        public string? Amount { get; set; }
        public string? DueDate { get; set; }
        public string? Status { get; set; }
        public string? FileName { get; set; }
        public string? ContentType { get; set; }
        public string? CreatedBy { get; set; }
        public string CreatedAt { get; set; } = string.Empty;
    }

    public class ProjectActivityRequest
    {
        public string Kind { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Body { get; set; }
        public string? Amount { get; set; }
        public string? DueDate { get; set; }
        public string? Status { get; set; }
        public string? FileName { get; set; }
        public string? ContentType { get; set; }
        public string? ContentBase64 { get; set; }
    }

    public class ProjectFileDto
    {
        public string ProjectId { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = "application/octet-stream";
        public string ContentBase64 { get; set; } = string.Empty;
    }

    public class MilestonePaymentResultDto
    {
        public bool Ok { get; set; }
        public string? Message { get; set; }
        public int Milestone { get; set; }
        public decimal AmountPaid { get; set; }
        public decimal TotalInvoiced { get; set; }
        public Job? Job { get; set; }
    }

    /// <summary>Raised when the API rejects a request with a readable message.</summary>
    public class ApiRejectedException : Exception
    {
        public ApiRejectedException(string message) : base(message) { }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Interface Definition
    // ─────────────────────────────────────────────────────────────────────────

    public interface IConnectProApiClient
    {
        Task<LoginResponse?> LoginAsync(LoginRequest request);
        Task<LoginResponse?> RegisterAsync(RegisterRequest request);
        Task<RefreshResponse> RefreshAsync(RefreshRequest request);
        Task<List<QuoteDto>> GetQuotesAsync();
        Task<QuoteDto> GetQuoteAsync(Guid id);
        Task<QuoteDto> CreateQuoteAsync(QuoteDtoForCreation quoteDto);
        Task<QuoteDto?> TrackProposalAsync(string reference, string email);
        Task<List<QuoteDto>> GetPendingQuotesAsync();
        Task<QuoteDto> GenerateDraftEstimateAsync(Guid id);
        Task<QuoteDto> ApproveQuoteAsync(Guid id);
        Task<string> SendInvoiceAsync(Guid id);
        Task PayQuoteAsync(Guid id);
        Task RejectQuoteAsync(Guid id, string? reason = null);
        Task<List<UserDto>> GetUsersAsync(string? search = null, string? role = null, int page = 1, int pageSize = 20);
        Task<UserDto> GetUserAsync(string id);
        Task<UserDto> CreateUserAsync(CreateUserRequest request);
        Task<List<string>> GetAvailableRolesAsync();
        Task<UserDto> UpdateUserAsync(string id, UpdateUserRequest request);
        Task<string> ResetPasswordAsync(string id, ResetPasswordRequest request);
        Task ChangePasswordAsync(string id, ChangePasswordRequest request);
        Task DeactivateUserAsync(string id, DeactivateRequest request);
        Task ReactivateUserAsync(string id, ReactivateRequest request);
        Task HardDeleteUserAsync(string id, DeleteUserRequest request);
        Task DeleteProjectsAsync(List<Guid> projectIds, string? reason = null);
        Task<AuditLogsListDto> GetAuditLogsAsync(string? q = null, int page = 1, int pageSize = 20);
        Task<AuditLogsListDto> GetAuditLogsByUserIdAsync(string userId, int page = 1, int pageSize = 20);
        Task<FileContentResult> ExportAuditLogsAsync(string? q = null);
        Task<InvoiceDto> GetInvoice(Guid id);
        Task<FileContentResult> GetInvoiceHtml(Guid id);
        Task<byte[]> GetInvoicePdfAsync(Guid id);
        Task<UbuhlebethuConnectPro.Core.Models.DTOs.ManagerDashboardViewModel> GetDashboardStats();

        // Methods for frontend dashboard & roles
        Task<UserRolesDto> GetUserRolesAsync(string id);
        Task UpdateUserRolesAsync(string id, UpdateUserRolesRequest request);
        Task<List<Job>> GetJobsAsync();
        Task<Job> UpdateJobAsync(Job job);
        Task<Job> PayMilestoneAsync(Guid jobId, int? milestone = null);
        Task<List<ProjectActivityDto>> GetJobActivitiesAsync(Guid jobId);
        Task<ProjectActivityDto> AddJobActivityAsync(Guid jobId, ProjectActivityRequest request);
        Task<ProjectFileDto?> GetJobFileAsync(string activityId);
        Task SetJobActivityStatusAsync(Guid jobId, string activityId, string status);
        Task<List<SiteReportDto>> GetSiteReportsAsync(Guid projectId);
        Task<(Stream Stream, string ContentType)?> GetSiteReportPhotoAsync(Guid reportId);
        Task<List<AttendanceDto>> GetAttendanceAsync(Guid projectId, DateTime? date = null);
        Task<List<SupportTicketDto>> GetSupportTicketsAsync();
        Task<SupportTicketDto> CreateSupportTicketAsync(string subject, string description);
        Task<bool> RespondToSupportTicketAsync(Guid id, string response);
        Task<List<QuoteRequest>> GetClientQuotesAsync(string username, DateTime? from, DateTime? to, string? status);
        Task<List<NotificationDto>> GetUnreadNotificationsAsync(string username);
        Task MarkNotificationReadAsync(Guid id);

        // Manager & Office review workflow methods
        Task SendToManagerAsync(Guid id, decimal? estimatedBudget);
        Task ManagerAcceptQuoteAsync(Guid id);
        Task ManagerRejectQuoteAsync(Guid id, string reason);
        Task SendFeedbackAsync(Guid id, string feedback);
        Task<UbuhlebethuConnectPro.WebApp.Models.DTOs.SystemHealthViewModel?> GetHealthCheckAsync();
        Task<UbuhlebethuConnectPro.WebApp.ApiClient.ChatAskResponseDto?> ChatAskAsync(string question, string? provider = null);
        Task<UbuhlebethuConnectPro.WebApp.ApiClient.ConversationResponseDto?> ConversationAsync(UbuhlebethuConnectPro.WebApp.ApiClient.ConversationRequestDto request);
    }
    public class ChatAskResponseDto
    {
        public string? Answer { get; set; }
        public string? Suggestion { get; set; }
        public bool Fallback { get; set; }
    }
    public class ConversationMessageDto
    {
        public string Role { get; set; } = "user";
        public string Content { get; set; } = string.Empty;
    }
    public class ConversationRequestDto
    {
        public List<ConversationMessageDto> Messages { get; set; } = new();
        public Guid? ProjectId { get; set; }
        public string? Provider { get; set; }
    }
    public class ConversationResponseDto
    {
        public string Answer { get; set; } = string.Empty;
        public bool UsedFallback { get; set; }
        public string Source { get; set; } = string.Empty;
        public List<string> ToolsUsed { get; set; } = new();
    }
}
