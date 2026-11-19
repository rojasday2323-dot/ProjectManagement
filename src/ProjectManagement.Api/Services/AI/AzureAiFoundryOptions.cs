namespace ProjectManagement.Api.Services.AI;

public sealed class AzureAiFoundryOptions
{
    public const string SectionName = "AzureAiFoundry";

    public string? Endpoint { get; set; }
    public string? ApiKey { get; set; }
    public string? Model { get; set; }
    public string? DeploymentName
    {
        get => Model;
        set => Model = value;
    }

    public string ApiVersion { get; set; } = "2023-06-01";
    public int MaxOutputTokens { get; set; } = 16384;
}
