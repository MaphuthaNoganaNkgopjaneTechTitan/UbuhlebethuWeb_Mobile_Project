using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UbuhlebethuConnectPro.Core.Options;
using UbuhlebethuConnectPro.Core.Services;

namespace UbuhlebethuConnectPro.Infrastructure.Services
{
    public class S3StorageService : IStorageService
    {
        private readonly IAmazonS3 _s3Client;
        private readonly StorageOptions _options;
        private readonly LocalStorageService _localFallback;
        private readonly ILogger<S3StorageService> _logger;

        public S3StorageService(
            IAmazonS3 s3Client,
            IOptions<StorageOptions> options,
            LocalStorageService localFallback,
            ILogger<S3StorageService> logger)
        {
            _s3Client = s3Client;
            _options = options.Value;
            _localFallback = localFallback;
            _logger = logger;
        }

        public async Task<string> UploadAsync(Stream stream, string fileName, string contentType, string folder, CancellationToken ct = default)
        {
            var key = $"{folder.Trim('/')}/{Guid.NewGuid():N}_{Path.GetFileName(fileName)}";

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            try
            {
                if (stream.CanSeek)
                    stream.Position = 0;

                var putRequest = new PutObjectRequest
                {
                    BucketName = _options.Bucket,
                    Key = key,
                    InputStream = stream,
                    ContentType = string.IsNullOrWhiteSpace(contentType) ? "image/jpeg" : contentType
                };

                await _s3Client.PutObjectAsync(putRequest, linkedCts.Token);
                _logger.LogInformation("Successfully uploaded {Key} to S3 / Backblaze B2 bucket {Bucket}", key, _options.Bucket);

                // Generate public or presigned URL
                return GetPublicOrPresignedUrl(key, TimeSpan.FromDays(7));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to upload to S3 / Backblaze B2 bucket {Bucket}. Falling back to local storage.", _options.Bucket);
                if (stream.CanSeek)
                    stream.Position = 0;
                return await _localFallback.UploadAsync(stream, fileName, contentType, folder, ct);
            }
        }

        public async Task<Stream?> GetAsync(string keyOrPath, CancellationToken ct = default)
        {
            if (keyOrPath.StartsWith("/uploads/") || !keyOrPath.Contains("/"))
            {
                return await _localFallback.GetAsync(keyOrPath, ct);
            }

            try
            {
                var key = ExtractKey(keyOrPath);
                var response = await _s3Client.GetObjectAsync(_options.Bucket, key, ct);
                return response.ResponseStream;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get object {Key} from S3. Trying local fallback.", keyOrPath);
                return await _localFallback.GetAsync(keyOrPath, ct);
            }
        }

        public string GetPublicOrPresignedUrl(string keyOrPath, TimeSpan? expiry = null)
        {
            if (string.IsNullOrWhiteSpace(keyOrPath))
                return string.Empty;

            if (keyOrPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                keyOrPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return keyOrPath;
            }

            if (keyOrPath.StartsWith("/uploads/"))
            {
                return keyOrPath;
            }

            try
            {
                var key = ExtractKey(keyOrPath);
                var expiryDuration = expiry ?? TimeSpan.FromHours(24);

                var request = new GetPreSignedUrlRequest
                {
                    BucketName = _options.Bucket,
                    Key = key,
                    Expires = DateTime.UtcNow.Add(expiryDuration)
                };

                return _s3Client.GetPreSignedURL(request);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate presigned URL for {Key}. Returning key as fallback.", keyOrPath);
                return keyOrPath;
            }
        }

        public async Task DeleteAsync(string keyOrPath, CancellationToken ct = default)
        {
            if (keyOrPath.StartsWith("/uploads/"))
            {
                await _localFallback.DeleteAsync(keyOrPath, ct);
                return;
            }

            try
            {
                var key = ExtractKey(keyOrPath);
                await _s3Client.DeleteObjectAsync(_options.Bucket, key, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete S3 object {Key}", keyOrPath);
            }
        }

        private string ExtractKey(string keyOrUrl)
        {
            if (keyOrUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                keyOrUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                var uri = new Uri(keyOrUrl);
                var path = uri.AbsolutePath.TrimStart('/');
                // If path style: bucket/key, strip bucket if present
                if (path.StartsWith(_options.Bucket + "/", StringComparison.OrdinalIgnoreCase))
                {
                    return path.Substring(_options.Bucket.Length + 1);
                }
                return path;
            }

            return keyOrUrl.TrimStart('/');
        }
    }
}
