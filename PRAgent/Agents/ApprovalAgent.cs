using Microsoft.SemanticKernel;
using PRAgent.Models;
using PRAgent.Services;

namespace PRAgent.Agents;

public class ApprovalAgent
{
    private readonly IKernelService _kernelService;
    private readonly IGitHubService _gitHubService;
    private readonly AgentDefinition _definition;

    public ApprovalAgent(
        IKernelService kernelService,
        IGitHubService gitHubService,
        string? customSystemPrompt = null)
    {
        _kernelService = kernelService;
        _gitHubService = gitHubService;
        _definition = AgentDefinition.ApprovalAgent;

        if (!string.IsNullOrEmpty(customSystemPrompt))
        {
            _definition = new AgentDefinition(
                _definition.Name,
                _definition.Role,
                customSystemPrompt,
                _definition.Description
            );
        }
    }

    public async Task<(bool ShouldApprove, string Reasoning, string? Comment)> DecideAsync(
        string owner,
        string repo,
        int prNumber,
        string reviewResult,
        ApprovalThreshold threshold,
        CancellationToken cancellationToken = default)
    {
        var kernel = _kernelService.CreateKernel(_definition.SystemPrompt);

        var pr = await _gitHubService.GetPullRequestAsync(owner, repo, prNumber);

        var thresholdDescription = threshold switch
        {
            ApprovalThreshold.Critical => "CRITICAL: No critical issues allowed",
            ApprovalThreshold.Major => "MAJOR: No major or critical issues allowed",
            ApprovalThreshold.Minor => "MINOR: No minor, major, or critical issues allowed",
            ApprovalThreshold.None => "NONE: Always approve",
            _ => "MINOR: No minor, major, or critical issues allowed"
        };

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

        var response = await _kernelService.InvokePromptAsStringAsync(kernel, prompt, cancellationToken);

        return ParseApprovalResponse(response);
    }

    public async Task<string> ApproveAsync(
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
        bool shouldApprove = false;
        string reasoning = string.Empty;
        string? comment = null;

        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("DECISION:", StringComparison.OrdinalIgnoreCase))
            {
                var value = trimmed.Substring("DECISION:".Length).Trim();
                shouldApprove = value.Equals("APPROVE", StringComparison.OrdinalIgnoreCase);
            }
            else if (trimmed.StartsWith("REASONING:", StringComparison.OrdinalIgnoreCase))
            {
                reasoning = trimmed.Substring("REASONING:".Length).Trim();
            }
            else if (trimmed.StartsWith("APPROVAL_COMMENT:", StringComparison.OrdinalIgnoreCase))
            {
                var value = trimmed.Substring("APPROVAL_COMMENT:".Length).Trim();
                if (!value.Equals("N/A", StringComparison.OrdinalIgnoreCase))
                {
                    comment = value;
                }
            }
        }

        return (shouldApprove, reasoning, comment);
    }
}
