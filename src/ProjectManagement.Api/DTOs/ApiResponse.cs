namespace ProjectManagement.Api.DTOs;

public record ApiErrorResponse(
    bool Success,
    string Message
)
{
    public static ApiErrorResponse Error(string message) => new(false, message);
}
