using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PRAgent.Agents;
using PRAgent.Agents.SK;
using PRAgent.CommandLine;
using PRAgent.Models;
using PRAgent.Services;
using PRAgent.Services.SK;
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

                    // PRAgent Configuration
                    var prAgentConfig = configuration.GetSection("PRAgent").Get<PRAgentConfig>()
                        ?? new PRAgentConfig();
                    services.AddSingleton(_ => prAgentConfig);

                    // GitHub Service
                    services.AddSingleton<IGitHubService, GitHubService>();

                    // Kernel Service
                    services.AddSingleton<IKernelService, KernelService>();

                    // Configuration Service
                    services.AddSingleton<IConfigurationService, ConfigurationService>();

                    // Data Services
                    services.AddSingleton<PullRequestDataService>();

                    // Agents (現在稼働中)
                    services.AddSingleton<ReviewAgent>();
                    services.AddSingleton<ApprovalAgent>();
                    services.AddSingleton<SummaryAgent>();

                    // SK Agents (Semantic Kernel Agent Framework)
                    services.AddSingleton<PRAgentFactory>();
                    services.AddSingleton<SKReviewAgent>();
                    services.AddSingleton<SKSummaryAgent>();
                    services.AddSingleton<SKApprovalAgent>();

                    // Agent Orchestrator - AgentFramework設定に応じて切り替え
                    if (prAgentConfig.AgentFramework?.Enabled == true)
                    {
                        services.AddSingleton<IAgentOrchestratorService, SKAgentOrchestratorService>();
                        Log.Information("Using SKAgentOrchestratorService (Agent Framework enabled)");
                    }
                    else
                    {
                        services.AddSingleton<IAgentOrchestratorService, AgentOrchestratorService>();
                        Log.Information("Using AgentOrchestratorService (standard mode)");
                    }

                    // PR Analysis Service
                    services.AddSingleton<IPRAnalysisService, PRAnalysisService>();
                })
                .Build();

            // Run CLI
            return await RunCliAsync(args, host.Services);
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

    static async Task<int> RunCliAsync(string[] args, IServiceProvider services)
    {
        if (args.Length == 0)
        {
            HelpTextGenerator.ShowHelp();
            return 0;
        }

        var command = args[0].ToLowerInvariant();

        try
        {
            var commandHandler = command switch
            {
                "review" => CreateReviewHandler(args, services),
                "summary" => CreateSummaryHandler(args, services),
                "approve" => CreateApproveHandler(args, services),
                "comment" => CreateCommentHandler(args, services),
                "help" or "--help" or "-h" => null,
                _ => null
            };

            if (commandHandler is not null)
            {
                return await commandHandler.ExecuteAsync();
            }

            Log.Error("Unknown command: {Command}", command);
            HelpTextGenerator.ShowHelp();
            return 1;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Command failed: {Command}", command);
            return 1;
        }
    }

    private static ICommandHandler CreateReviewHandler(string[] args, IServiceProvider services)
    {
        var options = CommandLineParser.ParseReviewOptions(args);
        var prAnalysisService = services.GetRequiredService<IPRAnalysisService>();
        return new ReviewCommandHandler(options, prAnalysisService);
    }

    private static ICommandHandler CreateSummaryHandler(string[] args, IServiceProvider services)
    {
        var options = CommandLineParser.ParseSummaryOptions(args);
        var prAnalysisService = services.GetRequiredService<IPRAnalysisService>();
        return new SummaryCommandHandler(options, prAnalysisService);
    }

    private static ICommandHandler CreateApproveHandler(string[] args, IServiceProvider services)
    {
        var options = CommandLineParser.ParseApproveOptions(args);
        var prAnalysisService = services.GetRequiredService<IPRAnalysisService>();
        var gitHubService = services.GetRequiredService<IGitHubService>();
        return new ApproveCommandHandler(options, prAnalysisService, gitHubService);
    }

    private static ICommandHandler CreateCommentHandler(string[] args, IServiceProvider services)
    {
        var options = CommentCommandOptions.Parse(args);
        var gitHubService = services.GetRequiredService<IGitHubService>();
        return new CommentCommandHandler(options, gitHubService);
    }
}
