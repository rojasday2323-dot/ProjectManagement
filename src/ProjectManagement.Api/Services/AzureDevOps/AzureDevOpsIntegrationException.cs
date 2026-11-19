namespace ProjectManagement.Api.Services.AzureDevOps;

public sealed class AzureDevOpsIntegrationException(
    int statusCode,
    string code,
    string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
}
