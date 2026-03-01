using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using PRAgent.Services;

namespace PRAgent.Agents;

/// <summary>
/// 詳細な行コメントを作成するサブエージェント
/// ReviewAgentからFunction Callingで呼び出され、個々の問題について詳細なコメントを生成
/// </summary>
public class DetailedCommentAgent : IDetailedCommentAgent
{
    private readonly IKernelService _kernelService;
    private readonly ILogger<DetailedCommentAgent> _logger;
    private string _language = "en";

    public DetailedCommentAgent(
        IKernelService kernelService,
        ILogger<DetailedCommentAgent> logger)
    {
        _kernelService = kernelService;
        _logger = logger;
    }

    /// <summary>
    /// 言語を動的に設定
    /// </summary>
    public void SetLanguage(string language)
    {
        _language = language;
    }

    /// <summary>
    /// 個々の問題について詳細なコメントを生成（Function Calling用）
    /// </summary>
    /// <param name="filePath">ファイルパス</param>
    /// <param name="lineNumber">行番号</param>
    /// <param name="codeSnippet">周辺コード</param>
    /// <param name="issueSummary">問題の概要</param>
    /// <returns>詳細なコメント（JSON形式）</returns>
    [KernelFunction("get_detailed_comment")]
    public async Task<string> GetDetailedCommentAsync(
        string filePath,
        int lineNumber,
        string codeSnippet,
        string issueSummary)
    {
        var systemPrompt = GetDetailedCommentPrompt(_language);
        var kernel = _kernelService.CreateAgentKernel(systemPrompt);

        var prompt = $$"""
            以下のコードの問題点について、GitHubのプルリクエストレビュー用の詳細なコメントを作成してください。

            ## ファイル情報
            - ファイルパス: {{filePath}}
            - 行番号: {{lineNumber}}

            ## 対象コード
            ```
            {{codeSnippet}}
            ```

            ## 問題の概要
            {{issueSummary}}

            ## 出力形式（JSON）
            以下の形式で出力してください：
            ```json
            {
              "path": "{{filePath}}",
              "line": {{lineNumber}},
              "body": "詳細なコメント本文",
              "suggestion": "修正提案のコード（あれば）"
            }
            ```

            重要: 必ず有効なJSONオブジェクトのみを出力してください。
            """;

        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        var chatHistory = new ChatHistory();
        chatHistory.AddUserMessage(prompt);

        var response = await chatService.GetChatMessageContentsAsync(chatHistory, executionSettings: null, kernel);
        var responseText = response.FirstOrDefault()?.Content ?? "{}";

        _logger.LogInformation("=== DetailedCommentAgent Response for {File}:{Line} ===\n{Response}",
            filePath, lineNumber, responseText);

        return responseText;
    }

    /// <summary>
    /// 複数の問題について一括で詳細コメントを生成（バッチ処理用）
    /// </summary>
    public async Task<List<LineCommentData>> CreateDetailedCommentsAsync(
        List<IssueContext> issues,
        string language)
    {
        SetLanguage(language);
        var results = new List<LineCommentData>();

        foreach (var issue in issues)
        {
            try
            {
                var jsonResult = await GetDetailedCommentAsync(
                    issue.FilePath,
                    issue.LineNumber,
                    issue.CodeSnippet,
                    issue.IssueSummary);

                var comment = ParseSingleCommentFromJson(jsonResult);
                if (comment != null)
                {
                    results.Add(comment);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create detailed comment for {File}:{Line}",
                    issue.FilePath, issue.LineNumber);
            }
        }

        return results;
    }

