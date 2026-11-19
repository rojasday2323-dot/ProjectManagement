namespace ProjectManagement.Api.DTOs;

public record DeveloperResponse(
    int Id,
    int UserId,
    string DisplayName,
    string? Email,
    string Specialty,
    string Seniority,
    DateTime CreatedAt,
    bool IsActive
);

/// <summary>Fields used to register a developer without requiring contact information.</summary>
public sealed record CreateDeveloperRequest(string DisplayName, string? Specialty, string? Seniority);

/// <summary>Editable profile fields for a registered developer.</summary>
public sealed record UpdateDeveloperRequest(string DisplayName, string? Specialty, string? Seniority);

/// <summary>Active state for a developer; deactivation preserves history.</summary>
public sealed record UpdateDeveloperStatusRequest(bool IsActive);
