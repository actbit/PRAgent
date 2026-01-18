using PRAgent.Agents;
using PRAgent.Models;
using PRAgent.Services;

namespace PRAgent.Services;

public class AgentOrchestratorService : IAgentOrchestratorService
{
    private readonly UnifiedReviewAgent _unifiedReviewAgent;
    private readonly ReviewAgent _reviewAgent; // 後方互換性のため
    private readonly ApprovalAgent _approvalAgent;
    private readonly SummaryAgent _summaryAgent;
    private readonly IGitHubService _gitHubService;

    public AgentOrchestratorService(
        UnifiedReviewAgent unifiedReviewAgent,
        ReviewAgent reviewAgent,
        ApprovalAgent approvalAgent,
        SummaryAgent summaryAgent,
        IGitHubService gitHubService)
    {
        _unifiedReviewAgent = unifiedReviewAgent;
        _reviewAgent = reviewAgent;
        _approvalAgent = approvalAgent;
        _summaryAgent = summaryAgent;
        _gitHubService = gitHubService;
    }

  
    public async Task<string> SummarizeAsync(string owner, string repo, int prNumber, CancellationToken cancellationToken = default)
    {
        return await _summaryAgent.SummarizeAsync(owner, repo, prNumber, cancellationToken);
    }

    public async Task<ApprovalResult> ReviewAndApproveAsync(
        string owner,
        string repo,
        int prNumber,
        ApprovalThreshold threshold,
        CancellationToken cancellationToken = default)
    {
        // Step 1: ReviewAgent performs the review
        var review = await _reviewAgent.ReviewAsync(owner, repo, prNumber, cancellationToken);

        // Step 2: ApprovalAgent decides based on the review
        var (shouldApprove, reasoning, comment) = await _approvalAgent.DecideAsync(
            owner, repo, prNumber, review, threshold, cancellationToken);

        // Step 3: If approved, execute the approval
        string? approvalUrl = null;
        if (shouldApprove)
        {
            var approvalResult = await _approvalAgent.ApproveAsync(owner, repo, prNumber, comment);
            approvalUrl = approvalResult;
        }

        return new ApprovalResult
        {
            Approved = shouldApprove,
            Review = review,
            Reasoning = reasoning,
            Comment = comment,
            ApprovalUrl = approvalUrl
        };
    }

    public async Task<string> ReviewAsync(string owner, string repo, int prNumber, CancellationToken cancellationToken = default)
    {
        // 統合されたエージェントを使用
        var result = await _unifiedReviewAgent.ReviewAsync(owner, repo, prNumber, cancellationToken);

        return result.Review;
    }

    public async Task<string> ReviewAsync(string owner, string repo, int prNumber, string language, CancellationToken cancellationToken = default)
    {
        // 統合されたエージェントを使用
        var result = await _unifiedReviewAgent.ReviewAsync(owner, repo, prNumber, cancellationToken);

        return result.Review;
    }

    public async Task<string> SummarizeAsync(string owner, string repo, int prNumber, string language, CancellationToken cancellationToken = default)
    {
        _summaryAgent.SetLanguage(language);
        return await _summaryAgent.SummarizeAsync(owner, repo, prNumber, cancellationToken);
    }

    public async Task<ApprovalResult> ReviewAndApproveAsync(
        string owner,
        string repo,
        int prNumber,
        ApprovalThreshold threshold,
        string language,
        CancellationToken cancellationToken = default)
    {
        _unifiedReviewAgent.SetLanguage(language);
        _approvalAgent.SetLanguage(language);

        // Step 1: UnifiedReviewAgent performs the review
        var result = await _unifiedReviewAgent.ReviewAsync(owner, repo, prNumber, cancellationToken);
        var review = result.Review;

        // Step 2: ApprovalAgent decides based on the review
        var (shouldApprove, reasoning, comment) = await _approvalAgent.DecideAsync(
            owner, repo, prNumber, review, threshold, cancellationToken);

        // Step 3: If approved, execute the approval
        string? approvalUrl = null;
        if (shouldApprove)
        {
            var approvalResult = await _approvalAgent.ApproveAsync(owner, repo, prNumber, comment);
            approvalUrl = approvalResult;
        }

        return new ApprovalResult
        {
            Approved = shouldApprove,
            Review = review,
            Reasoning = reasoning,
            Comment = comment,
            ApprovalUrl = approvalUrl
        };
    }
}
