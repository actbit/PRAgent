using System.Text.Json.Serialization;

namespace PRAgent.ReviewModels;

/// <summary>
/// レビュー問題点
/// </summary>
public class ReviewIssue
{
    /// <summary>
    /// タイトル
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// レベル
    /// </summary>
    [JsonPropertyName("level")]
    public string Level { get; set; } = "INFO";

    /// <summary>
    /// 説明
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// ファイルパス
    /// </summary>
    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// 開始行
    /// </summary>
    [JsonPropertyName("startLine")]
    public int? StartLine { get; set; }

    /// <summary>
    /// 終了行
    /// </summary>
    [JsonPropertyName("endLine")]
    public int? EndLine { get; set; }

    /// <summary>
    /// 修正提案
    /// </summary>
    [JsonPropertyName("suggestion")]
    public string Suggestion { get; set; } = string.Empty;
}