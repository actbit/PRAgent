using System.Text.RegularExpressions;
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

    /// <summary>
    /// ファイルのdiffから行番号に対応するdiff positionを計算します
    /// </summary>
    /// <param name="patch">ファイルのdiffパッチ</param>
    /// <param="lineNumber">ファイル内の行番号（1ベース）</param>
    /// <returns>diff position（1ベース）、見つからない場合はnull</returns>
    private static int? CalculateDiffPosition(string? patch, int lineNumber)
    {
        if (string.IsNullOrEmpty(patch))
            return null;

        var lines = patch.Split('\n');
        int position = 0;
        int currentNewLine = 0;

        foreach (var line in lines)
        {
            position++;

            // Hunk headerを解析: @@ -start_old,count +start_new,count @@ heading
            var hunkMatch = Regex.Match(line, @"^@@\s+-\d+(?:,\d+)?\s+\+(\d+)(?:,\d+)?\s+@@");
            if (hunkMatch.Success)
            {
                // 開始行番号の1つ前に設定（次の行でインクリメントして正しい行番号になるように）
                currentNewLine = int.Parse(hunkMatch.Groups[1].Value) - 1;
                continue;
            }

            // 行のタイプを判定
            if (line.StartsWith("+"))
            {
                // 追加行: 新しいファイルの行番号が増える
                currentNewLine++;
                if (currentNewLine == lineNumber)
                {
                    return position;
                }
            }
            else if (line.StartsWith("-"))
            {
                // 削除行: 新しいファイルの行番号は変わらない
                // 削除行にはコメントできないのでスキップ
            }
            else if (line.StartsWith(" ") || (line.Length == 0 && position < lines.Length))
            {
                // コンテキスト行
                // 空行はdiffの最後のアーティファクト（Splitの結果）を除く
                currentNewLine++;
                if (currentNewLine == lineNumber)
                {
                    return position;
                }
            }
            // \ No newline at end of file などのメタ行はスキップ
        }

        return null;
    }

    /// <summary>
    /// PRのファイルのdiffを取得します
    /// </summary>
    private async Task<string?> GetFilePatchAsync(string owner, string repo, int prNumber, string filePath)
    {
        var files = await _client.PullRequest.Files(owner, repo, prNumber);
        var file = files.FirstOrDefault(f => f.FileName == filePath);
        return file?.Patch;
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
    /// レビュー本文、行コメント、承認ステータスをまとめて1つのReviewとして投稿します
    /// </summary>
    public async Task<PullRequestReview> CreateCompleteReviewAsync(
        string owner,
        string repo,
        int prNumber,
        string? reviewBody,
        List<DraftPullRequestReviewComment> comments,
        bool approve = false)
    {
        var review = new PullRequestReviewCreate()
        {
            Body = reviewBody ?? string.Empty,
            Event = approve ? PullRequestReviewEvent.Approve : PullRequestReviewEvent.Comment,
            Comments = comments
        };

        return await _client.PullRequest.Review.Create(owner, repo, prNumber, review);
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

        // diffを取得してpositionを計算
        var patch = await GetFilePatchAsync(owner, repo, prNumber, filePath);
        var position = CalculateDiffPosition(patch, lineNumber);

        if (!position.HasValue)
        {
            throw new ArgumentException($"Could not find line {lineNumber} in diff for file {filePath}. The line may not be part of the changes.");
        }

        return await _client.PullRequest.Review.Create(
            owner,
            repo,
            prNumber,
            new PullRequestReviewCreate
            {
                Event = PullRequestReviewEvent.Comment,
                Comments = new List<DraftPullRequestReviewComment>
                {
                    new DraftPullRequestReviewComment(commentBody, filePath, position.Value)
                }
            }
        );
    }

    public async Task<PullRequestReview> CreateMultipleLineCommentsAsync(string owner, string repo, int prNumber, List<(string FilePath, int? LineNumber, int? StartLine, int? EndLine, string Comment, string? Suggestion)> comments)
    {
        // ファイルごとのdiffをキャッシュ
        var patchCache = new Dictionary<string, string?>();

        var draftComments = new List<DraftPullRequestReviewComment>();
        var errors = new List<string>();

        foreach (var c in comments)
        {
            var commentBody = c.Suggestion != null ? $"{c.Comment}\n```suggestion\n{c.Suggestion}\n```" : c.Comment;

            int targetLine;
            if (c.LineNumber.HasValue)
            {
                targetLine = c.LineNumber.Value;
            }
            else if (c.StartLine.HasValue)
            {
                targetLine = c.StartLine.Value;
            }
            else
            {
                errors.Add($"Comment must have either LineNumber or StartLine: {c.FilePath}");
                continue;
            }

            // diffを取得（キャッシュを使用）
            if (!patchCache.TryGetValue(c.FilePath, out var patch))
            {
                patch = await GetFilePatchAsync(owner, repo, prNumber, c.FilePath);
                patchCache[c.FilePath] = patch;
            }

            // positionを計算
            var position = CalculateDiffPosition(patch, targetLine);
            if (!position.HasValue)
            {
                errors.Add($"Could not find line {targetLine} in diff for file {c.FilePath}");
                continue;
            }

            draftComments.Add(new DraftPullRequestReviewComment(commentBody, c.FilePath, position.Value));
        }

        if (errors.Count > 0 && draftComments.Count == 0)
        {
            throw new ArgumentException($"Failed to create any comments: {string.Join("; ", errors)}");
        }

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
