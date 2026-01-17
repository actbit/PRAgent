using Microsoft.SemanticKernel;
using PRAgent.Services;

namespace PRAgent.Plugins.PRAnalysis;

public class PRSummaryPlugin
{
    private readonly IKernelService _kernelService;
    private readonly IGitHubService _gitHubService;

    public PRSummaryPlugin(
        IKernelService kernelService,
        IGitHubService gitHubService)
    {
        _kernelService = kernelService;
        _gitHubService = gitHubService;
    }

    public async Task<string> SummarizePullRequestAsync(
        string owner,
        string repo,
        int prNumber,
        CancellationToken cancellationToken = default)
    {
        var kernel = _kernelService.CreateKernel("You are a technical writer specializing in clear, concise documentation.");

        var pr = await _gitHubService.GetPullRequestAsync(owner, repo, prNumber);
        var files = await _gitHubService.GetPullRequestFilesAsync(owner, repo, prNumber);
        var diff = await _gitHubService.GetPullRequestDiffAsync(owner, repo, prNumber);

        var fileList = string.Join("\n", files.Select(f => $"- {f.FileName} ({f.Status}): +{f.Additions} -{f.Deletions}"));

        var prompt = $"""
            You are a technical writer specializing in clear, concise documentation. Summarize the following pull request.

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
            Provide a clear, concise summary that includes:

            1. **Purpose**: What does this PR aim to achieve?

            2. **Key Changes**: Main files and components modified

            3. **Impact**: What areas of the codebase are affected?

            4. **Risk Assessment**:
               - Low risk / Medium risk / High risk
               - Brief justification

            5. **Testing Notes**: Any areas that need special attention during testing

            Keep the summary under 300 words. Use bullet points for readability.

            Format your response in markdown.
            """;

        return await _kernelService.InvokePromptAsStringAsync(kernel, prompt, cancellationToken);
    }
}
