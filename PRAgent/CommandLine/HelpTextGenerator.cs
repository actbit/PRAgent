namespace PRAgent.CommandLine;

/// <summary>
/// Generates help text for the CLI application
/// </summary>
public static class HelpTextGenerator
{
    private const string HelpText = """
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
        """;

    /// <summary>
    /// Displays the help text to the console
    /// </summary>
    public static void ShowHelp()
    {
        Console.WriteLine(HelpText);
    }
}
