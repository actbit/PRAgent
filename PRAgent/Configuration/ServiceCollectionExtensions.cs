using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PRAgent.Agents;
using PRAgent.Agents.SK;
using PRAgent.Models;
using PRAgent.Services;
using PRAgent.Services.SK;
using PRAgent.Validators;
using Serilog;

namespace PRAgent.Configuration;

/// <summary>
/// Extension methods for configuring services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds core application services to the service collection
    /// </summary>
    public static IServiceCollection AddPRAgentServices(
        this IServiceCollection services,
        IConfiguration configuration)
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

        // GitHub Token (for GitHubService constructor)
        services.AddSingleton(_ => prSettings.GitHubToken ?? string.Empty);

        // GitHub Service
        services.AddSingleton<IGitHubService, GitHubService>();

        // Kernel Service
        services.AddSingleton<IKernelService, KernelService>();

        // Configuration Service
        services.AddSingleton<IConfigurationService, ConfigurationService>();

        // Data Services
        services.AddSingleton<PullRequestDataService>();

        // Detailed Comment Agent
        services.AddSingleton<IDetailedCommentAgent, DetailedCommentAgent>();

        // SK Agents (Semantic Kernel Agent Framework)
        services.AddSingleton<PRAgentFactory>();
        services.AddSingleton<SKReviewAgent>();
        services.AddSingleton<SKSummaryAgent>();
        services.AddSingleton<SKApprovalAgent>();

        // Agent Orchestrator - SKAgentOrchestratorServiceを使用
        services.AddSingleton<IAgentOrchestratorService, SKAgentOrchestratorService>();

        // PR Analysis Service
        services.AddSingleton<IPRAnalysisService, PRAnalysisService>();

        return services;
    }
}
