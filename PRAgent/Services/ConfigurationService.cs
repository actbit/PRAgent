using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PRAgent.Models;

namespace PRAgent.Services;

public class ConfigurationService : IConfigurationService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ConfigurationService> _logger;

    public ConfigurationService(
        IConfiguration configuration,
        ILogger<ConfigurationService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public Task<PRAgentConfig> GetConfigurationAsync(string owner, string repo, int prNumber)
    {
        // appsettings.jsonから設定を読み込み
        var config = new PRAgentConfig
        {
            Enabled = _configuration.GetValue<bool>("PRAgent:Enabled", true),
            SystemPrompt = _configuration["PRAgent:SystemPrompt"],
            Review = new ReviewConfig
            {
                Enabled = _configuration.GetValue<bool>("PRAgent:Review:Enabled", true),
                AutoPost = _configuration.GetValue<bool>("PRAgent:Review:AutoPost", false),
                CustomPrompt = _configuration["PRAgent:Review:CustomPrompt"]
            },
            Summary = new SummaryConfig
            {
                Enabled = _configuration.GetValue<bool>("PRAgent:Summary:Enabled", true),
                PostAsComment = _configuration.GetValue<bool>("PRAgent:Summary:PostAsComment", true),
                CustomPrompt = _configuration["PRAgent:Summary:CustomPrompt"]
            },
            Approve = new ApproveConfig
            {
                Enabled = _configuration.GetValue<bool>("PRAgent:Approve:Enabled", true),
                AutoApproveThreshold = _configuration.GetValue<string>("PRAgent:Approve:AutoApproveThreshold", "minor") ?? "minor",
                RequireReviewFirst = _configuration.GetValue<bool>("PRAgent:Approve:RequireReviewFirst", true)
            },
            IgnorePaths = _configuration.GetSection("PRAgent:IgnorePaths").Get<List<string>>() ?? GetDefaultIgnorePaths(),
            AgentFramework = new AgentFrameworkConfig
            {
                Enabled = _configuration.GetValue<bool>("PRAgent:AgentFramework:Enabled", true),
                OrchestrationMode = _configuration.GetValue<string>("PRAgent:AgentFramework:OrchestrationMode", "sequential") ?? "sequential",
                SelectionStrategy = _configuration.GetValue<string>("PRAgent:AgentFramework:SelectionStrategy", "approval_workflow") ?? "approval_workflow",
                EnableFunctionCalling = _configuration.GetValue<bool>("PRAgent:AgentFramework:EnableFunctionCalling", true),
                EnableAutoApproval = _configuration.GetValue<bool>("PRAgent:AgentFramework:EnableAutoApproval", true),
                MaxTurns = _configuration.GetValue<int>("PRAgent:AgentFramework:MaxTurns", 10)
            }
        };

        _logger.LogInformation("Loaded PRAgent configuration from appsettings.json");
        return Task.FromResult(config);
    }

    public Task<string?> GetCustomPromptAsync(string owner, string repo, string promptPath)
    {
        // appsettings.jsonからカスタムプロンプトを取得
        var customPrompt = _configuration[$"PRAgent:CustomPrompts:{promptPath}"];
        return Task.FromResult(customPrompt);
    }

    private static List<string> GetDefaultIgnorePaths()
    {
        return new List<string>
        {
            "*.min.js",
            "dist/**",
            "node_modules/**",
            "*.min.css",
            "bin/**",
            "obj/**"
        };
    }
}
