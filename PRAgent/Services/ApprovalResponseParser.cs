namespace PRAgent.Services;

public static class ApprovalResponseParser
{
    public static (bool ShouldApprove, string Reasoning, string? Comment) Parse(string response)
    {
        bool shouldApprove = false;
        string reasoning = string.Empty;
        string? comment = null;

        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("DECISION:", StringComparison.OrdinalIgnoreCase))
            {
                var value = trimmed.Substring("DECISION:".Length).Trim();
                shouldApprove = value.Equals("APPROVE", StringComparison.OrdinalIgnoreCase);
            }
            else if (trimmed.StartsWith("REASONING:", StringComparison.OrdinalIgnoreCase))
            {
                reasoning = trimmed.Substring("REASONING:".Length).Trim();
            }
            else if (trimmed.StartsWith("APPROVAL_COMMENT:", StringComparison.OrdinalIgnoreCase))
            {
                var value = trimmed.Substring("APPROVAL_COMMENT:".Length).Trim();
                if (!value.Equals("N/A", StringComparison.OrdinalIgnoreCase))
                {
                    comment = value;
                }
            }
        }

        return (shouldApprove, reasoning, comment);
    }
}
