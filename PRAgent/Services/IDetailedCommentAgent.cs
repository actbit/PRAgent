namespace PRAgent.Services;

/// <summary>
/// 詳細な行コメントを作成するエージェントのインターフェース
/// </summary>
public interface IDetailedCommentAgent
{
    /// <summary>
    /// 言語を設定
    /// </summary>
    void SetLanguage(string language);

    /// <summary>
    /// 個々の問題について詳細なコメントを生成（Function Calling用）
    /// </summary>
    /// <param name="filePath">ファイルパス</param>
    /// <param name="lineNumber">行番号</param>
    /// <param name="codeSnippet">周辺コード</param>
    /// <param name="issueSummary">問題の概要</param>
    /// <returns>詳細なコメント（JSON形式）</returns>
    Task<string> GetDetailedCommentAsync(string filePath, int lineNumber, string codeSnippet, string issueSummary);

    /// <summary>
    /// 複数の問題について一括で詳細コメントを生成
    /// </summary>
    /// <param name="issues">問題のリスト</param>
    /// <param name="language">出力言語</param>
    /// <returns>行コメントのリスト</returns>
    Task<List<LineCommentData>> CreateDetailedCommentsAsync(List<IssueContext> issues, string language);

    /// <summary>
    /// レビュー概要から詳細な行コメントを作成（従来のメソッド）
    /// </summary>
    /// <param name="reviewOverview">レビュー結果の文字列</param>
    /// <param name="language">出力言語</param>
    /// <returns>行コメントのリスト</returns>
    Task<List<LineCommentData>> CreateCommentsAsync(string reviewOverview, string language);
}

/// <summary>
/// 行コメントデータ
/// </summary>
public class LineCommentData
{
    public required string Path { get; init; }
    public required int Line { get; init; }
    public required string Body { get; init; }
    public string? Suggestion { get; init; }
}

/// <summary>
/// 問題のコンテキスト情報
/// </summary>
public class IssueContext
{
    public required string FilePath { get; init; }
    public required int LineNumber { get; init; }
    public required string CodeSnippet { get; init; }
    public required string IssueSummary { get; init; }
}
