using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using PRAgent.Models;
using PRAgent.Services;

namespace PRAgent.Agents;

public class SummaryAgent : BaseAgent
{
    private readonly ILogger<SummaryAgent> _logger;

    public SummaryAgent(
        IKernelService kernelService,
        IGitHubService gitHubService,
        PullRequestDataService prDataService,
        AISettings aiSettings,
        ILogger<SummaryAgent> logger,
        string? customSystemPrompt = null)
        : base(kernelService, gitHubService, prDataService, aiSettings, AgentDefinition.SummaryAgent, customSystemPrompt)
    {
        _logger = logger;
    }

    public new void SetLanguage(string language) => base.SetLanguage(language);

    public async Task<string> SummarizeAsync(
        string owner,
        string repo,
        int prNumber,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Language: {Language}", AISettings.Language);
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
