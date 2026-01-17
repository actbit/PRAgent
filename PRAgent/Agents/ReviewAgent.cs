using Microsoft.SemanticKernel;
using PRAgent.Services;

namespace PRAgent.Agents;

public class ReviewAgent : BaseAgent
{
    public ReviewAgent(
        IKernelService kernelService,
        IGitHubService gitHubService,
        PullRequestDataService prDataService,
        string? customSystemPrompt = null)
        : base(kernelService, gitHubService, prDataService, AgentDefinition.ReviewAgent, customSystemPrompt)
    {
    }

    public async Task<string> ReviewAsync(
        string owner,
        string repo,
        int prNumber,
        CancellationToken cancellationToken = default)
    {
        var (pr, files, diff) = await GetPRDataAsync(owner, repo, prNumber);
        var fileList = PullRequestDataService.FormatFileList(files);

        var systemPrompt = """
            You are an expert code reviewer with deep knowledge of software engineering best practices.
            Your role is to provide thorough, constructive, and actionable feedback on pull requests.
            """;

        var prompt = PullRequestDataService.CreateReviewPrompt(pr, fileList, diff, systemPrompt);

        return await KernelService.InvokePromptAsStringAsync(CreateKernel(), prompt, cancellationToken);
    }
}
