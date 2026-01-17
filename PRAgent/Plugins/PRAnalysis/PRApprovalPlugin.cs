using Microsoft.SemanticKernel;
using PRAgent.Models;
using PRAgent.Services;

namespace PRAgent.Plugins.PRAnalysis;

public class PRApprovalPlugin
{
    private readonly IKernelService _kernelService;
    private readonly IGitHubService _gitHubService;

    public PRApprovalPlugin(
        IKernelService kernelService,
        IGitHubService gitHubService)
    {
        _kernelService = kernelService;
        _gitHubService = gitHubService;
    }

    public async Task<(bool ShouldApprove, string Reasoning, string? Comment)> MakeApprovalDecisionAsync(
        string owner,
        string repo,
        int prNumber,
        string reviewResult,
        ApprovalThreshold threshold,
        CancellationToken cancellationToken = default)
    {
        var pr = await _gitHubService.GetPullRequestAsync(owner, repo, prNumber);
        var thresholdDescription = ApprovalThresholdHelper.GetDescription(threshold);

        var kernel = _kernelService.CreateKernel("You are a senior technical lead responsible for approving pull requests.");

        var prompt = $"""
            You are a senior technical lead responsible for approving pull requests. Based on the review provided, make an approval decision.

            ## Pull Request Information
            - Title: {pr.Title}
            - Author: {pr.User.Login}
            - Description: {pr.Body ?? "No description provided"}

            ## Code Review
            {reviewResult}

            ## Approval Threshold
            The current threshold for auto-approval is: {thresholdDescription}

            Provide your decision in this format:

            DECISION: [APPROVE|REJECT]
            REASONING: [Explain why, listing any issues above the threshold]
            CONDITIONS: [Any conditions for merge, or N/A]
            APPROVAL_COMMENT: [Comment or N/A]

            Be conservative when approving - if in doubt, recommend rejection or request for additional review.
            """;

        var response = await _kernelService.InvokePromptAsStringAsync(kernel, prompt, cancellationToken);

        return ApprovalResponseParser.Parse(response);
    }

    public async Task<string> ApprovePullRequestAsync(
        string owner,
        string repo,
        int prNumber,
        string? comment = null)
    {
        var result = await _gitHubService.ApprovePullRequestAsync(owner, repo, prNumber, comment);
        return $"PR approved: {result.HtmlUrl}";
    }
}