    /// <summary>
    /// レビュー概要から詳細な行コメントを作成（従来のメソッド、互換性のため残す）
    /// </summary>
    public async Task<List<LineCommentData>> CreateCommentsAsync(string reviewOverview, string language)
    {
        SetLanguage(language);

        var systemPrompt = GetDetailedCommentPrompt(_language);
        var kernel = _kernelService.CreateAgentKernel(systemPrompt);

        var prompt = $$"""
            以下のレビュー概要に基づいて、GitHubのプルリクエストレビュー用の詳細な行コメントを作成してください。

            ## レビュー概要
            {{reviewOverview}}

            ## 指示
            1. 各問題点に対して、ファイルパス、行番号、コメント本文を抽出
            2. コメントは簡潔かつ建設的に
            3. 修正提案がある場合は suggestion ブロックを含める

            ## 出力形式（JSON）
            ```json
            [
              {
                "path": "src/File.cs",
                "line": 45,
                "body": "ここでメモリリークが発生する可能性があります。using文を使用してください。",
                "suggestion": "using var resource = ...;"
              }
            ]
            ```

            重要: 必ず有効なJSON配列のみを出力してください。
            """;

        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        var chatHistory = new ChatHistory();
        chatHistory.AddUserMessage(prompt);

        var response = await chatService.GetChatMessageContentsAsync(chatHistory, executionSettings: null, kernel);
        var responseText = response.FirstOrDefault()?.Content ?? "[]";

        _logger.LogInformation("=== DetailedCommentAgent Response ===\n{Response}", responseText);

        return ParseCommentsFromJson(responseText);
    }

    /// <summary>
    /// 単一のJSONオブジェクトから行コメントデータをパース
    /// </summary>
    private LineCommentData? ParseSingleCommentFromJson(string json)
    {
        try
        {
            // コードブロック内のJSONを探す
            var codeBlockMatch = System.Text.RegularExpressions.Regex.Match(json, @"```(?:json)?\s*([\s\S]*?)```");
            if (codeBlockMatch.Success)
            {
                json = codeBlockMatch.Groups[1].Value.Trim();
            }

            // JSONオブジェクトを探す
            var jsonMatch = System.Text.RegularExpressions.Regex.Match(json, @"\{[\s\S]*?\}");
            if (!jsonMatch.Success)
            {
                _logger.LogWarning("No valid JSON object found in response");
                return null;
            }

            return System.Text.Json.JsonSerializer.Deserialize<LineCommentData>(jsonMatch.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse single comment from JSON");
            return null;
        }
    }

    /// <summary>
    /// JSON配列から行コメントデータをパース
    /// </summary>
    private List<LineCommentData> ParseCommentsFromJson(string json)
    {
        try
        {
            // まずコードブロック内のJSONを探す
            var codeBlockMatch = System.Text.RegularExpressions.Regex.Match(json, @"```(?:json)?\s*([\s\S]*?)```");
            if (codeBlockMatch.Success)
            {
                json = codeBlockMatch.Groups[1].Value.Trim();
            }

            // JSON配列を探す（非貪欲マッチ）
            var jsonMatch = System.Text.RegularExpressions.Regex.Match(json, @"\[[\s\S]*?\]");
            if (!jsonMatch.Success)
            {
                _logger.LogWarning("No valid JSON array found in response");
                return new List<LineCommentData>();
            }

            var jsonArray = jsonMatch.Value;
            var comments = System.Text.Json.JsonSerializer.Deserialize<List<LineCommentData>>(jsonArray);

            return comments ?? new List<LineCommentData>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse comments from JSON");
            return new List<LineCommentData>();
        }
    }

    /// <summary>
    /// 言語に応じた詳細コメント用プロンプトを取得
    /// </summary>
    private static string GetDetailedCommentPrompt(string language)
    {
        var isJapanese = language?.ToLowerInvariant() == "ja";

        if (isJapanese)
        {
            return """
                あなたはGitHubのプルリクエストレビュー用の詳細コメントを作成する専門家です。

                ## 役割
                指定されたコードの問題点について、詳細なコメントを生成

                ## 出力ルール
                1. 有効なJSONオブジェクトのみを出力
                2. コメントには以下を含める:
                   - path: ファイルパス（入力と同じ値）
                   - line: 行番号（入力と同じ値）
                   - body: 詳細なコメント本文
                   - suggestion: 修正提案（オプション）
                3. コメントは建設的で、修正方法を具体的に提示
                4. コードスニペットを参照して、具体的な改善案を提示
                """;
        }
        else
        {
            return """
                You are an expert at creating detailed line comments for GitHub pull request reviews.

                ## Your Role
                Generate detailed comments for specified code issues.

                ## Output Rules
                1. Output only valid JSON object
                2. Include in comment:
                   - path: file path (same as input)
                   - line: line number (same as input)
                   - body: detailed comment body
                   - suggestion: fix suggestion (optional)
                3. Keep comments constructive with specific improvement suggestions
                4. Reference the code snippet and provide concrete fixes
                """;
        }
    }
}
