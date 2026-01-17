using PRAgent.Models;

namespace PRAgent.CommandLine;

/// <summary>
/// Parses command line arguments into strongly-typed options
/// </summary>
public static class CommandLineParser
{
    /// <summary>
    /// Parses arguments for the review command
    /// </summary>
    public static ReviewOptions ParseReviewOptions(string[] args)
    {
        var options = new ReviewOptions();

        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--owner":
                case "-o":
                    if (i + 1 < args.Length)
                        options = options with { Owner = args[++i] };
                    break;
                case "--repo":
                case "-r":
                    if (i + 1 < args.Length)
                        options = options with { Repo = args[++i] };
                    break;
                case "--pr":
                case "-p":
                case "--number":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out var pr))
                        options = options with { PrNumber = pr };
                    break;
                case "--post-comment":
                case "-c":
                    options = options with { PostComment = true };
                    break;
                case "--language":
                case "-l":
                    if (i + 1 < args.Length)
                        options = options with { Language = args[++i] };
                    break;
            }
        }

        return options;
    }

    /// <summary>
    /// Parses arguments for the summary command
    /// </summary>
    public static SummaryOptions ParseSummaryOptions(string[] args)
    {
        var options = new SummaryOptions();

        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--owner":
                case "-o":
                    if (i + 1 < args.Length)
                        options = options with { Owner = args[++i] };
                    break;
                case "--repo":
                case "-r":
                    if (i + 1 < args.Length)
                        options = options with { Repo = args[++i] };
                    break;
                case "--pr":
                case "-p":
                case "--number":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out var pr))
                        options = options with { PrNumber = pr };
                    break;
                case "--post-comment":
                case "-c":
                    options = options with { PostComment = true };
                    break;
                case "--language":
                case "-l":
                    if (i + 1 < args.Length)
                        options = options with { Language = args[++i] };
                    break;
            }
        }

        return options;
    }

    /// <summary>
    /// Parses arguments for the approve command
    /// </summary>
    public static ApproveOptions ParseApproveOptions(string[] args)
    {
        var options = new ApproveOptions();

        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--owner":
                case "-o":
                    if (i + 1 < args.Length)
                        options = options with { Owner = args[++i] };
                    break;
                case "--repo":
                case "-r":
                    if (i + 1 < args.Length)
                        options = options with { Repo = args[++i] };
                    break;
                case "--pr":
                case "-p":
                case "--number":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out var pr))
                        options = options with { PrNumber = pr };
                    break;
                case "--auto":
                    options = options with { Auto = true };
                    break;
                case "--threshold":
                case "-t":
                    if (i + 1 < args.Length)
                        options = options with { Threshold = ParseThreshold(args[++i]) };
                    break;
                case "--comment":
                case "-m":
                    if (i + 1 < args.Length)
                        options = options with { Comment = args[++i] };
                    break;
                case "--post-comment":
                case "-c":
                    options = options with { PostComment = true };
                    break;
                case "--language":
                case "-l":
                    if (i + 1 < args.Length)
                        options = options with { Language = args[++i] };
                    break;
            }
        }

        return options;
    }

    /// <summary>
    /// Parses a threshold string value into an ApprovalThreshold enum
    /// </summary>
    public static ApprovalThreshold ParseThreshold(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "critical" => ApprovalThreshold.Critical,
            "major" => ApprovalThreshold.Major,
            "minor" => ApprovalThreshold.Minor,
            "none" => ApprovalThreshold.None,
            _ => ApprovalThreshold.Minor
        };
    }
}
