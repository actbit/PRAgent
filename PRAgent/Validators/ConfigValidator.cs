using PRAgent.Models;

namespace PRAgent.Validators;

public static class ConfigValidator
{
    public static bool ValidateAISettings(AISettings settings, IList<string> errors)
    {
        var isValid = true;

        if (string.IsNullOrWhiteSpace(settings.Endpoint))
        {
            errors.Add("AI Endpoint is required.");
            isValid = false;
        }
        else if (!Uri.TryCreate(settings.Endpoint, UriKind.Absolute, out _))
        {
            errors.Add("AI Endpoint must be a valid URI.");
            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            errors.Add("AI API Key is required.");
            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(settings.ModelId))
        {
            errors.Add("AI Model ID is required.");
            isValid = false;
        }

        return isValid;
    }

    public static bool ValidatePRSettings(PRSettings settings, IList<string> errors)
    {
        // GitHub Token can be provided via environment variable in GitHub Actions
        // Skip validation if running in CI (check for GITHUB_ACTIONS env var)
        var isGitHubActions = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"));

        if (!isGitHubActions && string.IsNullOrWhiteSpace(settings.GitHubToken))
        {
            errors.Add("GitHub Token is required when not running in GitHub Actions.");
            return false;
        }

        return true;
    }

    public static bool ValidateApprovalThreshold(string threshold, IList<string> errors)
    {
        var validValues = new[] { "critical", "major", "minor", "none" };

        if (!validValues.Contains(threshold.ToLowerInvariant()))
        {
            errors.Add($"Approval threshold must be one of: {string.Join(", ", validValues)}");
            return false;
        }

        return true;
    }
}
