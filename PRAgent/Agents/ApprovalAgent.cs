using Microsoft.SemanticKernel;
using PRAgent.Models;
using PRAgent.Services;

namespace PRAgent.Agents;

public class ApprovalAgent : BaseAgent
{
    public ApprovalAgent(
        IKernelService kernelService,
        IGitHubService gitHubService,
        PullRequestDataService prDataService,
        string? customSystemPrompt = null)
        : base(kernelService, gitHubService, prDataService, AgentDefinition.ApprovalAgent, customSystemPrompt)
    {
    }

    public async Task<(bool ShouldApprove, string Reasoning, string? Comment)> DecideAsync(
        string owner,
        string repo,
        int prNumber,
        string reviewResult,
        ApprovalThreshold threshold,
        CancellationToken cancellationToken = default)
    {
        var pr = await GitHubService.GetPullRequestAsync(owner, repo, prNumber);
        var thresholdDescription = ApprovalThresholdHelper.GetDescription(threshold);

        var prompt = $"""
            Based on the code review below, make an approval decision for this pull request.

            ## Pull Request
            - Title: {pr.Title}
            - Author: {pr.User.Login}

            ## Code Review Result
            {reviewResult}

            ## Approval Threshold
            {thresholdDescription}

            Provide your decision in this format:

            DECISION: [APPROVE/REJECT]
            REASONING: [Explain why, listing any issues above the threshold]
            CONDITIONS: [Any conditions for merge, or N/A]
            APPROVAL_COMMENT: [Brief comment if approved, or N/A]

            Be conservative - when in doubt, reject or request additional review.
            """;

        var response = await KernelService.InvokePromptAsStringAsync(CreateKernel(), prompt, cancellationToken);

        return ApprovalResponseParser.Parse(response);
    }

    public async Task<string> ApproveAsync(
        string owner,
        string repo,
        int prNumber,
        string? comment = null)
    {
        var result = await GitHubService.ApprovePullRequestAsync(owner, repo, prNumber, comment);
        return $"PR approved: {result.HtmlUrl}";
    }
}
