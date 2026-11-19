namespace ProjectManagement.Api.DTOs;

public record ProductOwnerResponse(
    int Id,
    int UserId,
    string DisplayName,
    string? Email,
    string Department,
    DateTime CreatedAt
);
