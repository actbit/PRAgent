using PRAgent.Services;
using Serilog;

namespace PRAgent.CommandLine;

/// <summary>
/// Handles the approve command
/// </summary>
public class ApproveCommandHandler : ICommandHandler
{
    private readonly ApproveOptions _options;
    private readonly IPRAnalysisService _prAnalysisService;
    private readonly IGitHubService _gitHubService;

    public ApproveCommandHandler(
        ApproveOptions options,
        IPRAnalysisService prAnalysisService,
        IGitHubService gitHubService)
    {
        _options = options;
        _prAnalysisService = prAnalysisService;
        _gitHubService = gitHubService;
    }

    public async Task<int> ExecuteAsync()
    {
        if (!_options.IsValid(out var errors))
        {
            Log.Error("Invalid options:");
            foreach (var error in errors)
            {
                Log.Error("  - {Error}", error);
            }
            return 1;
        }

        if (_options.Auto)
        {
            // Auto mode: Review and approve based on AI decision
            var result = await _prAnalysisService.ReviewAndApproveAsync(
                _options.Owner!,
                _options.Repo!,
                _options.PrNumber,
                _options.Threshold,
                _options.PostComment,
                _options.Language);

            Console.WriteLine();
            Console.WriteLine("## Review Result");
            Console.WriteLine(result.Review);
            Console.WriteLine();
            Console.WriteLine($"## Approval Decision: {(result.Approved ? "APPROVED" : "NOT APPROVED")}");
            Console.WriteLine($"Reasoning: {result.Reasoning}");

            if (result.Approved && !string.IsNullOrEmpty(result.ApprovalUrl))
            {
                Console.WriteLine($"Approval URL: {result.ApprovalUrl}");
            }
            Console.WriteLine();

            return result.Approved ? 0 : 1;
        }
        else
        {
            // Direct approval without review
            var result = await _gitHubService.ApprovePullRequestAsync(
                _options.Owner!,
                _options.Repo!,
                _options.PrNumber,
                _options.Comment);

            Console.WriteLine($"PR approved: {result}");
            return 0;
        }
    }
}
