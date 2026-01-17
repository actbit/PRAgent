using PRAgent.Models;

namespace PRAgent.Services;

public interface IPRAnalysisService
{
    Task<string> ReviewPullRequestAsync(string owner, string repo, int prNumber, bool postComment = false);
    Task<string> SummarizePullRequestAsync(string owner, string repo, int prNumber, bool postComment = false);
    Task<ApprovalResult> ReviewAndApproveAsync(
        string owner,
        string repo,
        int prNumber,
        ApprovalThreshold threshold,
        bool postComment = false);
}
