using PRAgent.Models;

namespace PRAgent.Services;

public static class ApprovalThresholdHelper
{
    public static string GetDescription(ApprovalThreshold threshold)
    {
        return threshold switch
        {
            ApprovalThreshold.Critical => "CRITICAL: No critical issues allowed",
            ApprovalThreshold.Major => "MAJOR: No major or critical issues allowed",
            ApprovalThreshold.Minor => "MINOR: No minor, major, or critical issues allowed",
            ApprovalThreshold.None => "NONE: Always approve",
            _ => "MINOR: No minor, major, or critical issues allowed"
        };
    }
}
