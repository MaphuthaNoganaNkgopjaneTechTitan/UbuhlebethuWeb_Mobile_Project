using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UbuhlebethuConnectPro.Core.Models;

namespace UbuhlebethuConnectPro.Core.Services
{
    public record SiteReportSubmission(Guid ProjectId, string Description, int ProgressPercent, double? Latitude, double? Longitude, string? Stage);
    public record UploadedPhoto(Stream Stream, string FileName, long Length);

    public interface ISiteReportService
    {
        Task<SiteReport> CreateAsync(SiteReportSubmission submission, string reportedBy, UploadedPhoto? photo, CancellationToken ct = default, Guid? clientReportId = null);
    }
}
