using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PRAgent.Agents;
using PRAgent.Models;
using PRAgent.Services;
using PRAgent.Validators;
using Serilog;

namespace PRAgent;

internal class Program
{
    static async Task<int> Main(string[] args)
    {
        // Build configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        // Configure Serilog
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
            )
            .CreateLogger();

        try
        {
            Log.Information("PRAgent starting...");

            // Build host
            var host = Host.CreateDefaultBuilder(args)
                .UseSerilog()
                .ConfigureServices((context, services) =>
                {
                    // Configuration
                    services.Configure<AISettings>(
                        configuration.GetSection(AISettings.SectionName));
                    services.Configure<PRSettings>(
                        configuration.GetSection(PRSettings.SectionName));

                    // Core Services
                    var aiSettings = configuration.GetSection(AISettings.SectionName).Get<AISettings>()
                        ?? new AISettings();
                    var prSettings = configuration.GetSection(PRSettings.SectionName).Get<PRSettings>()
                        ?? new PRSettings();

                    // Validate settings
                    var errors = new List<string>();
                    ConfigValidator.ValidateAISettings(aiSettings, errors);
                    ConfigValidator.ValidatePRSettings(prSettings, errors);

                    if (errors.Any())
                    {
                        Log.Error("Configuration validation failed:");
                        foreach (var error in errors)
                        {
                            Log.Error("  - {Error}", error);
                        }
                        throw new InvalidOperationException("Invalid configuration");
                    }

                    services.AddSingleton(_ => aiSettings);
                    services.AddSingleton(_ => prSettings);

                    // GitHub Service
                    services.AddSingleton<IGitHubService, GitHubService>();

                    // Kernel Service
                    services.AddSingleton<IKernelService, KernelService>();

                    // Configuration Service
                    services.AddSingleton<IConfigurationService, ConfigurationService>();

                    // Data Services
                    services.AddSingleton<PullRequestDataService>();

                    // Agents
                    services.AddSingleton<ReviewAgent>();
                    services.AddSingleton<ApprovalAgent>();
                    services.AddSingleton<SummaryAgent>();

                    // Agent Orchestrator
                    services.AddSingleton<IAgentOrchestratorService, AgentOrchestratorService>();

                    // PR Analysis Service
                    services.AddSingleton<IPRAnalysisService, PRAnalysisService>();
                })
                .Build();

            // Run CLI
            return await RunCliAsync(args, host.Services, configuration);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "PRAgent terminated unexpectedly");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    static async Task<int> RunCliAsync(string[] args, IServiceProvider services, IConfiguration configuration)
    {
        if (args.Length == 0)
        {
            ShowHelp();
            return 0;
        }

        var command = args[0].ToLowerInvariant();
        var prSettings = services.GetRequiredService<PRSettings>();

        try
        {
            switch (command)
            {
                case "review":
                    return await RunReviewCommandAsync(args, services);

                case "summary":
                    return await RunSummaryCommandAsync(args, services);

                case "approve":
                    return await RunApproveCommandAsync(args, services);

                case "help":
                case "--help":
                case "-h":
                    ShowHelp();
                    return 0;

                default:
                    Log.Error("Unknown command: {Command}", command);
                    ShowHelp();
                    return 1;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Command failed: {Command}", command);
            return 1;
        }
    }

    static async Task<int> RunReviewCommandAsync(string[] args, IServiceProvider services)
    {
        var options = ParseReviewOptions(args);

        if (!options.IsValid(out var errors))
        {
            Log.Error("Invalid options:");
            foreach (var error in errors)
            {
                Log.Error("  - {Error}", error);
            }
            return 1;
        }

        var service = services.GetRequiredService<IPRAnalysisService>();
        var review = await service.ReviewPullRequestAsync(
            options.Owner!,
            options.Repo!,
            options.PrNumber,
            options.PostComment);

        Console.WriteLine();
        Console.WriteLine(review);
        Console.WriteLine();

        return 0;
    }

    static async Task<int> RunSummaryCommandAsync(string[] args, IServiceProvider services)
    {
        var options = ParseSummaryOptions(args);

        if (!options.IsValid(out var errors))
        {
            Log.Error("Invalid options:");
            foreach (var error in errors)
            {
                Log.Error("  - {Error}", error);
            }
            return 1;
        }

        var service = services.GetRequiredService<IPRAnalysisService>();
        var summary = await service.SummarizePullRequestAsync(
            options.Owner!,
            options.Repo!,
            options.PrNumber,
            options.PostComment);

        Console.WriteLine();
        Console.WriteLine(summary);
        Console.WriteLine();

        return 0;
    }

    static async Task<int> RunApproveCommandAsync(string[] args, IServiceProvider services)
    {
        var options = ParseApproveOptions(args);

        if (!options.IsValid(out var errors))
        {
            Log.Error("Invalid options:");
            foreach (var error in errors)
            {
                Log.Error("  - {Error}", error);
            }
            return 1;
        }

        var service = services.GetRequiredService<IPRAnalysisService>();

        if (options.Auto)
        {
            // Auto mode: Review and approve based on AI decision
            var result = await service.ReviewAndApproveAsync(
                options.Owner!,
                options.Repo!,
                options.PrNumber,
                options.Threshold,
                options.PostComment);

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
            var gitHubService = services.GetRequiredService<IGitHubService>();
            var result = await gitHubService.ApprovePullRequestAsync(
                options.Owner!,
                options.Repo!,
                options.PrNumber,
                options.Comment);

            Console.WriteLine($"PR approved: {result}");
            return 0;
        }
    }

    static ReviewOptions ParseReviewOptions(string[] args)
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
            }
        }

        return options;
    }

    static SummaryOptions ParseSummaryOptions(string[] args)
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
            }
        }

        return options;
    }

    static ApproveOptions ParseApproveOptions(string[] args)
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
            }
        }

        return options;
    }

    static ApprovalThreshold ParseThreshold(string value)
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

    static void ShowHelp()
    {
        Console.WriteLine("""
            PRAgent - AI-powered Pull Request Agent

            USAGE:
              PRAgent <command> [options]

            COMMANDS:
              review      Review a pull request
              summary     Summarize a pull request
              approve     Approve a pull request
              help        Show this help message

            REVIEW OPTIONS:
              --owner, -o      Repository owner (required)
              --repo, -r       Repository name (required)
              --pr, -p         Pull request number (required)
              --post-comment, -c    Post review as PR comment

            SUMMARY OPTIONS:
              --owner, -o      Repository owner (required)
              --repo, -r       Repository name (required)
              --pr, -p         Pull request number (required)
              --post-comment, -c    Post summary as PR comment

            APPROVE OPTIONS:
              --owner, -o      Repository owner (required)
              --repo, -r       Repository name (required)
              --pr, -p         Pull request number (required)
              --auto           Review first, then approve based on AI decision
              --threshold, -t  Approval threshold (critical|major|minor|none, default: minor)
              --comment, -m    Approval comment (only without --auto)
              --post-comment, -c    Post decision as PR comment (only with --auto)

            ENVIRONMENT VARIABLES:
              AI_ENDPOINT      OpenAI-compatible endpoint URL
              AI_API_KEY       API key for the AI service
              AI_MODEL_ID      Model ID to use
              GITHUB_TOKEN     GitHub personal access token

            EXAMPLES:
              PRAgent review --owner "org" --repo "repo" --pr 123
              PRAgent review -o "org" -r "repo" -p 123 --post-comment
              PRAgent summary --owner "org" --repo "repo" --pr 123
              PRAgent approve --owner "org" --repo "repo" --pr 123 --auto
              PRAgent approve -o "org" -r "repo" -p 123 --auto --threshold major
              PRAgent approve --owner "org" --repo "repo" --pr 123 --comment "LGTM"
            """);
    }

    record ReviewOptions
    {
        public string? Owner { get; init; }
        public string? Repo { get; init; }
        public int PrNumber { get; init; }
        public bool PostComment { get; init; }

        public bool IsValid(out List<string> errors)
        {
            errors = new List<string>();

            if (string.IsNullOrEmpty(Owner))
                errors.Add("--owner is required");
            if (string.IsNullOrEmpty(Repo))
                errors.Add("--repo is required");
            if (PrNumber <= 0)
                errors.Add("--pr is required and must be a positive number");

            return errors.Count == 0;
        }
    }

    record SummaryOptions
    {
        public string? Owner { get; init; }
        public string? Repo { get; init; }
        public int PrNumber { get; init; }
        public bool PostComment { get; init; }

        public bool IsValid(out List<string> errors)
        {
            errors = new List<string>();

            if (string.IsNullOrEmpty(Owner))
                errors.Add("--owner is required");
            if (string.IsNullOrEmpty(Repo))
                errors.Add("--repo is required");
            if (PrNumber <= 0)
                errors.Add("--pr is required and must be a positive number");

            return errors.Count == 0;
        }
    }

    record ApproveOptions
    {
        public string? Owner { get; init; }
        public string? Repo { get; init; }
        public int PrNumber { get; init; }
        public bool Auto { get; init; }
        public ApprovalThreshold Threshold { get; init; } = ApprovalThreshold.Minor;
        public string? Comment { get; init; }
        public bool PostComment { get; init; }

        public bool IsValid(out List<string> errors)
        {
            errors = new List<string>();

            if (string.IsNullOrEmpty(Owner))
                errors.Add("--owner is required");
            if (string.IsNullOrEmpty(Repo))
                errors.Add("--repo is required");
            if (PrNumber <= 0)
                errors.Add("--pr is required and must be a positive number");

            return errors.Count == 0;
        }
    }
}
