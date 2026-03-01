using Octokit;
using PRAgent.Models;

namespace PRAgent.Services;

public class GitHubService : IGitHubService
{
    private readonly GitHubClient _client;

    public GitHubService(string gitHubToken)
    {
        _client = new GitHubClient(new ProductHeaderValue("PRAgent"))
        {
            Credentials = new Credentials(gitHubToken)
        };
    }

    public async Task<PullRequest> GetPullRequestAsync(string owner, string repo, int prNumber)
    {
        return await _client.PullRequest.Get(owner, repo, prNumber);
    }

    public async Task<IReadOnlyList<PullRequestFile>> GetPullRequestFilesAsync(string owner, string repo, int prNumber)
    {
        return await _client.PullRequest.Files(owner, repo, prNumber);
    }

    public async Task<IReadOnlyList<PullRequestReviewComment>> GetPullRequestCommentsAsync(string owner, string repo, int prNumber)
    {
        // Octokit 14.0.0 doesn't have direct PullRequestReviewComment access via PullRequest.Comment
        // Return empty list for now - this method is optional for core functionality
        return Array.Empty<PullRequestReviewComment>();
    }

    public async Task<IReadOnlyList<IssueComment>> GetPullRequestReviewCommentsAsync(string owner, string repo, int prNumber)
    {
        return await _client.Issue.Comment.GetAllForIssue(owner, repo, prNumber);
    }

    public async Task<string> GetPullRequestDiffAsync(string owner, string repo, int prNumber)
    {
        var pr = await GetPullRequestAsync(owner, repo, prNumber);
        var files = await GetPullRequestFilesAsync(owner, repo, prNumber);

        var diffBuilder = new System.Text.StringBuilder();
        diffBuilder.AppendLine($"# Pull Request #{prNumber}: {pr.Title}");
        diffBuilder.AppendLine($"Author: {pr.User.Login}");
        diffBuilder.AppendLine($"Description: {pr.Body}");
        diffBuilder.AppendLine();

        foreach (var file in files)
        {
            diffBuilder.AppendLine($"## {file.FileName}");
            diffBuilder.AppendLine($"Status: {file.Status}");
            diffBuilder.AppendLine($"Changes: +{file.Additions} -{file.Deletions}");
            diffBuilder.AppendLine();

            if (!string.IsNullOrEmpty(file.Patch))
            {
                diffBuilder.AppendLine("```diff");
                diffBuilder.AppendLine(file.Patch);
                diffBuilder.AppendLine("```");
            }
            diffBuilder.AppendLine();
        }

        return diffBuilder.ToString();
    }

    public async Task<PullRequestReview> CreateReviewCommentAsync(string owner, string repo, int prNumber, string body)
    {
        var reviewComment = new PullRequestReviewCreate()
        {
            Body = body,
            Event = PullRequestReviewEvent.Comment
        };

        return await _client.PullRequest.Review.Create(owner, repo, prNumber, reviewComment);
    }

    public async Task<PullRequestReview> CreateReviewWithCommentsAsync(string owner, string repo, int prNumber, string reviewBody, List<DraftPullRequestReviewComment> comments)
    {
        var reviewComment = new PullRequestReviewCreate()
        {
            Body = reviewBody,
            Event = PullRequestReviewEvent.Comment,
            Comments = comments
        };

        return await _client.PullRequest.Review.Create(owner, repo, prNumber, reviewComment);
    }

    /// <summary>
    /// レビュー本文と詳細コメントをまとめて投稿
    /// </summary>
    public async Task CreateCompleteReviewAsync(string owner, string repo, int prNumber, string reviewBody, List<DraftPullRequestReviewComment> comments)
    {
        var review = new PullRequestReviewCreate()
        {
            Body = reviewBody,
            Event = PullRequestReviewEvent.Comment,
            Comments = comments
        };

        await _client.PullRequest.Review.Create(owner, repo, prNumber, review);
    }

    public async Task<IssueComment> CreateIssueCommentAsync(string owner, string repo, int prNumber, string body)
    {
        return await _client.Issue.Comment.Create(owner, repo, prNumber, body);
    }

    public async Task<PullRequestReview> ApprovePullRequestAsync(string owner, string repo, int prNumber, string? comment = null)
    {
        var review = new PullRequestReviewCreate()
        {
            Body = comment ?? "Approved by PRAgent",
            Event = PullRequestReviewEvent.Approve
        };

        return await _client.PullRequest.Review.Create(owner, repo, prNumber, review);
    }

    public async Task<string?> GetRepositoryFileContentAsync(string owner, string repo, string path, string? branch = null)
    {
        try
        {
            var defaultBranch = await _client.Repository.Get(owner, repo);
            var reference = branch ?? $"heads/{defaultBranch.DefaultBranch}";

            var contents = await _client.Repository.Content.GetAllContentsByRef(owner, repo, path, reference);

            if (contents.Count > 0)
            {
                var content = contents[0];
                if (content.Type == ContentType.File)
                {
                    return content.Content;
                }
            }

            return null;
        }
        catch (NotFoundException)
        {
            return null;
        }
    }

    public async Task<bool> FileExistsAsync(string owner, string repo, string path, string? branch = null)
    {
        var content = await GetRepositoryFileContentAsync(owner, repo, path, branch);
        return content != null;
    }

    public async Task<PullRequestReview> CreateLineCommentAsync(string owner, string repo, int prNumber, string filePath, int lineNumber, string comment, string? suggestion = null)
    {
        // 行コメントを作成
        var commentBody = suggestion != null ? $"{comment}\n```suggestion\n{suggestion}\n```" : comment;

        return await _client.PullRequest.Review.Create(
            owner,
            repo,
            prNumber,
            new PullRequestReviewCreate
            {
                Event = PullRequestReviewEvent.Comment,
                Comments = new List<DraftPullRequestReviewComment>
                {
                    new DraftPullRequestReviewComment(commentBody, filePath, lineNumber)
                }
            }
        );
    }

    public async Task<PullRequestReview> CreateMultipleLineCommentsAsync(string owner, string repo, int prNumber, List<(string FilePath, int? LineNumber, int? StartLine, int? EndLine, string Comment, string? Suggestion)> comments)
    {
        var draftComments = comments.Select(c =>
        {
            var commentBody = c.Suggestion != null ? $"{c.Comment}\n```suggestion\n{c.Suggestion}\n```" : c.Comment;

            // 1行コメントのみ対応（LineNumberがある場合）
            if (c.LineNumber.HasValue)
            {
                return new DraftPullRequestReviewComment(commentBody, c.FilePath, c.LineNumber.Value);
            }
            // 範囲コメントは1行目を使用
            else if (c.StartLine.HasValue)
            {
                return new DraftPullRequestReviewComment(commentBody, c.FilePath, c.StartLine.Value);
            }
            else
            {
                throw new ArgumentException($"Comment must have either LineNumber or StartLine: {c.FilePath}");
            }
        }).ToList();

        return await _client.PullRequest.Review.Create(
            owner,
            repo,
            prNumber,
            new PullRequestReviewCreate
            {
                Event = PullRequestReviewEvent.Comment,
                Comments = draftComments
            }
        );
    }
}
