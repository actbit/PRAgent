using Microsoft.SemanticKernel;
using PRAgent.Services;

namespace PRAgent.Agents;

public class SummaryAgent
{
    private readonly IKernelService _kernelService;
    private readonly IGitHubService _gitHubService;
    private readonly AgentDefinition _definition;

    public SummaryAgent(
        IKernelService kernelService,
        IGitHubService gitHubService,
        string? customSystemPrompt = null)
    {
        _kernelService = kernelService;
        _gitHubService = gitHubService;
        _definition = AgentDefinition.SummaryAgent;

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

    public async Task<string> SummarizeAsync(
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
            Create a concise summary of this pull request.

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
            1. **Purpose**: What this PR achieves
            2. **Key Changes**: Main files/components modified
            3. **Impact**: Areas affected
            4. **Risk Assessment**: Low/Medium/High with justification
            5. **Testing Notes**: Areas needing special attention

            Keep under 300 words. Use markdown with bullet points.
            """;

        return await _kernelService.InvokePromptAsStringAsync(kernel, prompt, cancellationToken);
    }
}
