using Microsoft.Extensions.Logging;
using PRAgent.Models;

namespace PRAgent.Services;

public class PRAnalysisService : IPRAnalysisService
{
    private readonly IAgentOrchestratorService _agentOrchestrator;
    private readonly IGitHubService _gitHubService;
    private readonly IConfigurationService _configurationService;
    private readonly ILogger<PRAnalysisService> _logger;

    public PRAnalysisService(
        IAgentOrchestratorService agentOrchestrator,
        IGitHubService gitHubService,
        IConfigurationService configurationService,
        ILogger<PRAnalysisService> logger)
    {
        _agentOrchestrator = agentOrchestrator;
        _gitHubService = gitHubService;
        _configurationService = configurationService;
        _logger = logger;
    }

    public async Task<string> ReviewPullRequestAsync(string owner, string repo, int prNumber, bool postComment = false)
    {
        _logger.LogInformation("Starting PR review for {Owner}/{Repo}#{PrNumber}", owner, repo, prNumber);

        var config = await _configurationService.GetConfigurationAsync(owner, repo, prNumber);

        if (!config.Enabled)
        {
            return "PRAgent is disabled for this repository.";
        }

        if (config.Review == null || !config.Review.Enabled)
        {
            return "Review is disabled for this repository.";
        }

        var review = await _agentOrchestrator.ReviewAsync(owner, repo, prNumber);

        if (postComment)
        {
            await _gitHubService.CreateIssueCommentAsync(owner, repo, prNumber,
                $"## 🤖 PRAgent Review\n\n{review}");
            _logger.LogInformation("Review comment posted for {Owner}/{Repo}#{PrNumber}", owner, repo, prNumber);
        }

        return review;
    }

    public async Task<string> SummarizePullRequestAsync(string owner, string repo, int prNumber, bool postComment = false)
    {
        _logger.LogInformation("Starting PR summary for {Owner}/{Repo}#{PrNumber}", owner, repo, prNumber);

        var config = await _configurationService.GetConfigurationAsync(owner, repo, prNumber);

        if (!config.Enabled)
        {
            return "PRAgent is disabled for this repository.";
        }

        if (config.Summary == null || !config.Summary.Enabled)
        {
            return "Summary is disabled for this repository.";
        }

        var summary = await _agentOrchestrator.SummarizeAsync(owner, repo, prNumber);

        if (postComment)
        {
            await _gitHubService.CreateIssueCommentAsync(owner, repo, prNumber,
                $"## 🤖 PRAgent Summary\n\n{summary}");
            _logger.LogInformation("Summary comment posted for {Owner}/{Repo}#{PrNumber}", owner, repo, prNumber);
        }

        return summary;
    }

    public async Task<ApprovalResult> ReviewAndApproveAsync(
        string owner,
        string repo,
        int prNumber,
        ApprovalThreshold threshold,
        bool postComment = false)
    {
        _logger.LogInformation("Starting PR review and approval for {Owner}/{Repo}#{PrNumber}", owner, repo, prNumber);

        var config = await _configurationService.GetConfigurationAsync(owner, repo, prNumber);

        if (!config.Enabled)
        {
            return new ApprovalResult
            {
                Approved = false,
                Review = "PRAgent is disabled for this repository.",
                Reasoning = "Feature disabled in repository configuration."
            };
        }

        if (config.Approve == null || !config.Approve.Enabled)
        {
            return new ApprovalResult
            {
                Approved = false,
                Review = "Approval is disabled for this repository.",
                Reasoning = "Approval disabled in repository configuration."
            };
        }

        var result = await _agentOrchestrator.ReviewAndApproveAsync(owner, repo, prNumber, threshold);

        if (postComment && !result.Approved)
        {
            await _gitHubService.CreateIssueCommentAsync(owner, repo, prNumber,
                $"## 🤖 PRAgent Approval Decision\n\n**Decision:** Not Approved\n\n**Reasoning:** {result.Reasoning}\n\n---\n\n{result.Review}");
        }

        return result;
    }
}
