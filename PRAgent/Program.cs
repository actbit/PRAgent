using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PRAgent.Agents;
using PRAgent.Agents.SK;
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

                    // Agents (現在稼働中)
                    services.AddSingleton<ReviewAgent>();
                    services.AddSingleton<ApprovalAgent>();
                    services.AddSingleton<SummaryAgent>();

                    // SK Agents (Semantic Kernel Agent Framework)
                    services.AddSingleton<PRAgentFactory>();
                    services.AddSingleton<SKReviewAgent>();
                    services.AddSingleton<SKSummaryAgent>();
                    services.AddSingleton<SKApprovalAgent>();

                    // Agent Orchestrator (現在は既存の実装を使用)
                    // TODO: 将来的にSKAgentOrchestratorServiceに切り替え
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

                case "comment":
                    return await RunCommentCommandAsync(args, services);

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

        if (options.AutoCommit)
        {
            // Function Callingモードでレビューを実行
            return await RunReviewWithAutoCommitAsync(args, services);
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

    static async Task<int> RunReviewWithAutoCommitAsync(string[] args, IServiceProvider services)
    {
        var options = ParseReviewOptions(args);

        // 必要なサービスを取得
        var reviewAgent = services.GetRequiredService<ReviewAgent>();
        var gitHubService = services.GetRequiredService<IGitHubService>();

        // バッファを作成
        var buffer = new PRActionBuffer();
        var executor = new PRActionExecutor(gitHubService, options.Owner!, options.Repo!, options.PrNumber);

        Console.WriteLine();
        Console.WriteLine("=== PRレビュー（Function Callingモード） ===");
        Console.WriteLine();

        // レビューを実行してバッファにアクションを蓄積
        var result = await reviewAgent.ReviewWithActionsAsync(
            options.Owner!,
            options.Repo!,
            options.PrNumber,
            buffer);

        Console.WriteLine(result);
        Console.WriteLine();

        // プレビューを表示
        var preview = executor.CreatePreview(buffer);
        Console.WriteLine(preview);
        Console.WriteLine();

        // 確認を求める
        if (buffer.GetState().LineCommentCount > 0 ||
            buffer.GetState().ReviewCommentCount > 0 ||
            buffer.GetState().SummaryCount > 0)
        {
            Console.Write("上記のアクションを投稿しますか？ [投稿する] [キャンセル]: ");
            var input = Console.ReadLine()?.Trim().ToLowerInvariant();

            if (input == "投稿する" || input == "post" || input == "y" || input == "yes")
            {
                // アクションを実行
                var actionResult = await executor.ExecuteAsync(buffer);

                if (actionResult.Success)
                {
                    Console.WriteLine();
                    Console.WriteLine($"✓ {actionResult.Message}");
                    if (actionResult.ApprovalUrl != null)
                        Console.WriteLine($"  承認URL: {actionResult.ApprovalUrl}");
                    return 0;
                }
                else
                {
                    Console.WriteLine();
                    Console.WriteLine($"✗ {actionResult.Message}");
                    return 1;
                }
            }
            else
            {
                Console.WriteLine("キャンセルしました。");
                return 0;
            }
        }
        else
        {
            Console.WriteLine("投稿するアクションがありませんでした。");
            return 0;
        }
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

        if (options.AutoCommit)
        {
            // Function Callingモードでサマリーを作成
            return await RunSummaryWithAutoCommitAsync(args, services);
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

    static async Task<int> RunSummaryWithAutoCommitAsync(string[] args, IServiceProvider services)
    {
        var options = ParseSummaryOptions(args);

        // 必要なサービスを取得
        var summaryAgent = services.GetRequiredService<SummaryAgent>();
        var gitHubService = services.GetRequiredService<IGitHubService>();

        // バッファを作成
        var buffer = new PRActionBuffer();
        var executor = new PRActionExecutor(gitHubService, options.Owner!, options.Repo!, options.PrNumber);

        Console.WriteLine();
        Console.WriteLine("=== PRサマリー（Function Callingモード） ===");
        Console.WriteLine();

        // サマリーを作成してバッファにアクションを蓄積
        var result = await summaryAgent.SummarizeWithActionsAsync(
            options.Owner!,
            options.Repo!,
            options.PrNumber,
            buffer);

        Console.WriteLine(result);
        Console.WriteLine();

        // プレビューを表示
        var preview = executor.CreatePreview(buffer);
        Console.WriteLine(preview);
        Console.WriteLine();

        // 確認を求める
        if (buffer.GetState().SummaryCount > 0 || buffer.GetState().HasGeneralComment)
        {
            Console.Write("上記のアクションを投稿しますか？ [投稿する] [キャンセル]: ");
            var input = Console.ReadLine()?.Trim().ToLowerInvariant();

            if (input == "投稿する" || input == "post" || input == "y" || input == "yes")
            {
                // アクションを実行
                var actionResult = await executor.ExecuteAsync(buffer);

                if (actionResult.Success)
                {
                    Console.WriteLine();
                    Console.WriteLine($"✓ {actionResult.Message}");
                    return 0;
                }
                else
                {
                    Console.WriteLine();
                    Console.WriteLine($"✗ {actionResult.Message}");
                    return 1;
                }
            }
            else
            {
                Console.WriteLine("キャンセルしました。");
                return 0;
            }
        }
        else
        {
            Console.WriteLine("投稿するアクションがありませんでした。");
            return 0;
        }
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

    static async Task<int> RunCommentCommandAsync(string[] args, IServiceProvider services)
    {
        // commentコマンドのみの場合はヘルプを表示
        if (args.Length == 1)
        {
            Console.WriteLine("コメント投稿機能の使い方:");
            Console.WriteLine();
            Console.WriteLine("基本形式:");
            Console.WriteLine("  comment @123 コメント内容");
            Console.WriteLine("  comment src/file.cs@123 コメント内容");
            Console.WriteLine();
            Console.WriteLine("Suggestion付き:");
            Console.WriteLine("  comment @123 コメント内容 --suggestion \"修正コード\"");
            Console.WriteLine();
            Console.WriteLine("承認付き:");
            Console.WriteLine("  comment @123 コメント内容 --approve");
            Console.WriteLine();
            Console.WriteLine("複数コメント:");
            Console.WriteLine("  comment @123 コメント1 @456 コメント2");
            Console.WriteLine();
            Console.WriteLine("行範囲指定:");
            Console.WriteLine("  comment @45-67 このセクション全体を見直してください");
            Console.WriteLine();
            Console.WriteLine("必須オプション:");
            Console.WriteLine("  --owner, -o      リポジトリオーナー");
            Console.WriteLine("  --repo, -r        リポジトリ名");
            Console.WriteLine("  --pr, -p          プルリクエスト番号");
            Console.WriteLine("  --approve         コメント投稿後にPRを承認");
            Console.WriteLine();
            Console.WriteLine("例:");
            Console.WriteLine("  PRAgent comment --owner myorg --repo myrepo --pr 123 @150 ここを修正してください");
            Console.WriteLine("  PRAgent comment -o myorg -r myrepo -p 123 @200 不適切なコード --suggestion \"適切なコード\"");
            Console.WriteLine("  PRAgent comment --owner myorg --repo myrepo --pr 123 @100 \"修正が必要\" --approve");
            return 0;
        }

        var options = CommentCommandOptions.Parse(args);

        if (!options.IsValid(out var errors))
        {
            Log.Error("Invalid options:");
            foreach (var error in errors)
            {
                Log.Error("  - {Error}", error);
            }
            return 1;
        }

        var gitHubService = services.GetRequiredService<IGitHubService>();

        try
        {
            // 最初にPR情報を取得してファイルを確認
            var pr = await gitHubService.GetPullRequestAsync(options.Owner!, options.Repo!, options.PrNumber);

            Console.WriteLine("以下のコメントを投稿しますか？");
            Console.WriteLine($"PR: {pr.Title} (#{options.PrNumber})");
            Console.WriteLine();

            foreach (var (comment, index) in options.Comments.Select((c, i) => (c, i)))
            {
                if (comment == null) continue;

                Console.WriteLine($"コメント {index + 1}:");
                Console.WriteLine($"  ファイル: {comment.FilePath}");
                Console.WriteLine($"  行数: {comment.LineNumber}");
                Console.WriteLine($"  コメント: {comment.CommentText}");

                if (!string.IsNullOrEmpty(comment.SuggestionText))
                {
                    Console.WriteLine($"  修正案: {comment.SuggestionText}");
                }
                Console.WriteLine();
            }

            Console.Write("[投稿する] [キャンセル]: ");
            var input = Console.ReadLine()?.Trim().ToLowerInvariant();

            if (input == "投稿する" || input == "post" || input == "y" || input == "yes")
            {
                // 複数のコメントを一度に投稿
                var commentList = options.Comments
                    .Where(c => c != null)
                    .Select(c => (
                        FilePath: c!.FilePath,
                        LineNumber: c.LineNumber,
                        Comment: c.CommentText,
                        Suggestion: c.SuggestionText
                    ))
                    .ToList();

                if (commentList.Any())
                {
                    await gitHubService.CreateMultipleLineCommentsAsync(
                        options.Owner!,
                        options.Repo!,
                        options.PrNumber,
                        commentList
                    );

                    Console.WriteLine("コメントを投稿しました.");
                }

                // --approveオプションが指定されていた場合はPRを承認
                if (options.Approve)
                {
                    Console.WriteLine("\nPRを承認しますか？ [承認する] [キャンセル]: ");
                    var approveInput = Console.ReadLine()?.Trim().ToLowerInvariant();

                    if (approveInput == "承認する" || approveInput == "approve" || approveInput == "y" || approveInput == "yes")
                    {
                        await gitHubService.ApprovePullRequestAsync(
                            options.Owner!,
                            options.Repo!,
                            options.PrNumber,
                            "Approved by PRAgent with comments"
                        );
                        Console.WriteLine("PRを承認しました.");
                    }
                    else
                    {
                        Console.WriteLine("PRの承認をキャンセルしました.");
                    }
                }

                return 0;
            }
            else
            {
                Console.WriteLine("キャンセルしました。");
                return 0;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Comment posting failed");
            Console.WriteLine("コメントの投稿に失敗しました。");
            return 1;
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
                case "--auto-commit":
                    options = options with { AutoCommit = true };
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
                case "--auto-commit":
                    options = options with { AutoCommit = true };
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
                case "--auto-commit":
                    options = options with { AutoCommit = true };
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
              comment     Post a comment on a specific line
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

            COMMENT OPTIONS:
              --owner, -o      Repository owner (required)
              --repo, -r       Repository name (required)
              --pr, -p         Pull request number (required)
              --approve        Approve the PR after posting comments

            COMMENT FORMAT:
              comment @123 "This needs improvement"
              comment @45-67 "Review this section"
              comment src/file.cs@123 "Fix this" --suggestion "Fixed code"
              comment @123 "Comment1" @456 "Comment2"

            EXAMPLES:
              PRAgent review --owner "org" --repo "repo" --pr 123
              PRAgent review -o "org" -r "repo" -p 123 --post-comment
              PRAgent summary --owner "org" --repo "repo" --pr 123
              PRAgent approve --owner "org" --repo "repo" --pr 123 --auto
              PRAgent approve -o "org" -r "repo" -p 123 --auto --threshold major
              PRAgent approve --owner "org" --repo "repo" --pr 123 --comment "LGTM"
              PRAgent comment --owner "org" --repo "repo" --pr 123 @150 "This part is wrong" --suggestion "This part is correct"
              PRAgent comment -o "org" -r "repo" -p 123 @100 "Fix this" --approve
            """);
    }

    record ReviewOptions
    {
        public string? Owner { get; init; }
        public string? Repo { get; init; }
        public int PrNumber { get; init; }
        public bool PostComment { get; init; }
        public bool AutoCommit { get; init; }

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
        public bool AutoCommit { get; init; }

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
        public bool AutoCommit { get; init; }

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