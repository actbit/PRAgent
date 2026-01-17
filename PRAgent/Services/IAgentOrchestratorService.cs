using PRAgent.Agents;
using PRAgent.Models;

namespace PRAgent.Services;

public interface IAgentOrchestratorService
{
    Task<string> ReviewAsync(string owner, string repo, int prNumber, CancellationToken cancellationToken = default);
    Task<string> SummarizeAsync(string owner, string repo, int prNumber, CancellationToken cancellationToken = default);
    Task<ApprovalResult> ReviewAndApproveAsync(
        string owner,
        string repo,
        int prNumber,
        ApprovalThreshold threshold,
        CancellationToken cancellationToken = default);
}

public class ApprovalResult
{
    public bool Approved { get; set; }
    public string Review { get; set; } = string.Empty;
    public string Reasoning { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public string? ApprovalUrl { get; set; }
}
