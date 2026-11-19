namespace ProjectManagement.Api.DTOs;

public record UserResponse(
    int Id,
    string DisplayName,
    string? Email,
    string Role,
    bool IsActive,
    DateTime CreatedAt
);
