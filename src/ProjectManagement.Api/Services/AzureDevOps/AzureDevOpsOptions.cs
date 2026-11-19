using System.Text.RegularExpressions;

namespace ProjectManagement.Api.Services.AzureDevOps;

public sealed class AzureDevOpsOptions
{
    public const string SectionName = "AzureDevOps";
    public const string UserImpersonationScope = "499b84ac-1321-427f-aa17-267ca6975798/user_impersonation";

    public string OrganizationUrl { get; set; } = string.Empty;

    public IReadOnlyList<string> Scopes { get; } = [UserImpersonationScope];

    public static bool IsValidOrganizationUrl(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            return false;

        return uri.Scheme == Uri.UriSchemeHttps
            && uri.Host.Equals("dev.azure.com", StringComparison.OrdinalIgnoreCase)
            && uri.Port == 443
            && string.IsNullOrEmpty(uri.UserInfo)
            && Regex.IsMatch(uri.AbsolutePath, "^/[A-Za-z0-9][A-Za-z0-9._-]{0,62}/?$", RegexOptions.CultureInvariant)
            && string.IsNullOrEmpty(uri.Query)
            && string.IsNullOrEmpty(uri.Fragment);
    }
}
