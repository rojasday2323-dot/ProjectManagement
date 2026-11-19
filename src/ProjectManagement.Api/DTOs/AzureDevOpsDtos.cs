namespace ProjectManagement.Api.DTOs;

/// <summary>Connection information for the configured Azure DevOps organization.</summary>
public sealed record AzureDevOpsStatusResponse(
    string OrganizationUrl,
    bool Connected,
    int VisibleProjectCount);

/// <summary>A project visible to the authenticated Azure DevOps user.</summary>
public sealed record AzureDevOpsProjectResponse(
    Guid Id,
    string Name,
    string State,
    string? Visibility);

/// <summary>A work item returned from Azure DevOps.</summary>
public sealed record AzureDevOpsWorkItemResponse(
    int Id,
    string Title,
    string WorkItemType,
    string? State,
    string? AssignedTo,
    DateTimeOffset? StartDate,
    DateTimeOffset? TargetDate,
    DateTimeOffset? CreatedDate,
    DateTimeOffset? ChangedDate,
    string? Url,
    string ProjectName,
    int? LocalProjectId,
    string? LocalProjectName,
    int? CommitmentId,
    string? CommitmentTitle);

/// <summary>Details for a work item, including existing local project or commitment links.</summary>
public sealed record AzureDevOpsWorkItemDetailResponse(
    AzureDevOpsWorkItemResponse WorkItem,
    IReadOnlyDictionary<string, object?> Fields);
