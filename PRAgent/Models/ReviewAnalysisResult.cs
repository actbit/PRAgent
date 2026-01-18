using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PRAgent.ReviewModels;

/// <summary>
/// レビュー分析結果
/// </summary>
public class ReviewAnalysisResult
{
    /// <summary>
    /// 検出された問題点リスト
    /// </summary>
    [JsonPropertyName("issues")]
    public List<ReviewIssue> Issues { get; set; } = new();

    /// <summary>
    /// レビューのサマリー
    /// </summary>
    [JsonPropertyName("reviewSummary")]
    public string ReviewSummary { get; set; } = string.Empty;
}