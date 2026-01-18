using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Octokit;
using PRAgent.Agents;
using PRAgent.Models;
using PRAgent.ReviewModels;
using PRAgent.Services;

namespace PRAgent.Services;

/// <summary>
/// レビュー分析関連のTool集
/// </summary>
public class ReviewAnalysisTools
{
    private readonly IGitHubService _gitHubService;
    private readonly PullRequestDataService _prDataService;
    private readonly ILogger<ReviewAnalysisTools> _logger;
    private string _language = "ja";

    public ReviewAnalysisTools(
        IGitHubService gitHubService,
        PullRequestDataService prDataService,
        ILogger<ReviewAnalysisTools> logger)
    {
        _gitHubService = gitHubService;
        _prDataService = prDataService;
        _logger = logger;
    }

    public void SetLanguage(string language)
    {
        _language = language.ToLowerInvariant() switch
        {
            "ja" => "ja",
            "en" => "en",
            _ => "ja" // デフォルトは日本語
        };

        _logger.LogInformation("Language set to: {Language}", _language);
    }

    /// <summary>
    /// レビュー内容から問題点を抽出するTool
    /// レビュー内容を解析して、ファイルごとの問題点を構造化された形式で抽出します
    /// </summary>
    [KernelFunction("ExtractReviewIssues")]
    public async Task<ReviewAnalysisResult> ExtractReviewIssuesAsync(
        string reviewContent,
        string language = "ja")
    {
        _logger.LogInformation("=== ExtractReviewIssues Called ===");
        _logger.LogInformation("Review content length: {Length} characters", reviewContent?.Length ?? 0);
        SetLanguage(language);

        try
        {
            // レビュー内容を解析して問題点を抽出
            var issues = ParseReviewContent(reviewContent, _language);

            _logger.LogInformation("Extracted {IssueCount} issues from review", issues.Count);
            foreach (var issue in issues)
            {
                _logger.LogInformation("Issue: {Level} - {Title} in {FilePath}:{StartLine}", issue.Level, issue.Title, issue.FilePath, issue.StartLine);
            }

            return new ReviewAnalysisResult
            {
                Issues = issues,
                ReviewSummary = CreateReviewSummary(issues)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract review issues");
            throw;
        }
    }

    /// <summary>
    /// 問題点からGitHubレビューコメントを生成するTool
    /// 抽出された問題点から、GitHubプルリクエスト用の適切なレビューコメントを生成します
    /// </summary>
    [KernelFunction("GenerateReviewComments")]
    public async Task<List<PRAgent.ReviewModels.DraftPullRequestReviewComment>> GenerateReviewCommentsAsync(
        ReviewAnalysisResult analysis,
        string language = "ja")
    {
        _logger.LogInformation("=== GenerateReviewComments Called ===");
        _logger.LogInformation("Processing {IssueCount} issues", analysis?.Issues?.Count ?? 0);
        SetLanguage(language);

        var comments = new List<PRAgent.ReviewModels.DraftPullRequestReviewComment>();

        if (analysis?.Issues == null || !analysis.Issues.Any())
        {
            _logger.LogWarning("No issues found in analysis result");
            return comments;
        }

        foreach (var issue in analysis.Issues)
        {
            try
            {
                _logger.LogInformation("Processing issue: {Title} in {FilePath}:{StartLine}", issue.Title, issue.FilePath, issue.StartLine);

                var comment = await GenerateCommentForIssueAsync(issue, _language);
                if (comment != null)
                {
                    comments.Add(comment);
                    _logger.LogInformation("Comment added for issue: {Title}", issue.Title);
                }
                else
                {
                    _logger.LogWarning("Comment is null for issue: {Title}", issue.Title);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate comment for issue: {Title}", issue.Title);

                // フォールバックコメント
                _logger.LogInformation("Using fallback comment for issue: {Title}", issue.Title);
                var fallbackComment = new PRAgent.ReviewModels.DraftPullRequestReviewComment(
                    issue.FilePath,
                    $"{issue.Level}: {issue.Description}\n\n{issue.Suggestion}",
                    issue.StartLine);
                comments.Add(fallbackComment);
            }
        }

        _logger.LogInformation("Generated {CommentCount} comments from {IssueCount} issues", comments.Count, analysis.Issues.Count);
        return comments;
    }

    /// <summary>
    /// 差分がないファイルのコンテンツを読み込むTool
    /// </summary>
    [KernelFunction("ReadFileContent")]
    public async Task<string> ReadFileContentAsync(
        string owner,
        string repo,
        string filePath,
        int prNumber)
    {
        _logger.LogInformation("=== ReadFileContent Called for {FilePath} ===", filePath);

        try
        {
            // PRの変更ファイルから正しいパスを特定
            var (_, files, _) = await _prDataService.GetPullRequestDataAsync(owner, repo, prNumber);

            var changedFile = files.FirstOrDefault(f => f.FileName.Equals(filePath, StringComparison.OrdinalIgnoreCase));

            if (changedFile != null && string.IsNullOrEmpty(changedFile.Patch))
            {
                // 差分がないファイルのコンテンツを読み込む
                var content = await _gitHubService.GetRepositoryFileContentAsync(owner, repo, changedFile.FileName);
                return content ?? $"File not found: {filePath}";
            }
            else
            {
                return $"File {filePath} has diff or not found in PR";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read file content for {FilePath}", filePath);
            return $"Error reading file: {ex.Message}";
        }
    }

    /// <summary>
    /// レビュー内容のパーサー
    /// </summary>
    private List<PRAgent.ReviewModels.ReviewIssue> ParseReviewContent(string reviewContent, string language)
    {
        var issues = new List<PRAgent.ReviewModels.ReviewIssue>();

        // セクションごとに分割
        var sections = reviewContent.Split(new[] { "\n\n### ", "\n## ", "\n##\n\n" }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var section in sections)
        {
            // セクションタイトルを解析
            var titleMatch = System.Text.RegularExpressions.Regex.Match(section, @"^(###\s*\[([A-Z]+)\])?\s*(.+)");
            if (titleMatch.Success)
            {
                var levelStr = titleMatch.Groups[2].Value;
                var title = titleMatch.Groups[3].Value.Trim();

                // レベルを判定 - string型に変換
                var level = levelStr switch
                {
                    "CRITICAL" => "CRITICAL",
                    "MAJOR" => "MAJOR",
                    "MINOR" => "MINOR",
                    "POSITIVE" => "POSITIVE",
                    _ => "MAJOR"
                };

                // ファイルパスを抽出
                var pathMatch = System.Text.RegularExpressions.Regex.Match(section, @"\*\*ファイル:\*\*`([^`]+)`");
                var path = pathMatch.Success ? pathMatch.Groups[1].Value : "src/File.cs";

                if (!pathMatch.Success)
                {
                    _logger.LogWarning("Failed to extract file path from section. Using default: src/File.cs. Section: {Section}", section.Substring(0, Math.Min(100, section.Length)));
                }

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
                    _logger.LogInformation("Line number extracted successfully: {StartLine}-{EndLine}", startLine, endLine ?? startLine);
                }
                else
                {
                    _logger.LogWarning("Failed to extract line number from section. Pattern not matched. Section: {Section}", section.Substring(0, Math.Min(100, section.Length)));
                }

                // 問題説明を抽出
                var problemSection = System.Text.RegularExpressions.Regex.Split(section, @"\n\*\*\w+:\*\*");
                var problem = problemSection.Length > 1 ? problemSection[1].Split('\n')[0].Trim() : section.Split('\n')[0].Trim();

                // 修正提案を抽出
                var suggestion = "";
                var suggestionMatch = System.Text.RegularExpressions.Regex.Match(section, @"```suggestion\s*\n?([^\n`]+)");
                if (suggestionMatch.Success)
                {
                    suggestion = suggestionMatch.Groups[1].Value.Trim();
                }

                issues.Add(new PRAgent.ReviewModels.ReviewIssue
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
    /// 各問題からGitHubコメントを生成
    /// </summary>
    private async Task<PRAgent.ReviewModels.DraftPullRequestReviewComment?> GenerateCommentForIssueAsync(PRAgent.ReviewModels.ReviewIssue issue, string language)
    {
        var prompt = CreateCommentPrompt(issue, language);

        // KernelServiceをDIで取得して呼び出す（実際の実装ではKernelを渡す）
        // ここでは簡略化して直接返す
        var commentText = await GenerateCommentText(issue, language);

        _logger.LogInformation("Generated comment for issue: {Title} at {FilePath}:{StartLine}", issue.Title, issue.FilePath, issue.StartLine);

        return new PRAgent.ReviewModels.DraftPullRequestReviewComment(issue.FilePath, commentText, issue.StartLine);
    }

    /// <summary>
    /// コメント生成用のプロンプト
    /// </summary>
    private string CreateCommentPrompt(PRAgent.ReviewModels.ReviewIssue issue, string language)
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

    /// <summary>
    /// コメントテキストを生成（実際の実装ではAI呼び出し）
    /// </summary>
    private Task<string> GenerateCommentText(PRAgent.ReviewModels.ReviewIssue issue, string language)
    {
        return Task.FromResult(
            $"{issue.Level}: {issue.Description}\n\n{issue.Suggestion}"
        );
    }

    /// <summary>
    /// レビュー要約を作成
    /// </summary>
    private string CreateReviewSummary(List<PRAgent.ReviewModels.ReviewIssue> issues)
    {
        var criticalCount = issues.Count(i => i.Level == "CRITICAL");
        var majorCount = issues.Count(i => i.Level == "MAJOR");
        var minorCount = issues.Count(i => i.Level == "MINOR");
        var positiveCount = issues.Count(i => i.Level == "POSITIVE");

        return $"Issues found: {issues.Count} (Critical: {criticalCount}, Major: {majorCount}, Minor: {minorCount}, Positive: {positiveCount})";
    }
}

