namespace ProjectManagement.Api.DTOs;

public record ProjectMemberResponse(
    int Id,
    int UserId,
    string DisplayName,
    string? Email,
    string Role,
    DateTime JoinedAt,
    bool IsActive
);
