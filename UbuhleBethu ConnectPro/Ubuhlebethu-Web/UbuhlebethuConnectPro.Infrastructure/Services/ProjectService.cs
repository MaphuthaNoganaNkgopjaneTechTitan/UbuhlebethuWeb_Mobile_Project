using System;
using System.Threading.Tasks;
using UbuhlebethuConnectPro.Core.Models;
using UbuhlebethuConnectPro.Core.Repositories;
using UbuhlebethuConnectPro.Core.Services;

namespace UbuhlebethuConnectPro.Infrastructure.Services
{
    public class ProjectService : IProjectService
    {
        private readonly IProjectRepository _projectRepo;
        private readonly IQuotationRepository _quoteRepo;

        public ProjectService(IProjectRepository projectRepo, IQuotationRepository quoteRepo)
        {
            _projectRepo = projectRepo;
            _quoteRepo = quoteRepo;
        }

       public async Task<Project> CreateProjectFromQuoteAsync(Guid quoteId, string managerUsername)
       {
           var quote = await _quoteRepo.GetByIdAsync(quoteId);
           if (quote == null) throw new System.Collections.Generic.KeyNotFoundException("Quote not found.");

           var project = new Project
           {
               Id = Guid.NewGuid(),
               QuoteRequestId = quote.Id,
               Title = quote.ProjectDescription ?? "New Project",
               StartDate = DateTime.UtcNow,
               Status = "Active",
               SiteManagerName = managerUsername,
               ClientName = quote.ClientName ?? "Unknown",
               ClientUserName = quote.CreatedBy ?? "Unknown",
               QuoteReference = quote.Reference ?? ""
           };

           await _projectRepo.AddAsync(project);
           await _projectRepo.SaveChangesAsync();

           return project;
       }

        public async Task UpdateProjectStatusAsync(Guid projectId, string newStatus)
        {
            var project = await _projectRepo.GetByIdAsync(projectId);
            if (project == null) throw new System.Collections.Generic.KeyNotFoundException("Project not found.");

            project.Status = newStatus;
            await _projectRepo.SaveChangesAsync();
        }
    }
}
