using Microsoft.SemanticKernel;
using PRAgent.Services;

namespace PRAgent.Agents;

public class ReviewAgent
{
    private readonly IKernelService _kernelService;
    private readonly IGitHubService _gitHubService;
    private readonly AgentDefinition _definition;

    public ReviewAgent(
        IKernelService kernelService,
        IGitHubService gitHubService,
        string? customSystemPrompt = null)
    {
        _kernelService = kernelService;
        _gitHubService = gitHubService;
        _definition = AgentDefinition.ReviewAgent;

        if (!string.IsNullOrEmpty(customSystemPrompt))
        {
            _definition = new AgentDefinition(
                _definition.Name,
                _definition.Role,
                customSystemPrompt,
                _definition.Description
            );
        }
    }

    public async Task<string> ReviewAsync(
        string owner,
        string repo,
        int prNumber,
        CancellationToken cancellationToken = default)
    {
        var kernel = _kernelService.CreateKernel(_definition.SystemPrompt);

        var pr = await _gitHubService.GetPullRequestAsync(owner, repo, prNumber);
        var files = await _gitHubService.GetPullRequestFilesAsync(owner, repo, prNumber);
        var diff = await _gitHubService.GetPullRequestDiffAsync(owner, repo, prNumber);

        var fileList = string.Join("\n", files.Select(f => $"- {f.FileName} ({f.Status}): +{f.Additions} -{f.Deletions}"));

        var prompt = $"""
            Analyze the following pull request and provide a comprehensive code review.

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

            Format in markdown.
            """;

        return await _kernelService.InvokePromptAsStringAsync(kernel, prompt, cancellationToken);
    }
}
