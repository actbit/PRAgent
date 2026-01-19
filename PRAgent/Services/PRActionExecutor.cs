using Octokit;
using PRAgent.Models;
using PRAgent.Services;

namespace PRAgent.Services;

/// <summary>
/// 蓄積されたPRアクションをGitHubに投稿するサービス
/// </summary>
public class PRActionExecutor
{
    private readonly IGitHubService _gitHubService;
    private readonly string _owner;
    private readonly string _repo;
    private readonly int _prNumber;

    public PRActionExecutor(
        IGitHubService gitHubService,
        string owner,
        string repo,
        int prNumber)
    {
        _gitHubService = gitHubService;
        _owner = owner;
        _repo = repo;
        _prNumber = prNumber;
    }

    /// <summary>
    /// バッファ内のすべてのアクションをGitHubに投稿します
    /// </summary>
    public async Task<PRActionResult> ExecuteAsync(PRActionBuffer buffer, CancellationToken cancellationToken = default)
    {
        var result = new PRActionResult
        {
            Owner = _owner,
            Repo = _repo,
            PrNumber = _prNumber
        };

        try
        {
            // 1. レビューコメントを投稿
            if (buffer.ReviewComments.Count > 0)
            {
                foreach (var reviewComment in buffer.ReviewComments)
                {
                    await _gitHubService.CreateReviewCommentAsync(
                        _owner, _repo, _prNumber, reviewComment.Comment);
                }
                result.ReviewCommentsPosted = buffer.ReviewComments.Count;
            }

            // 2. 行コメントを投稿
            if (buffer.LineComments.Count > 0)
            {
                var comments = buffer.LineComments.Select(c => (
                    c.FilePath,
                    c.LineNumber,
                    c.StartLine,
                    c.EndLine,
                    c.Comment,
                    c.Suggestion
                )).ToList();

                var reviewResult = await _gitHubService.CreateMultipleLineCommentsAsync(
                    _owner, _repo, _prNumber, comments);

                result.LineCommentsPosted = comments.Count;
            }

            // 3. サマリーを全体コメントとして投稿
            if (buffer.Summaries.Count > 0)
            {
                var summaryText = string.Join("\n\n", buffer.Summaries);
                var commentResult = await _gitHubService.CreateIssueCommentAsync(
                    _owner, _repo, _prNumber,
                    $@"## PR Summary

{summaryText}");

                result.SummariesPosted = buffer.Summaries.Count;
                result.SummaryCommentUrl = commentResult.HtmlUrl;
            }

            // 4. 全体コメントを投稿
            if (!string.IsNullOrEmpty(buffer.GeneralComment))
            {
                var commentResult = await _gitHubService.CreateIssueCommentAsync(
                    _owner, _repo, _prNumber, buffer.GeneralComment);

                result.GeneralCommentPosted = true;
                result.GeneralCommentUrl = commentResult.HtmlUrl;
            }

            // 5. 承認ステータスに応じた処理を実行
            switch (buffer.ApprovalState)
            {
                case PRApprovalState.Approved:
                    var approvalResult = await _gitHubService.ApprovePullRequestAsync(
                        _owner, _repo, _prNumber, buffer.ApprovalComment);

                    result.Approved = true;
                    result.ApprovalState = PRApprovalState.Approved;
                    result.ApprovalUrl = approvalResult.HtmlUrl;
                    break;

                case PRApprovalState.ChangesRequested:
                    // 変更依頼をレビューコメントとして投稿
                    var changesComment = $"## Changes Requested\n\n{buffer.ApprovalComment ?? "Please address the issues mentioned in the review."}";
                    await _gitHubService.CreateReviewCommentAsync(
                        _owner, _repo, _prNumber, changesComment);

                    result.ApprovalState = PRApprovalState.ChangesRequested;
                    result.ChangesRequested = true;
                    break;

                case PRApprovalState.None:
                    // 何もしない（コメントのみ）
                    break;
            }

            result.TotalActionsPosted =
                result.ReviewCommentsPosted +
                result.LineCommentsPosted +
                result.SummariesPosted +
                (result.GeneralCommentPosted ? 1 : 0) +
                (result.Approved ? 1 : 0) +
                (result.ChangesRequested ? 1 : 0);

            result.Message = $"Successfully posted {result.TotalActionsPosted} action(s) to PR #{_prNumber}";
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
            result.Message = $"Failed to post actions: {ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// アクションをGitHubに投稿する前に確認するためのサマリーを作成します
    /// </summary>
    public string CreatePreview(PRActionBuffer buffer)
    {
        var preview = $"""
            ## PR # {_prNumber} に投稿されるアクションのプレビュー

            """;

        if (buffer.LineComments.Count > 0)
        {
            preview += $"### 行コメント ({buffer.LineComments.Count}件)\n";
            foreach (var comment in buffer.LineComments)
            {
                var suggestion = !string.IsNullOrEmpty(comment.Suggestion)
                    ? $"\n  提案: {comment.Suggestion}"
                    : "";
                preview += $"- {comment.FilePath}:{comment.LineNumber}: {comment.Comment}{suggestion}\n";
            }
            preview += "\n";
        }

        if (buffer.Summaries.Count > 0)
        {
            preview += $"### サマリー ({buffer.Summaries.Count}件)\n";
            foreach (var summary in buffer.Summaries)
            {
                preview += $"- {summary.Substring(0, Math.Min(100, summary.Length))}...\n";
            }
            preview += "\n";
        }

        if (!string.IsNullOrEmpty(buffer.GeneralComment))
        {
            preview += $"### 全体コメント\n{buffer.GeneralComment.Substring(0, Math.Min(200, buffer.GeneralComment.Length))}...\n\n";
        }

        // 承認ステータスに応じた表示
        switch (buffer.ApprovalState)
        {
            case PRApprovalState.Approved:
                preview += $"### 承認\nはい - {buffer.ApprovalComment ?? "コメントなし"}\n\n";
                break;

            case PRApprovalState.ChangesRequested:
                preview += $"### 変更依頼\nはい - {buffer.ApprovalComment ?? "コメントなし"}\n\n";
                break;

            case PRApprovalState.None:
                // 何も表示しない（コメントのみ）
                break;
        }

        var totalActions = buffer.LineComments.Count +
                          buffer.Summaries.Count +
                          (string.IsNullOrEmpty(buffer.GeneralComment) ? 0 : 1) +
                          (buffer.ApprovalState != PRApprovalState.None ? 1 : 0);

        preview += $"**合計: {totalActions}件のアクション**";

        return preview;
    }
}

/// <summary>
/// PRアクションの実行結果
/// </summary>
public class PRActionResult
{
    public string Owner { get; init; } = string.Empty;
    public string Repo { get; init; } = string.Empty;
    public int PrNumber { get; init; }
    public bool Success { get; set; }
    public int TotalActionsPosted { get; set; }
    public int ReviewCommentsPosted { get; set; }
    public int LineCommentsPosted { get; set; }
    public int SummariesPosted { get; set; }
    public bool GeneralCommentPosted { get; set; }
    public bool Approved { get; set; }
    public bool ChangesRequested { get; set; }
    public PRApprovalState? ApprovalState { get; set; }
    public string? SummaryCommentUrl { get; set; }
    public string? GeneralCommentUrl { get; set; }
    public string? ApprovalUrl { get; set; }
    public string? Error { get; set; }
    public string Message { get; set; } = string.Empty;
}
