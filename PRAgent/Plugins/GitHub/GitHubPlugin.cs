using Microsoft.SemanticKernel;
using PRAgent.Services;

namespace PRAgent.Plugins.GitHub;

public class GitHubPlugin
{
    private readonly IGitHubService _gitHubService;
    private readonly string _owner;
    private readonly string _repo;
    private readonly int _prNumber;

    public GitHubPlugin(IGitHubService gitHubService, string owner, string repo, int prNumber)
    {
        _gitHubService = gitHubService;
        _owner = owner;
        _repo = repo;
        _prNumber = prNumber;
    }

    [KernelFunction("get_pr_title")]
    public async Task<string> GetPRTitleAsync()
    {
        var pr = await _gitHubService.GetPullRequestAsync(_owner, _repo, _prNumber);
        return pr.Title;
    }

    [KernelFunction("get_pr_author")]
    public async Task<string> GetPRAuthorAsync()
    {
        var pr = await _gitHubService.GetPullRequestAsync(_owner, _repo, _prNumber);
        return pr.User.Login;
    }

    [KernelFunction("get_pr_description")]
    public async Task<string> GetPRDescriptionAsync()
    {
        var pr = await _gitHubService.GetPullRequestAsync(_owner, _repo, _prNumber);
        return pr.Body ?? string.Empty;
    }

    [KernelFunction("get_pr_branches")]
    public async Task<string> GetPRBranchesAsync()
    {
        var pr = await _gitHubService.GetPullRequestAsync(_owner, _repo, _prNumber);
        return $"{pr.Head.Ref} -> {pr.Base.Ref}";
    }

    [KernelFunction("get_pr_files")]
    public async Task<string> GetPRFilesAsync()
    {
        var files = await _gitHubService.GetPullRequestFilesAsync(_owner, _repo, _prNumber);
        var fileList = new System.Text.StringBuilder();

        foreach (var file in files)
        {
            fileList.AppendLine($"- {file.FileName} ({file.Status}): +{file.Additions} -{file.Deletions}");
        }

        return fileList.ToString();
    }

    [KernelFunction("get_pr_diff")]
    public async Task<string> GetPRDiffAsync()
    {
        return await _gitHubService.GetPullRequestDiffAsync(_owner, _repo, _prNumber);
    }

    [KernelFunction("post_comment")]
    public async Task<string> PostCommentAsync(string comment)
    {
        var result = await _gitHubService.CreateIssueCommentAsync(_owner, _repo, _prNumber, comment);
        return $"Comment posted: {result.HtmlUrl}";
    }

    [KernelFunction("approve_pr")]
    public async Task<string> ApprovePRAsync(string? comment = null)
    {
        var result = await _gitHubService.ApprovePullRequestAsync(_owner, _repo, _prNumber, comment);
        return $"PR approved: {result.HtmlUrl}";
    }
}
