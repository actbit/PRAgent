using Microsoft.SemanticKernel;
using PRAgent.Services;

namespace PRAgent.Plugins.PRAnalysis;

public class PRReviewPlugin
{
    private readonly IKernelService _kernelService;
    private readonly PullRequestDataService _prDataService;
    private readonly string _systemPrompt;

    public PRReviewPlugin(
        IKernelService kernelService,
        PullRequestDataService prDataService,
        string? systemPrompt = null)
    {
        _kernelService = kernelService;
        _prDataService = prDataService;
        _systemPrompt = systemPrompt ?? "You are an expert code reviewer. Provide thorough, constructive feedback.";
    }

    public async Task<string> ReviewPullRequestAsync(
        string owner,
        string repo,
        int prNumber,
        CancellationToken cancellationToken = default)
    {
        var (pr, files, diff) = await _prDataService.GetPullRequestDataAsync(owner, repo, prNumber);
        var fileList = PullRequestDataService.FormatFileList(files);

        var prompt = PullRequestDataService.CreateReviewPrompt(pr, fileList, diff, _systemPrompt);

        var kernel = _kernelService.CreateKernel(_systemPrompt);
        return await _kernelService.InvokePromptAsStringAsync(kernel, prompt, cancellationToken);
    }
}
