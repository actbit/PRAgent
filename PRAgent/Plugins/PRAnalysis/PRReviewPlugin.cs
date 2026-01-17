using Microsoft.SemanticKernel;
using PRAgent.Services;

namespace PRAgent.Plugins.PRAnalysis;

public class PRReviewPlugin
{
    private readonly IKernelService _kernelService;
    private readonly IGitHubService _gitHubService;
    private readonly string _systemPrompt;

    public PRReviewPlugin(
        IKernelService kernelService,
        IGitHubService gitHubService,
        string? systemPrompt = null)
    {
        _kernelService = kernelService;
        _gitHubService = gitHubService;
        _systemPrompt = systemPrompt ?? "You are an expert code reviewer. Provide thorough, constructive feedback.";
    }

    public async Task<string> ReviewPullRequestAsync(
        string owner,
        string repo,
        int prNumber,
        CancellationToken cancellationToken = default)
    {
        var kernel = _kernelService.CreateKernel(_systemPrompt);

        var pr = await _gitHubService.GetPullRequestAsync(owner, repo, prNumber);
        var files = await _gitHubService.GetPullRequestFilesAsync(owner, repo, prNumber);
        var diff = await _gitHubService.GetPullRequestDiffAsync(owner, repo, prNumber);

        var fileList = string.Join("\n", files.Select(f => $"- {f.FileName} ({f.Status}): +{f.Additions} -{f.Deletions}"));

        var prompt = $"""
            You are an expert code reviewer. Analyze the following pull request and provide a comprehensive review.

            ## Pull Request Information
            - Title: {pr.Title}
            - Author: {pr.User.Login}
            - Description: {pr.Body ?? "No description provided"}
            - Branch: {pr.Head.Ref} -> {pr.Base.Ref}

            ## Changed Files
            {fileList}

            ## Diff
            {diff}

            ## Instructions
            Provide a structured code review that includes:

            1. **Overview**: Brief summary of what this PR changes

            2. **Critical Issues**: Any bugs, security vulnerabilities, or breaking changes
               - Use label: [CRITICAL]

            3. **Major Issues**: Significant problems that should be addressed
               - Use label: [MAJOR]

            4. **Minor Issues**: Small improvements, suggestions, or nitpicks
               - Use label: [MINOR]

            5. **Positive Highlights**: Good practices, well-written code worth mentioning
               - Use label: [POSITIVE]

            6. **Recommendation**:
               - Approve as is
               - Approve with suggestions
               - Request changes

            Focus on:
            - Code correctness and potential bugs
            - Security vulnerabilities
            - Performance considerations
            - Code organization and readability
            - Adherence to best practices
            - Test coverage (if applicable)

            Format your response in markdown with clear sections.
            """;

        return await _kernelService.InvokePromptAsStringAsync(kernel, prompt, cancellationToken);
    }
}
