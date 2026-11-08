using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace UbuhlebethuConnectPro.Core.Services
{
    public interface IStorageService
    {
        Task<string> UploadAsync(Stream stream, string fileName, string contentType, string folder, CancellationToken ct = default);
        Task<Stream?> GetAsync(string keyOrPath, CancellationToken ct = default);
        string GetPublicOrPresignedUrl(string keyOrPath, TimeSpan? expiry = null);
        Task DeleteAsync(string keyOrPath, CancellationToken ct = default);
    }
}
