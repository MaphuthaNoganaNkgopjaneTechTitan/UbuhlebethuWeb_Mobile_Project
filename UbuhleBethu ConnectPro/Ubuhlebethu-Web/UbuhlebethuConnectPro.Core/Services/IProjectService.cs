using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UbuhlebethuConnectPro.Core.Models;

namespace UbuhlebethuConnectPro.Core.Services
{
    public interface IProjectService
    {
        Task<Project> CreateProjectFromQuoteAsync(Guid quoteId, string managerUsername);
        Task UpdateProjectStatusAsync(Guid projectId, string newStatus);
    }
}
