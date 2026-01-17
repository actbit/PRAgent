using Microsoft.SemanticKernel;
using Octokit;
using PRAgent.Services;

namespace PRAgent.Agents;

public abstract class BaseAgent
{
    protected readonly IKernelService KernelService;
    protected readonly IGitHubService GitHubService;
    protected readonly PullRequestDataService PRDataService;
    protected AgentDefinition Definition;

    protected BaseAgent(
        IKernelService kernelService,
        IGitHubService gitHubService,
        PullRequestDataService prDataService,
        AgentDefinition definition,
        string? customSystemPrompt = null)
    {
        KernelService = kernelService;
        GitHubService = gitHubService;
        PRDataService = prDataService;

        Definition = definition;

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
