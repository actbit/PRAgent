using PRAgent.Models;

namespace PRAgent.Services;

public interface IConfigurationService
{
    Task<PRAgentConfig> GetConfigurationAsync(string owner, string repo, int prNumber);
    Task<string?> GetCustomPromptAsync(string owner, string repo, string promptPath);
}
