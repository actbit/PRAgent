using System.Text.Json.Serialization;

namespace PRAgent.ReviewModels;

/// <summary>
/// ドラフトプルリクエストレビューコメント
/// </summary>
public class DraftPullRequestReviewComment
{
    /// <summary>
    /// ファイルパス
    /// </summary>
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// コメント本文
    /// </summary>
    [JsonPropertyName("body")]
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// 行番号（position）
    /// </summary>
    [JsonPropertyName("position")]
    public int? Position { get; set; }

    /// <summary>
    /// 開始行（範囲コメント用）
    /// </summary>
    [JsonPropertyName("start_line")]
    public int? StartLine { get; set; }

    /// <summary>
    /// 終了行（範囲コメント用）
    /// </summary>
    [JsonPropertyName("line")]
    public int? Line { get; set; }

    public DraftPullRequestReviewComment() { }

    public DraftPullRequestReviewComment(string path, string body, int? position = null)
    {
        Path = path;
        Body = body;
        Position = position;
    }

    public DraftPullRequestReviewComment(string path, string body, int? startLine, int? line)
    {
        Path = path;
        Body = body;
        StartLine = startLine;
        Line = line;
    }
}