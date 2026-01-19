namespace PRAgent.Models;

/// <summary>
/// PRアクションを蓄積するバッファクラス
/// エージェントが実行中にコメントやサマリーを追加し、最後にまとめて投稿できる
/// </summary>
public class PRActionBuffer
{
    private readonly List<LineCommentAction> _lineComments = new();
    private readonly List<ReviewCommentAction> _reviewComments = new();
    private readonly List<string> _summaries = new();
    private string? _generalComment;
    private bool _shouldApprove;
    private string? _approvalComment;

    /// <summary>
    /// 行コメントを追加します
    /// </summary>
    public void AddLineComment(string filePath, int lineNumber, string comment, string? suggestion = null)
    {
        _lineComments.Add(new LineCommentAction
        {
            FilePath = filePath,
            LineNumber = lineNumber,
            Comment = comment,
            Suggestion = suggestion
        });
    }

    /// <summary>
    /// レビューコメントを追加します
    /// </summary>
    public void AddReviewComment(string comment)
    {
        _reviewComments.Add(new ReviewCommentAction
        {
            Comment = comment
        });
    }

    /// <summary>
    /// サマリーを追加します
    /// </summary>
    public void AddSummary(string summary)
    {
        _summaries.Add(summary);
    }

    /// <summary>
    /// 全体コメントを設定します
    /// </summary>
    public void SetGeneralComment(string comment)
    {
        _generalComment = comment;
    }

    /// <summary>
    /// 承認をマークします
    /// </summary>
    public void MarkForApproval(string? comment = null)
    {
        _shouldApprove = true;
        _approvalComment = comment;
    }

    /// <summary>
    /// バッファをクリアします
    /// </summary>
    public void Clear()
    {
        _lineComments.Clear();
        _reviewComments.Clear();
        _summaries.Clear();
        _generalComment = null;
        _shouldApprove = false;
        _approvalComment = null;
    }

    /// <summary>
    /// バッファの状態を取得します
    /// </summary>
    public PRActionState GetState()
    {
        return new PRActionState
        {
            LineCommentCount = _lineComments.Count,
            ReviewCommentCount = _reviewComments.Count,
            SummaryCount = _summaries.Count,
            HasGeneralComment = !string.IsNullOrEmpty(_generalComment),
            ShouldApprove = _shouldApprove
        };
    }

    /// <summary>
    /// 蓄積されたアクションを実行するためのデータを取得します
    /// </summary>
    public IReadOnlyList<LineCommentAction> LineComments => _lineComments.AsReadOnly();
    public IReadOnlyList<ReviewCommentAction> ReviewComments => _reviewComments.AsReadOnly();
    public IReadOnlyList<string> Summaries => _summaries.AsReadOnly();
    public string? GeneralComment => _generalComment;
    public bool ShouldApprove => _shouldApprove;
    public string? ApprovalComment => _approvalComment;
}

/// <summary>
/// 行コメントアクション
/// </summary>
public class LineCommentAction
{
    public required string FilePath { get; init; }
    public required int LineNumber { get; init; }
    public required string Comment { get; init; }
    public string? Suggestion { get; init; }
}

/// <summary>
/// レビューコメントアクション
/// </summary>
public class ReviewCommentAction
{
    public required string Comment { get; init; }
}

/// <summary>
/// PRアクションの状態
/// </summary>
public class PRActionState
{
    public int LineCommentCount { get; init; }
    public int ReviewCommentCount { get; init; }
    public int SummaryCount { get; init; }
    public bool HasGeneralComment { get; init; }
    public bool ShouldApprove { get; init; }
}
