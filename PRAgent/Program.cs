using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PRAgent.CommandLine;
using PRAgent.Configuration;
using PRAgent.Services;
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
                    services.AddPRAgentServices(configuration);
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
                "help" or "--help" or "-h" => null,
                _ => null
            };

            if (commandHandler is not null)
            {
                return await commandHandler.ExecuteAsync();
            }

            if (command is "help" or "--help" or "-h")
            {
                HelpTextGenerator.ShowHelp();
                return 0;
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
}
