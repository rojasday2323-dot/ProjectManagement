using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;

namespace ProjectManagement.Api.Services.AzureDevOps;

public interface IAzureDevOpsClient : IDisposable
{
    Task<IReadOnlyList<TeamProjectReference>> GetProjectsAsync(CancellationToken cancellationToken);
    Task<TeamProjectReference> GetProjectAsync(Guid projectId, CancellationToken cancellationToken);
    Task<IReadOnlyList<WorkItem>> GetProjectWorkItemsAsync(
        Guid projectId,
        int top,
        CancellationToken cancellationToken);
    Task<WorkItem> GetWorkItemAsync(int id, CancellationToken cancellationToken);
}

public interface IAzureDevOpsClientFactory
{
    IAzureDevOpsClient Create(Uri organizationUri, string accessToken);
}
