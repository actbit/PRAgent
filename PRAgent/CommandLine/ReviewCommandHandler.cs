using PRAgent.Services;
using Serilog;

namespace PRAgent.CommandLine;

/// <summary>
/// Handles the review command
/// </summary>
public class ReviewCommandHandler : ICommandHandler
{
    private readonly ReviewOptions _options;
    private readonly IPRAnalysisService _prAnalysisService;

    public ReviewCommandHandler(ReviewOptions options, IPRAnalysisService prAnalysisService)
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

        var review = await _prAnalysisService.ReviewPullRequestAsync(
            _options.Owner!,
            _options.Repo!,
            _options.PrNumber,
            _options.PostComment,
            _options.Language);

        Console.WriteLine();
        Console.WriteLine(review);
        Console.WriteLine();

        return 0;
    }
}
