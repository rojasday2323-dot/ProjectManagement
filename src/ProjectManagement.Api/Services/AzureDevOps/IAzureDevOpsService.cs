using ProjectManagement.Api.DTOs;

namespace ProjectManagement.Api.Services.AzureDevOps;

public interface IAzureDevOpsService
{
    Task<AzureDevOpsStatusResponse> GetStatusAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<AzureDevOpsProjectResponse>> GetProjectsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<AzureDevOpsWorkItemResponse>> GetProjectWorkItemsAsync(
        Guid projectId,
        int top,
        CancellationToken cancellationToken);
    Task<AzureDevOpsWorkItemDetailResponse> GetWorkItemAsync(
        int id,
        CancellationToken cancellationToken);
}
