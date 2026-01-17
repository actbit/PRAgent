using Microsoft.Extensions.Logging;
using PRAgent.Configuration;
using PRAgent.Models;

namespace PRAgent.Services;

public class ConfigurationService : IConfigurationService
{
    private readonly IGitHubService _gitHubService;
    private readonly ILogger<ConfigurationService> _logger;

    public ConfigurationService(
        IGitHubService gitHubService,
        ILogger<ConfigurationService> logger)
    {
        _gitHubService = gitHubService;
        _logger = logger;
    }

    public async Task<PRAgentConfig> GetConfigurationAsync(string owner, string repo, int prNumber)
    {
        try
        {
            // Try to get .github/pragent.yml from the repository
            var yamlContent = await _gitHubService.GetRepositoryFileContentAsync(owner, repo, ".github/pragent.yml");

            if (!string.IsNullOrEmpty(yamlContent))
            {
                var config = YamlConfigurationProvider.Deserialize<PRAgentYmlConfig>(yamlContent);
                if (config?.PRAgent != null)
                {
                    _logger.LogInformation("Loaded PRAgent configuration from .github/pragent.yml");
                    return config.PRAgent;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load .github/pragent.yml, using default configuration");
        }

        // Return default configuration
        _logger.LogInformation("Using default PRAgent configuration");
        return GetDefaultConfiguration();
    }

    public async Task<string?> GetCustomPromptAsync(string owner, string repo, string promptPath)
    {
        if (string.IsNullOrEmpty(promptPath))
        {
            return null;
        }

        try
        {
            var content = await _gitHubService.GetRepositoryFileContentAsync(owner, repo, promptPath);
            return content;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load custom prompt from {PromptPath}", promptPath);
            return null;
        }
    }

    private static PRAgentConfig GetDefaultConfiguration()
    {
        return new PRAgentConfig
        {
            Enabled = true,
            SystemPrompt = "You are an expert code reviewer and technical writer.",
            Review = new ReviewConfig
            {
                Enabled = true,
                AutoPost = false
            },
            Summary = new SummaryConfig
            {
                Enabled = true,
                PostAsComment = true
            },
            Approve = new ApproveConfig
            {
                Enabled = true,
                AutoApproveThreshold = "minor",
                RequireReviewFirst = true
            },
            IgnorePaths = new List<string>
            {
                "*.min.js",
                "dist/**",
                "node_modules/**",
                "*.min.css",
                "bin/**",
                "obj/**"
            }
        };
    }
}
