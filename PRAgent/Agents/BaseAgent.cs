using Microsoft.SemanticKernel;
using Octokit;
using PRAgent.Models;
using PRAgent.Services;

namespace PRAgent.Agents;

public abstract class BaseAgent
{
    protected readonly IKernelService KernelService;
    protected readonly IGitHubService GitHubService;
    protected readonly PullRequestDataService PRDataService;
    protected readonly AISettings AISettings;
    protected AgentDefinition Definition;

    protected BaseAgent(
        IKernelService kernelService,
        IGitHubService gitHubService,
        PullRequestDataService prDataService,
        AISettings aiSettings,
        AgentDefinition definition,
        string? customSystemPrompt = null)
    {
        KernelService = kernelService;
        GitHubService = gitHubService;
        PRDataService = prDataService;
        AISettings = aiSettings;

        // Apply language setting to the agent definition
        Definition = definition.WithLanguage(aiSettings.Language);

        if (!string.IsNullOrEmpty(customSystemPrompt))
        {
            Definition = new AgentDefinition(
                Definition.Name,
                Definition.Role,
                customSystemPrompt,
                Definition.Description
            );
        }
    }

    /// <summary>
    /// Sets the language for AI responses dynamically
    /// </summary>
    protected void SetLanguage(string language)
    {
        Definition = Definition.WithLanguage(language);
    }

    protected Kernel CreateKernel()
    {
        return KernelService.CreateKernel(Definition.SystemPrompt);
    }

    protected async Task<(PullRequest pr, IReadOnlyList<PullRequestFile> files, string diff)> GetPRDataAsync(
        string owner, string repo, int prNumber)
    {
        return await PRDataService.GetPullRequestDataAsync(owner, repo, prNumber);
    }
}
