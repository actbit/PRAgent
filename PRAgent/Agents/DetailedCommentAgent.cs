using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;
using Octokit;
using PRAgent.Models;
using PRAgent.Services;

namespace PRAgent.Agents;

/// <summary>
/// 詳細な行コメントを作成するサブエージェント
/// Tool呼び出しベースで各問題点ごとにコメントを作成
/// </summary>
public class DetailedCommentAgent : ReviewAgentBase, IDetailedCommentAgent
{
    private readonly ILogger<DetailedCommentAgent> _logger;

    public DetailedCommentAgent(
        IKernelService kernelService,
        IGitHubService gitHubService,
        PullRequestDataService prDataService,
        AISettings aiSettings,
        ILogger<DetailedCommentAgent> logger,
        string? customSystemPrompt = null)
        : base(kernelService, gitHubService, prDataService, aiSettings, AgentDefinition.DetailedCommentAgent, customSystemPrompt)
    {
        _logger = logger;
    }

    /// <summary>
    /// 言語を動的に設定
    /// </summary>
    public new void SetLanguage(string language) => base.SetLanguage(language);

    /// <summary>
    /// レビュー結果から詳細な行コメントを作成
    /// </summary>
    public async Task<List<DraftPullRequestReviewComment>> CreateCommentsAsync(string review, string language)
    {
        SetLanguage(language);

        // レビュー内容から問題点を抽出
        var issues = ExtractIssuesFromReview(review);

        // 各問題点に対してTool呼び出しでコメントを作成
        var comments = new List<DraftPullRequestReviewComment>();

        foreach (var issue in issues)
        {
            var comment = await CreateCommentForIssueAsync(issue, language);
            if (comment != null)
            {
                comments.Add(comment);
            }
        }

        return comments;
    }

    /// <summary>
    /// レビューから問題点を抽出
    /// </summary>
    private List<ReviewIssue> ExtractIssuesFromReview(string review)
    {
        var issues = new List<ReviewIssue>();

        // セクションごとに分割
        var sections = review.Split(new[] { "\n\n### ", "\n## ", "\n##\n\n" }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var section in sections)
        {
            // セクションタイトルを解析
            var titleMatch = System.Text.RegularExpressions.Regex.Match(section, @"^(###\s*\[([A-Z]+)\])?\s*(.+)");
            if (titleMatch.Success)
            {
                var levelStr = titleMatch.Groups[2].Value;
                var title = titleMatch.Groups[3].Value.Trim();

                // レベルを判定
                var level = levelStr switch
                {
                    "CRITICAL" => Severity.Critical,
                    "MAJOR" => Severity.Major,
                    "MINOR" => Severity.Minor,
                    "POSITIVE" => Severity.Positive,
                    _ => Severity.Major
                };

                // ファイルパスを抽出
                var pathMatch = System.Text.RegularExpressions.Regex.Match(section, @"\*\*ファイル:\*\*`([^`]+)`");
                var path = pathMatch.Success ? pathMatch.Groups[1].Value : "src/File.cs";

                // 行番号を抽出
                var lineMatch = System.Text.RegularExpressions.Regex.Match(section, @"\(lines?\s*(\d+)(?:-(\d+))?\)");
                int? startLine = null;
                int? endLine = null;

                if (lineMatch.Success)
                {
                    startLine = int.Parse(lineMatch.Groups[1].Value);
                    if (lineMatch.Groups[2].Success)
                    {
                        endLine = int.Parse(lineMatch.Groups[2].Value);
                    }
                }

                // 問題説明を抽出
                var problemSection = System.Text.RegularExpressions.Regex.Split(section, @"\n\*\*\w+:\*\*")[1];
                var problem = problemSection.Split('\n')[0].Trim();

                // 修正提案を抽出
                var suggestion = "";
                var suggestionMatch = System.Text.RegularExpressions.Regex.Match(section, @"```suggestion\s*\n?([^\n`]+)");
                if (suggestionMatch.Success)
                {
                    suggestion = suggestionMatch.Groups[1].Value.Trim();
                }

                issues.Add(new ReviewIssue
                {
                    Title = title,
                    Level = level,
                    FilePath = path,
                    StartLine = startLine ?? 1,
                    EndLine = endLine ?? 1,
                    Description = problem,
                    Suggestion = suggestion
                });
            }
        }

        return issues;
    }

    /// <summary>
    /// 各問題点に対してコメントを作成
    /// </summary>
    private async Task<DraftPullRequestReviewComment?> CreateCommentForIssueAsync(ReviewIssue issue, string language)
    {
        try
        {
            var prompt = CreateCommentPrompt(issue, language);

            // プロンプットを出力
            _logger.LogInformation("=== DetailedCommentAgent Prompt for Issue ===\n{Prompt}", prompt);

            var aiResponse = await KernelService.InvokePromptAsStringAsync(CreateKernel(), prompt);

            _logger.LogInformation("=== DetailedCommentAgent Response ===\n{Response}", aiResponse);

            return new DraftPullRequestReviewComment(issue.FilePath, aiResponse, issue.StartLine);
        }
        catch
        {
            // フォールバック：簡潔なコメントを作成
            return new DraftPullRequestReviewComment(
                issue.FilePath,
                $"{issue.Level}: {issue.Description}\n\n{issue.Suggestion}",
                issue.StartLine);
        }
    }

    /// <summary>
    /// コメント生成用のプロンプトを作成
    /// </summary>
    private string CreateCommentPrompt(ReviewIssue issue, string language)
    {
        return $$"""
            Create a detailed GitHub pull request review comment for this issue:

            **Issue Title:** {{issue.Title}}
            **Level:** {{issue.Level}}
            **File:** {{issue.FilePath}} (Line {{issue.StartLine}})
            **Description:** {{issue.Description}}
            **Suggestion:** {{issue.Suggestion}}

            Create a concise comment that:
            1. Clearly describes the problem
            2. Provides actionable feedback
            3. Uses professional and constructive language
            4. Keep it under 200 words

            **Output:** Only the comment text, no formatting.
            """;
    }
}

/// <summary>
/// レビュー問題のデータモデル
/// </summary>
public class ReviewIssue
{
    public string Title { get; set; } = string.Empty;
    public Severity Level { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public int StartLine { get; set; }
    public int EndLine { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Suggestion { get; set; } = string.Empty;
}

/// <summary>
/// レベルの列挙型
/// </summary>
public enum Severity
{
    Critical,
    Major,
    Minor,
    Positive
}