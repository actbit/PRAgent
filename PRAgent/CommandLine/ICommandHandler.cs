namespace PRAgent.CommandLine;

/// <summary>
/// Base interface for command handlers
/// </summary>
public interface ICommandHandler
{
    /// <summary>
    /// Executes the command and returns the exit code
    /// </summary>
    Task<int> ExecuteAsync();
}
