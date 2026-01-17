using Octokit;

namespace PRAgent.Services;

public class PullRequestDataService
{
    private readonly IGitHubService _gitHubService;

    public PullRequestDataService(IGitHubService gitHubService)
    {
        _gitHubService = gitHubService;
    }

    public async Task<(PullRequest pr, IReadOnlyList<PullRequestFile> files, string diff)> GetPullRequestDataAsync(
        string owner,
        string repo,
        int prNumber)
    {
        var pr = await _gitHubService.GetPullRequestAsync(owner, repo, prNumber);
        var files = await _gitHubService.GetPullRequestFilesAsync(owner, repo, prNumber);
        var diff = await _gitHubService.GetPullRequestDiffAsync(owner, repo, prNumber);

        return (pr, files, diff);
    }

    public static string FormatFileList(IReadOnlyList<PullRequestFile> files)
    {
        return string.Join("\n", files.Select(f => $"- {f.FileName} ({f.Status}): +{f.Additions} -{f.Deletions}"));
    }

    public static string CreateReviewPrompt(
        PullRequest pr,
        string fileList,
        string diff,
        string systemPrompt)
    {
        return $"""
            {systemPrompt}

            ## Pull Request Information
            - Title: {pr.Title}
            - Author: {pr.User.Login}
            - Description: {pr.Body ?? "No description provided"}
            - Branch: {pr.Head.Ref} -> {pr.Base.Ref}

            ## Changed Files
            {fileList}

            ## Diff
            {diff}

            Provide a structured review with:
            1. Overview
            2. Critical Issues [CRITICAL]
            3. Major Issues [MAJOR]
            4. Minor Issues [MINOR]
            5. Positive Highlights [POSITIVE]
            6. Recommendation (Approve as is / Approve with suggestions / Request changes)
            """;
    }

    public static string CreateSummaryPrompt(
        PullRequest pr,
        string fileList,
        string diff,
        string systemPrompt)
    {
        return $"""
            {systemPrompt}

            ## Pull Request
            - Title: {pr.Title}
            - Author: {pr.User.Login}
            - Description: {pr.Body ?? "No description provided"}
            - Branch: {pr.Head.Ref} -> {pr.Base.Ref}

            ## Changed Files
            {fileList}

            ## Diff
            {diff}

            Provide a summary including:
            1. Purpose: What does this PR achieve?
            2. Key Changes: Main files/components modified
            3. Impact: Areas affected
            4. Risk Assessment: Low/Medium/High with justification
            5. Testing Notes: Areas needing special attention

            Keep under 300 words. Use markdown.
            """;
    }
}
