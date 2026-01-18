using System.Collections.Generic;
using PRAgent.ReviewModels;

namespace PRAgent.Models;

/// <summary>
/// レビュー結果のモデル
/// </summary>
public class ReviewResult
{
    /// <summary>
    /// レビュー本文
    /// </summary>
    public string Review { get; set; } = string.Empty;

    /// <summary>
    /// 生成されたコメントリスト
    /// </summary>
    public List<DraftPullRequestReviewComment> Comments { get; set; } = new();

    /// <summary>
    /// 分析結果
    /// </summary>
    public ReviewAnalysisResult AnalysisResult { get; set; } = new();
}