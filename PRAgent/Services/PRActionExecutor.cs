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
            // ファイルごとのdiffをキャッシュ
            var patchCache = new Dictionary<string, string?>();
            var draftComments = new List<DraftPullRequestReviewComment>();
            var errors = new List<string>();

            // 行コメントを処理してDraftPullRequestReviewCommentに変換
            foreach (var lineComment in buffer.LineComments)
            {
                int targetLine;
                if (lineComment.LineNumber.HasValue)
                {
                    targetLine = lineComment.LineNumber.Value;
                }
                else if (lineComment.StartLine.HasValue)
                {
                    targetLine = lineComment.StartLine.Value;
                }
                else
                {
                    errors.Add($"Line comment must have either LineNumber or StartLine: {lineComment.FilePath}");
                    continue;
                }

                var commentBody = lineComment.Suggestion != null
                    ? $"{lineComment.Comment}\n```suggestion\n{lineComment.Suggestion}\n```"
                    : lineComment.Comment;

                // diffを取得（キャッシュを使用）
                if (!patchCache.TryGetValue(lineComment.FilePath, out var patch))
                {
                    patch = await GetFilePatchAsync(lineComment.FilePath);
                    patchCache[lineComment.FilePath] = patch;
                }

                // positionを計算
                var position = CalculateDiffPosition(patch, targetLine);
                if (!position.HasValue)
                {
                    errors.Add($"Could not find line {targetLine} in diff for file {lineComment.FilePath}");
                    continue;
                }

                draftComments.Add(new DraftPullRequestReviewComment(commentBody, lineComment.FilePath, position.Value));
            }

            // レビュー本文を作成
            var reviewBodyBuilder = new System.Text.StringBuilder();

            // レビューコメントを追加
            if (buffer.ReviewComments.Count > 0)
            {
                foreach (var reviewComment in buffer.ReviewComments)
                {
                    reviewBodyBuilder.AppendLine(reviewComment.Comment);
                    reviewBodyBuilder.AppendLine();
                }
            }

            // 承認コメントを追加
            if (!string.IsNullOrEmpty(buffer.ApprovalComment))
            {
                reviewBodyBuilder.AppendLine(buffer.ApprovalComment);
            }

            var reviewBody = reviewBodyBuilder.ToString().Trim();

            // 1つのReviewとしてまとめて投稿
            bool hasComments = draftComments.Count > 0 || !string.IsNullOrEmpty(reviewBody);
            bool shouldApprove = buffer.ApprovalState == PRApprovalState.Approved;

            if (hasComments || shouldApprove)
            {
                var reviewResult = await _gitHubService.CreateCompleteReviewAsync(
                    _owner,
                    _repo,
                    _prNumber,
                    reviewBody,
                    draftComments,
                    shouldApprove);

                result.ReviewCommentsPosted = buffer.ReviewComments.Count;
                result.LineCommentsPosted = draftComments.Count;

                if (shouldApprove)
                {
                    result.Approved = true;
                    result.ApprovalState = PRApprovalState.Approved;
                    result.ApprovalUrl = reviewResult.HtmlUrl;
                }
            }

            // 変更依頼の場合は別途投稿
            if (buffer.ApprovalState == PRApprovalState.ChangesRequested)
            {
                var changesComment = $"## Changes Requested\n\n{buffer.ApprovalComment ?? "Please address the issues mentioned in the review."}";
                await _gitHubService.CreateReviewCommentAsync(
                    _owner, _repo, _prNumber, changesComment);

                result.ApprovalState = PRApprovalState.ChangesRequested;
                result.ChangesRequested = true;
            }

            // サマリーを全体コメントとして投稿
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

            // 全体コメントを投稿
            if (!string.IsNullOrEmpty(buffer.GeneralComment))
            {
                var commentResult = await _gitHubService.CreateIssueCommentAsync(
                    _owner, _repo, _prNumber, buffer.GeneralComment);

                result.GeneralCommentPosted = true;
                result.GeneralCommentUrl = commentResult.HtmlUrl;
            }

            result.TotalActionsPosted =
                result.ReviewCommentsPosted +
                result.LineCommentsPosted +
                result.SummariesPosted +
                (result.GeneralCommentPosted ? 1 : 0) +
                (result.Approved ? 1 : 0) +
                (result.ChangesRequested ? 1 : 0);

            result.Message = $"Successfully posted {result.TotalActionsPosted} action(s) to PR #{_prNumber}";

            if (errors.Count > 0)
            {
                result.Message += $"\n\nWarnings: {string.Join("; ", errors)}";
            }
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
    /// ファイルのdiffを取得します
    /// </summary>
    private async Task<string?> GetFilePatchAsync(string filePath)
    {
        var files = await _gitHubService.GetPullRequestFilesAsync(_owner, _repo, _prNumber);
        var file = files.FirstOrDefault(f => f.FileName == filePath);
        return file?.Patch;
    }

    /// <summary>
    /// ファイルのdiffから行番号に対応するdiff positionを計算します
    /// </summary>
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
            var hunkMatch = System.Text.RegularExpressions.Regex.Match(line, @"^@@\s+-\d+(?:,\d+)?\s+\+(\d+)(?:,\d+)?\s+@@");
            if (hunkMatch.Success)
            {
                // 開始行番号の1つ前に設定（次の行でインクリメントして正しい行番号になるように）
                currentNewLine = int.Parse(hunkMatch.Groups[1].Value) - 1;
                continue;
            }

            // 行のタイプを判定
            if (line.StartsWith("+"))
            {
                currentNewLine++;
                if (currentNewLine == lineNumber)
                {
                    return position;
                }
            }
            else if (line.StartsWith("-"))
            {
                // 削除行: 新しいファイルの行番号は変わらない
            }
            else if (line.StartsWith(" ") || (line.Length == 0 && position < lines.Length))
            {
                // コンテキスト行（空行はdiffの最後のアーティファクトを除く）
                currentNewLine++;
                if (currentNewLine == lineNumber)
                {
                    return position;
                }
            }
        }

        return null;
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
