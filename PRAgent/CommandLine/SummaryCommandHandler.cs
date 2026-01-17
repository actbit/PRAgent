using PRAgent.Services;
using Serilog;

namespace PRAgent.CommandLine;

/// <summary>
/// Handles the summary command
/// </summary>
public class SummaryCommandHandler : ICommandHandler
{
    private readonly SummaryOptions _options;
    private readonly IPRAnalysisService _prAnalysisService;

    public SummaryCommandHandler(SummaryOptions options, IPRAnalysisService prAnalysisService)
    {
        _options = options;
        _prAnalysisService = prAnalysisService;
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

        var summary = await _prAnalysisService.SummarizePullRequestAsync(
            _options.Owner!,
            _options.Repo!,
            _options.PrNumber,
            _options.PostComment);

        Console.WriteLine();
        Console.WriteLine(summary);
        Console.WriteLine();

        return 0;
    }
}
