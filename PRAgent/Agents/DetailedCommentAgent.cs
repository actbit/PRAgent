using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using PRAgent.Models;
using PRAgent.Services;

namespace PRAgent.Agents;

/// <summary>
/// 詳細な行コメントを作成するサブエージェント
/// ReviewAgentが作成した概要から、具体的な行コメントを生成
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
    /// レビュー概要から詳細な行コメントを作成
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

        // JSONをパース
        return ParseCommentsFromJson(responseText);
    }

    /// <summary>
    /// JSONから行コメントデータをパース
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
                レビュー概要から、具体的な行コメントを抽出・生成

                ## 出力ルール
                1. 有効なJSON配列のみを出力
                2. 各コメントには以下を含める:
                   - path: ファイルパス
                   - line: 行番号
                   - body: コメント本文
                   - suggestion: 修正提案（オプション）
                3. コメントは簡潔かつ建設的に
                """;
        }
        else
        {
            return """
                You are an expert at creating detailed line comments for GitHub pull request reviews.

                ## Your Role
                Extract and generate specific line comments from review overview.

                ## Output Rules
                1. Output only valid JSON array
                2. Each comment should include:
                   - path: file path
                   - line: line number
                   - body: comment body
                   - suggestion: fix suggestion (optional)
                3. Keep comments concise and constructive
                """;
        }
    }
}
