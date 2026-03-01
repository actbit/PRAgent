namespace PRAgent.Services;

/// <summary>
/// 詳細な行コメントを作するエージェントのインターフェース
/// </summary>
public interface IDetailedCommentAgent
{
    /// <summary>
    /// レビュー結果から詳細な行コメントを作成
    /// </summary>
    /// <param name="review">レビュー結果の文字列</param>
    /// <param name="language">出力言語</param>
    /// <returns>行コメントのリスト</returns>
    Task<List<LineCommentData>> CreateCommentsAsync(string review, string language);

    /// <summary>
    /// 言語を動的に設定
    /// </summary>
    void SetLanguage(string language);
}

/// <summary>
/// 行コメントデータ/// </summary>
public class LineCommentData
{
    public required string Path { get; init; }
    public required int Line { get; init; }
    public required string Body { get; init; }
    public string? Suggestion { get; init; }
}
