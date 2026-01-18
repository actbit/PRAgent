using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Octokit;
using PRAgent.Agents;
using PRAgent.Models;
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

    public ReviewAnalysisTools(
        IGitHubService gitHubService,
        PullRequestDataService prDataService,
        ILogger<ReviewAnalysisTools> logger)
    {
        _gitHubService = gitHubService;
        _prDataService = prDataService;
        _logger = logger;
    }

    /// <summary>
    /// レビュー内容から問題点を抽出するTool
    /// </summary>
    [KernelFunction("ExtractReviewIssues")]
    public async Task<ReviewAnalysisResult> ExtractReviewIssuesAsync(
        string reviewContent,
        string language = "ja")
    {
        _logger.LogInformation("=== ExtractReviewIssues Called ===");

        try
        {
            // レビュー内容を解析して問題点を抽出
            var issues = ParseReviewContent(reviewContent, language);

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
    /// </summary>
    [KernelFunction("GenerateReviewComments")]
    public async Task<List<DraftPullRequestReviewComment>> GenerateReviewCommentsAsync(
        ReviewAnalysisResult analysis,
        string language = "ja")
    {
        _logger.LogInformation("=== GenerateReviewComments Called ===");

        var comments = new List<DraftPullRequestReviewComment>();

        foreach (var issue in analysis.Issues)
        {
            try
            {
                var comment = await GenerateCommentForIssueAsync(issue, language);
                if (comment != null)
                {
                    comments.Add(comment);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate comment for issue: {Title}", issue.Title);

                // フォールバックコメント
                var fallbackComment = new DraftPullRequestReviewComment(
                    issue.FilePath,
                    $"{issue.Level}: {issue.Description}\n\n{issue.Suggestion}",
                    issue.StartLine);
                comments.Add(fallbackComment);
            }
        }

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
    private List<ReviewIssue> ParseReviewContent(string reviewContent, string language)
    {
        var issues = new List<ReviewIssue>();

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
                var problemSection = System.Text.RegularExpressions.Regex.Split(section, @"\n\*\*\w+:\*\*");
                var problem = problemSection.Length > 1 ? problemSection[1].Split('\n')[0].Trim() : section.Split('\n')[0].Trim();

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
    /// 各問題からGitHubコメントを生成
    /// </summary>
    private async Task<DraftPullRequestReviewComment?> GenerateCommentForIssueAsync(ReviewIssue issue, string language)
    {
        var prompt = CreateCommentPrompt(issue, language);

        // KernelServiceをDIで取得して呼び出す（実際の実装ではKernelを渡す）
        // ここでは簡略化して直接返す
        var commentText = await GenerateCommentText(issue, language);

        return new DraftPullRequestReviewComment(issue.FilePath, commentText, issue.StartLine);
    }

    /// <summary>
    /// コメント生成用のプロンプト
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

    /// <summary>
    /// コメントテキストを生成（実際の実装ではAI呼び出し）
    /// </summary>
    private Task<string> GenerateCommentText(ReviewIssue issue, string language)
    {
        return Task.FromResult(
            $"{issue.Level}: {issue.Description}\n\n{issue.Suggestion}"
        );
    }

    /// <summary>
    /// レビュー要約を作成
    /// </summary>
    private string CreateReviewSummary(List<ReviewIssue> issues)
    {
        var criticalCount = issues.Count(i => i.Level == Severity.Critical);
        var majorCount = issues.Count(i => i.Level == Severity.Major);
        var minorCount = issues.Count(i => i.Level == Severity.Minor);
        var positiveCount = issues.Count(i => i.Level == Severity.Positive);

        return $"Issues found: {issues.Count} (Critical: {criticalCount}, Major: {majorCount}, Minor: {minorCount}, Positive: {positiveCount})";
    }
}

/// <summary>
/// レビュー分析結果
/// </summary>
public class ReviewAnalysisResult
{
    public List<ReviewIssue> Issues { get; set; } = new List<ReviewIssue>();
    public string ReviewSummary { get; set; } = string.Empty;
}