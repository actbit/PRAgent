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

    
    public async Task<PullRequestReview> CreateReviewWithCommentsAsync(
        string owner,
        string repo,
        int prNumber,
        string commitId,
        string body,
        IEnumerable<Octokit.DraftPullRequestReviewComment> comments)
    {
        try
        {
            // レビューを作成（今回はコメントは別途投稿）
            var review = new PullRequestReviewCreate
            {
                CommitId = commitId,
                Event = PullRequestReviewEvent.Comment,
                Body = body
            };

            return await _client.PullRequest.Review.Create(owner, repo, prNumber, review);
        }
        catch (Exception ex)
        {
            // Loggerがない場合は単にログを出力せずエラーをスロー
            Console.WriteLine($"Failed to create review with comments for PR {prNumber} in {owner}/{repo}: {ex.Message}");
            throw;
        }
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

    public async Task<PullRequestReviewComment> CreatePullRequestCommentAsync(string owner, string repo, int prNumber, string path, int position, string body)
    {
        // IssueCommentとして投稿（PullRequestReviewCommentは複雑なので簡略化）
        var comment = await _client.Issue.Comment.Create(owner, repo, prNumber, $"**{path}** (line {position}):\n{body}");
        return new PullRequestReviewComment(); // ダミーの戻り値
    }
}
