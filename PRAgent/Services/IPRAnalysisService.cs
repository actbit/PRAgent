using PRAgent.Models;

namespace PRAgent.Services;

public interface IPRAnalysisService
{
    Task<string> ReviewPullRequestAsync(string owner, string repo, int prNumber, bool postComment = false, string? language = null);
    Task<string> SummarizePullRequestAsync(string owner, string repo, int prNumber, bool postComment = false, string? language = null);
    Task<ApprovalResult> ReviewAndApproveAsync(
        string owner,
        string repo,
        int prNumber,
        ApprovalThreshold threshold,
        bool postComment = false,
        string? language = null);
}
