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
        var kernel = _kernelService.CreateKernel("You are a senior technical lead responsible for approving pull requests.");

        var pr = await _gitHubService.GetPullRequestAsync(owner, repo, prNumber);

        var thresholdDescription = threshold switch
        {
            ApprovalThreshold.Critical => "critical: No critical issues allowed",
            ApprovalThreshold.Major => "major: No major or critical issues allowed",
            ApprovalThreshold.Minor => "minor: No minor, major, or critical issues allowed",
            ApprovalThreshold.None => "none: Always approve regardless of issues",
            _ => "minor: No minor, major, or critical issues allowed"
        };

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

            ## Instructions
            Analyze the code review and provide:

            1. **Decision**: APPROVE or REJECT

            2. **Reasoning**:
               - List any issues found above the threshold
               - Explain why the PR should be approved or rejected

            3. **Conditions** (if applicable):
               - Any conditions that should be met before merge
               - Recommended reviewers

            4. **Comment for Approval**: A brief message to post with the approval (if APPROVED)

            Format your response as:

            ```
            DECISION: [APPROVE|REJECT]
            REASONING: [Your reasoning]
            CONDITIONS: [Any conditions or N/A]
            APPROVAL_COMMENT: [Comment or N/A]
            ```

            Be conservative when approving - if in doubt, recommend rejection or request for additional review.
            """;

        var response = await _kernelService.InvokePromptAsStringAsync(kernel, prompt, cancellationToken);

        return ParseApprovalResponse(response);
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

    private (bool ShouldApprove, string Reasoning, string? Comment) ParseApprovalResponse(string response)
    {
        var decision = false;
        var reasoning = string.Empty;
        string? comment = null;

        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            if (line.StartsWith("DECISION:", StringComparison.OrdinalIgnoreCase))
            {
                var value = line.Substring("DECISION:".Length).Trim();
                decision = value.Equals("APPROVE", StringComparison.OrdinalIgnoreCase);
            }
            else if (line.StartsWith("REASONING:", StringComparison.OrdinalIgnoreCase))
            {
                reasoning = line.Substring("REASONING:".Length).Trim();
            }
            else if (line.StartsWith("APPROVAL_COMMENT:", StringComparison.OrdinalIgnoreCase))
            {
                var value = line.Substring("APPROVAL_COMMENT:".Length).Trim();
                if (!value.Equals("N/A", StringComparison.OrdinalIgnoreCase))
                {
                    comment = value;
                }
            }
        }

        return (decision, reasoning, comment);
    }
}
