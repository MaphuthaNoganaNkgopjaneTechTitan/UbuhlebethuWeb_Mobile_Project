using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UbuhlebethuConnectPro.Core.Options;
using UbuhlebethuConnectPro.Core.Services;

namespace UbuhlebethuConnectPro.Infrastructure.Services
{
    public class LocalStorageService : IStorageService
    {
        private readonly IWebHostEnvironment _env;
        private readonly StorageOptions _options;
        private readonly ILogger<LocalStorageService> _logger;

        public LocalStorageService(
            IWebHostEnvironment env,
            IOptions<StorageOptions> options,
            ILogger<LocalStorageService> logger)
        {
            _env = env;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<string> UploadAsync(Stream stream, string fileName, string contentType, string folder, CancellationToken ct = default)
        {
            var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
            var uploadsDir = Path.Combine(webRoot, "uploads", folder);
            Directory.CreateDirectory(uploadsDir);

            var safeFileName = $"{Guid.NewGuid():N}_{Path.GetFileName(fileName)}";
            var filePath = Path.Combine(uploadsDir, safeFileName);

            await using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                if (stream.CanSeek)
                    stream.Position = 0;
                await stream.CopyToAsync(fs, ct);
            }

            var relativePath = $"/uploads/{folder}/{safeFileName}";
            _logger.LogInformation("Saved photo locally to {Path}", relativePath);
            return relativePath;
        }

        public Task<Stream?> GetAsync(string keyOrPath, CancellationToken ct = default)
        {
            var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
            var cleanPath = keyOrPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.Combine(webRoot, cleanPath);

            if (File.Exists(fullPath))
            {
                var fi = new FileInfo(fullPath);
                if (fi.Length > 0)
                {
                    return Task.FromResult<Stream?>(File.OpenRead(fullPath));
                }
            }

            return Task.FromResult<Stream?>(null);
        }

        public string GetPublicOrPresignedUrl(string keyOrPath, TimeSpan? expiry = null)
        {
            return keyOrPath;
        }

        public Task DeleteAsync(string keyOrPath, CancellationToken ct = default)
        {
            try
            {
                var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
                var cleanPath = keyOrPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                var fullPath = Path.Combine(webRoot, cleanPath);
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete local file {Path}", keyOrPath);
            }

            return Task.CompletedTask;
        }
    }
}
