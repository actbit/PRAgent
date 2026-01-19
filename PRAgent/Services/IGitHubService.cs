using Octokit;

namespace PRAgent.Services;

public interface IGitHubService
{
    Task<PullRequest> GetPullRequestAsync(string owner, string repo, int prNumber);
    Task<IReadOnlyList<PullRequestFile>> GetPullRequestFilesAsync(string owner, string repo, int prNumber);
    Task<IReadOnlyList<PullRequestReviewComment>> GetPullRequestCommentsAsync(string owner, string repo, int prNumber);
    Task<IReadOnlyList<IssueComment>> GetPullRequestReviewCommentsAsync(string owner, string repo, int prNumber);
    Task<string> GetPullRequestDiffAsync(string owner, string repo, int prNumber);
    Task<PullRequestReview> CreateReviewCommentAsync(string owner, string repo, int prNumber, string body);
    Task<PullRequestReview> CreateLineCommentAsync(string owner, string repo, int prNumber, string filePath, int lineNumber, string comment, string? suggestion = null);
    Task<PullRequestReview> CreateMultipleLineCommentsAsync(string owner, string repo, int prNumber, List<(string FilePath, int LineNumber, string Comment, string? Suggestion)> comments);
    Task<IssueComment> CreateIssueCommentAsync(string owner, string repo, int prNumber, string body);
    Task<PullRequestReview> ApprovePullRequestAsync(string owner, string repo, int prNumber, string? comment = null);
    Task<string?> GetRepositoryFileContentAsync(string owner, string repo, string path, string? branch = null);
    Task<bool> FileExistsAsync(string owner, string repo, string path, string? branch = null);
}
