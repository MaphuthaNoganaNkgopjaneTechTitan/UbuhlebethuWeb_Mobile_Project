using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using UbuhlebethuConnectPro.Core.Models;
using UbuhlebethuConnectPro.Core.Repositories;
using UbuhlebethuConnectPro.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace UbuhlebethuConnectPro.Infrastructure.Services
{
    public class SiteReportService : ISiteReportService
    {
        private readonly ISiteReportRepository _reports;
        private readonly IProjectRepository _projects;
        private readonly IWebHostEnvironment _env;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IStorageService _storageService;

        public SiteReportService(ISiteReportRepository reports, IProjectRepository projects, IWebHostEnvironment env, IServiceScopeFactory scopeFactory, IStorageService storageService)
        {
            _reports = reports;
            _projects = projects;
            _env = env;
            _scopeFactory = scopeFactory;
            _storageService = storageService;
        }

        public static double CalculateDistanceMeters(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371000.0; // Earth's radius in meters
            var dLat = (lat2 - lat1) * Math.PI / 180.0;
            var dLon = (lon2 - lon1) * Math.PI / 180.0;
            var rLat1 = lat1 * Math.PI / 180.0;
            var rLat2 = lat2 * Math.PI / 180.0;

            var a = Math.Sin(dLat / 2.0) * Math.Sin(dLat / 2.0) +
                    Math.Cos(rLat1) * Math.Cos(rLat2) *
                    Math.Sin(dLon / 2.0) * Math.Sin(dLon / 2.0);
            var c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
            return R * c;
        }

        public async Task<SiteReport> CreateAsync(SiteReportSubmission submission,string reportedBy,UploadedPhoto? photo,CancellationToken ct = default,Guid? clientReportId = null)
        {
            var project = await _projects.GetByIdAsync(submission.ProjectId, ct);
            if (project == null) throw new KeyNotFoundException("Project not found.");

            if (clientReportId.HasValue)
            {
                if (clientReportId.Value == Guid.Empty)
                {
                    throw new ValidationFailedException(
                        "Client report ID cannot be empty."
                    );
                }

                var existing = await _reports.GetByIdAsync(
                    clientReportId.Value,
                    ct
                );

                if (existing != null)
                {
                    if (existing.IsDeleted)
                    {
                        throw new ValidationFailedException(
                            "This report ID belongs to a deleted report and cannot be reused."
                        );
                    }

                    // Reject IDs previously used by another user or for another report.
                    if (!string.Equals(
                            existing.ReportedBy,
                            reportedBy,
                            StringComparison.OrdinalIgnoreCase) ||
                        existing.ProjectId != submission.ProjectId ||
                        existing.Description != submission.Description ||
                        existing.ProgressPercent != submission.ProgressPercent ||
                        existing.Latitude != submission.Latitude ||
                        existing.Longitude != submission.Longitude ||
                        existing.ConstructionStage != (submission.Stage ?? "General"))
                    {
                        throw new ValidationFailedException(
                            "This report ID is already associated with a different submission."
                        );
                    }

                    return existing;
                }
            }

            string? photoPath = null;
            if (photo != null)
            {
                var ext = Path.GetExtension(photo.FileName).ToLowerInvariant();
                if (ext != ".jpg" && ext != ".jpeg" && ext != ".png" && ext != ".webp")
                    throw new ArgumentException("Invalid photo extension. Only images are allowed.");

                var contentType = ext switch
                {
                    ".png" => "image/png",
                    ".webp" => "image/webp",
                    _ => "image/jpeg"
                };

                photoPath = await _storageService.UploadAsync(
                    photo.Stream,
                    photo.FileName,
                    contentType,
                    "sitereports",
                    ct
                );
            }

            double? distanceFromSite = null;
            bool isOffSite = false;

            if (submission.Latitude.HasValue && submission.Longitude.HasValue &&
                project.Latitude.HasValue && project.Longitude.HasValue)
            {
                var distance = CalculateDistanceMeters(
                    submission.Latitude.Value,
                    submission.Longitude.Value,
                    project.Latitude.Value,
                    project.Longitude.Value
                );
                distanceFromSite = Math.Round(distance, 1);
                var radius = project.GeofenceRadiusMeters > 0 ? project.GeofenceRadiusMeters : 300.0;
                isOffSite = distance > radius;
            }

            var report = new SiteReport
            {
                Id = clientReportId ?? Guid.NewGuid(),
                ProjectId = submission.ProjectId,
                ReportedBy = reportedBy ?? string.Empty,
                Description = submission.Description ?? string.Empty,
                ProgressPercent = submission.ProgressPercent,
                ConstructionStage = submission.Stage ?? "General",
                PhotoPath = photoPath,
                Latitude = submission.Latitude,
                Longitude = submission.Longitude,
                DistanceFromSiteMeters = distanceFromSite,
                IsOffSite = isOffSite,
                LoggedAt = DateTime.UtcNow,
                Status = "Submitted"
            };

            project.ProgressPercent = submission.ProgressPercent;

            await _reports.AddAsync(report, ct);

            try
            {
                await _reports.SaveChangesAsync(ct);
                await _projects.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (
                clientReportId.HasValue &&
                ex.InnerException is PostgresException pg &&
                pg.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                // Use a fresh scope so the failed insert is not
                // still tracked in the context used for this lookup.
                using var scope = _scopeFactory.CreateScope();

                var retryRepository = scope.ServiceProvider
                    .GetRequiredService<ISiteReportRepository>();

                var existing = await retryRepository.GetByIdAsync(
                    clientReportId.Value,
                    ct
                );

                // If no matching report exists, this was not a
                // recoverable duplicate of this submission.
                if (existing == null)
                {
                    throw;
                }

                if (existing.IsDeleted)
                {
                    throw new ValidationFailedException(
                        "This report ID belongs to a deleted report."
                    );
                }

                if (!string.Equals(
                        existing.ReportedBy,
                        reportedBy,
                        StringComparison.OrdinalIgnoreCase) ||
                    existing.ProjectId != submission.ProjectId ||
                    existing.Description != submission.Description ||
                    existing.ProgressPercent != submission.ProgressPercent ||
                    existing.Latitude != submission.Latitude ||
                    existing.Longitude != submission.Longitude ||
                    existing.ConstructionStage != (submission.Stage ?? "General"))
                {
                    throw new ValidationFailedException(
                        "This report ID belongs to a different submission."
                    );
                }

                // A matching report was already saved.
                // Return it rather than inserting another record.
                return existing;
            }

            return report;
        }
    }
}
