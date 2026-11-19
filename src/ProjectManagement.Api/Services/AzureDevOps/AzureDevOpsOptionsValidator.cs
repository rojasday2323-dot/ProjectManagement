using Microsoft.Extensions.Options;

namespace ProjectManagement.Api.Services.AzureDevOps;

public sealed class AzureDevOpsOptionsValidator : IValidateOptions<AzureDevOpsOptions>
{
    public ValidateOptionsResult Validate(string? name, AzureDevOpsOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.OrganizationUrl)
            && !AzureDevOpsOptions.IsValidOrganizationUrl(options.OrganizationUrl))
        {
            return ValidateOptionsResult.Fail(
                "AzureDevOps:OrganizationUrl debe ser una URL HTTPS válida con formato https://dev.azure.com/{organizacion}.");
        }

        return ValidateOptionsResult.Success;
    }
}
