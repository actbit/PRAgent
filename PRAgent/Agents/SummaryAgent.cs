using Microsoft.SemanticKernel;
using PRAgent.Services;

namespace PRAgent.Agents;

public class SummaryAgent : BaseAgent
{
    public SummaryAgent(
        IKernelService kernelService,
        IGitHubService gitHubService,
        PullRequestDataService prDataService,
        string? customSystemPrompt = null)
        : base(kernelService, gitHubService, prDataService, AgentDefinition.SummaryAgent, customSystemPrompt)
    {
    }

    public async Task<string> SummarizeAsync(
        string owner,
        string repo,
        int prNumber,
        CancellationToken cancellationToken = default)
    {
        var (pr, files, diff) = await GetPRDataAsync(owner, repo, prNumber);
        var fileList = PullRequestDataService.FormatFileList(files);

        var systemPrompt = """
            You are a technical writer specializing in creating clear, concise documentation.
            Your role is to summarize pull request changes accurately.
            """;

        var prompt = PullRequestDataService.CreateSummaryPrompt(pr, fileList, diff, systemPrompt);

        return await KernelService.InvokePromptAsStringAsync(CreateKernel(), prompt, cancellationToken);
    }
}
