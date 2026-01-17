using PRAgent.Agents;
using PRAgent.Models;

namespace PRAgent.Services;

public class AgentOrchestratorService : IAgentOrchestratorService
{
    private readonly ReviewAgent _reviewAgent;
    private readonly ApprovalAgent _approvalAgent;
    private readonly SummaryAgent _summaryAgent;

    public AgentOrchestratorService(
        ReviewAgent reviewAgent,
        ApprovalAgent approvalAgent,
        SummaryAgent summaryAgent)
    {
        _reviewAgent = reviewAgent;
        _approvalAgent = approvalAgent;
        _summaryAgent = summaryAgent;
    }

    public async Task<string> ReviewAsync(string owner, string repo, int prNumber, CancellationToken cancellationToken = default)
    {
        return await _reviewAgent.ReviewAsync(owner, repo, prNumber, cancellationToken);
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
            var result = await _approvalAgent.ApproveAsync(owner, repo, prNumber, comment);
            approvalUrl = result;
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

    public async Task<string> ReviewAsync(string owner, string repo, int prNumber, string language, CancellationToken cancellationToken = default)
    {
        _reviewAgent.SetLanguage(language);
        return await _reviewAgent.ReviewAsync(owner, repo, prNumber, cancellationToken);
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
        _reviewAgent.SetLanguage(language);
        _approvalAgent.SetLanguage(language);

        // Step 1: ReviewAgent performs the review
        var review = await _reviewAgent.ReviewAsync(owner, repo, prNumber, cancellationToken);

        // Step 2: ApprovalAgent decides based on the review
        var (shouldApprove, reasoning, comment) = await _approvalAgent.DecideAsync(
            owner, repo, prNumber, review, threshold, cancellationToken);

        // Step 3: If approved, execute the approval
        string? approvalUrl = null;
        if (shouldApprove)
        {
            var result = await _approvalAgent.ApproveAsync(owner, repo, prNumber, comment);
            approvalUrl = result;
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
