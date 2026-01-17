using Microsoft.SemanticKernel;
using PRAgent.Services;

namespace PRAgent.Plugins.PRAnalysis;

public class PRSummaryPlugin
{
    private readonly IKernelService _kernelService;
    private readonly PullRequestDataService _prDataService;

    public PRSummaryPlugin(
        IKernelService kernelService,
        PullRequestDataService prDataService)
    {
        _kernelService = kernelService;
        _prDataService = prDataService;
    }

    public async Task<string> SummarizePullRequestAsync(
        string owner,
        string repo,
        int prNumber,
        CancellationToken cancellationToken = default)
    {
        var (pr, files, diff) = await _prDataService.GetPullRequestDataAsync(owner, repo, prNumber);
        var fileList = PullRequestDataService.FormatFileList(files);

        var systemPrompt = "You are a technical writer specializing in clear, concise documentation.";
        var prompt = PullRequestDataService.CreateSummaryPrompt(pr, fileList, diff, systemPrompt);

        var kernel = _kernelService.CreateKernel(systemPrompt);
        return await _kernelService.InvokePromptAsStringAsync(kernel, prompt, cancellationToken);
    }
}
