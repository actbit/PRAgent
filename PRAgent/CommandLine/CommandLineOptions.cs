using PRAgent.Models;

namespace PRAgent.CommandLine;

/// <summary>
/// Base record for command options with common properties
/// </summary>
public abstract record CommandOptions
{
    public string? Owner { get; init; }
    public string? Repo { get; init; }
    public int PrNumber { get; init; }
    public bool PostComment { get; init; }
    public string? Language { get; init; } = null;

    /// <summary>
    /// Validates the common options shared across all commands
    /// </summary>
    protected virtual bool ValidateCommon(out List<string> errors)
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

/// <summary>
/// Options for the review command
/// </summary>
public record ReviewOptions : CommandOptions
{
    public bool IsValid(out List<string> errors)
    {
        return ValidateCommon(out errors);
    }
}

/// <summary>
/// Options for the summary command
/// </summary>
public record SummaryOptions : CommandOptions
{
    public bool IsValid(out List<string> errors)
    {
        return ValidateCommon(out errors);
    }
}

/// <summary>
/// Options for the approve command
/// </summary>
public record ApproveOptions : CommandOptions
{
    public bool Auto { get; init; }
    public ApprovalThreshold Threshold { get; init; } = ApprovalThreshold.Minor;
    public string? Comment { get; init; }

    public bool IsValid(out List<string> errors)
    {
        return ValidateCommon(out errors);
    }
}
