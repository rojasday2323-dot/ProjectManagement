using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.OAuth;
using Microsoft.VisualStudio.Services.WebApi;

namespace ProjectManagement.Api.Services.AzureDevOps;

public sealed class AzureDevOpsClientFactory : IAzureDevOpsClientFactory
{
    public IAzureDevOpsClient Create(Uri organizationUri, string accessToken)
    {
        var connection = new VssConnection(
            organizationUri,
            new VssOAuthAccessTokenCredential(accessToken));
        return new AzureDevOpsSdkClient(connection);
    }

    private sealed class AzureDevOpsSdkClient(VssConnection connection) : IAzureDevOpsClient
    {
        public async Task<IReadOnlyList<TeamProjectReference>> GetProjectsAsync(
            CancellationToken cancellationToken)
        {
            var client = await connection.GetClientAsync<ProjectHttpClient>(cancellationToken);
            const int pageSize = 1000;
            var projects = new List<TeamProjectReference>();
            var skip = 0;
            while (true)
            {
                var page = await client.GetProjects(top: pageSize, skip: skip).WaitAsync(cancellationToken);
                projects.AddRange(page);
                if (page.Count < pageSize)
                    return projects;

                skip += page.Count;
            }
        }

        public async Task<TeamProjectReference> GetProjectAsync(
            Guid projectId,
            CancellationToken cancellationToken)
        {
            var client = await connection.GetClientAsync<ProjectHttpClient>(cancellationToken);
            return await client.GetProject(projectId.ToString()).WaitAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<WorkItem>> GetProjectWorkItemsAsync(
            Guid projectId,
            int top,
            CancellationToken cancellationToken)
        {
            var client = await connection.GetClientAsync<WorkItemTrackingHttpClient>(cancellationToken);
            var query = new Wiql
            {
                Query = "SELECT [System.Id] FROM WorkItems ORDER BY [System.ChangedDate] DESC"
            };
            var result = await client.QueryByWiqlAsync(
                query,
                projectId,
                top: top,
                cancellationToken: cancellationToken);
            var ids = result.WorkItems.Select(item => item.Id).ToArray();
            if (ids.Length == 0)
                return [];

            var fields = AzureDevOpsService.WorkItemFields;
            return await client.GetWorkItemsAsync(
                ids,
                fields,
                expand: WorkItemExpand.None,
                errorPolicy: WorkItemErrorPolicy.Omit,
                cancellationToken: cancellationToken);
        }

        public async Task<WorkItem> GetWorkItemAsync(int id, CancellationToken cancellationToken)
        {
            var client = await connection.GetClientAsync<WorkItemTrackingHttpClient>(cancellationToken);
            return await client.GetWorkItemAsync(
                id,
                AzureDevOpsService.WorkItemFields,
                expand: WorkItemExpand.None,
                cancellationToken: cancellationToken);
        }

        public void Dispose() => connection.Dispose();
    }
}
