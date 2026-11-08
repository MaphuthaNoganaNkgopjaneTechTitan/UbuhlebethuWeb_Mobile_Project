using System.Threading;
using System.Threading.Tasks;
using UbuhlebethuConnectPro.Core.Models;

namespace UbuhlebethuConnectPro.Core.Repositories
{
    public interface ISystemSettingsRepository
    {
        Task<SystemSettings> GetSettingsAsync(CancellationToken ct = default);
        Task UpdateSettingsAsync(SystemSettings settings, CancellationToken ct = default);
    }
}
