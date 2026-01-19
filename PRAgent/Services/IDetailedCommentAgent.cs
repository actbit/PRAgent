using Octokit;

namespace PRAgent.Services;

/// <summary>
/// 詳細な行コメントを作成するエージェントのインターフェース
/// </summary>
public interface IDetailedCommentAgent
{
    /// <summary>
    /// レビュー結果から詳細な行コメントを作成
    /// </summary>
    /// <param name="review">レビュー結果の文字列</param>
    /// <param name="language">出力言語</param>
    /// <returns>GitHubレビュー用のコメントリスト</returns>
    Task<List<DraftPullRequestReviewComment>> CreateCommentsAsync(string review, string language);

    /// <summary>
    /// 言語を動的に設定
    /// </summary>
    void SetLanguage(string language);
}