using System.Text.RegularExpressions;

namespace PRAgent.Models;

public record CommentCommandOptions
{
    public string? Owner { get; init; }
    public string? Repo { get; init; }
    public int PrNumber { get; init; }
    public List<CommentTarget> Comments { get; init; } = new();
    public bool Approve { get; init; }
    public bool IsValid(out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrEmpty(Owner))
            errors.Add("--owner is required");
        if (string.IsNullOrEmpty(Repo))
            errors.Add("--repo is required");
        if (PrNumber <= 0)
            errors.Add("--pr is required and must be a positive number");

        // 有効なコメントが1つ以上あるかチェック
        if (!Comments.Any())
            errors.Add("No valid comments specified");

        foreach (var (comment, index) in Comments.Select((c, i) => (c, i)))
        {
            if (comment == null)
            {
                errors.Add($"Comment {index + 1}: Invalid comment format");
                continue;
            }

            // パース段階でチェック済みのため、ここでは不要
            // if (string.IsNullOrWhiteSpace(comment.CommentText))
            // {
            //     if (string.IsNullOrEmpty(comment.CommentText))
            //     {
            //         errors.Add($"Comment {index + 1}: Comment text is required");
            //     }
            //     else
            //     {
            //         errors.Add($"Comment {index + 1}: Comment text cannot be whitespace only");
            //     }
            // }

            if (comment.LineNumber <= 0)
            {
                errors.Add($"Comment {index + 1}: Line number must be greater than 0");
            }

            if (!string.IsNullOrEmpty(comment.FilePath) && !IsValidFilePath(comment.FilePath))
            {
                errors.Add($"Comment {index + 1}: Invalid file path format");
            }
        }

        return errors.Count == 0;
    }

    private static bool IsValidFilePath(string path)
    {
        try
        {
            // 基本的なファイルパスの検証
            return !string.IsNullOrWhiteSpace(path) &&
                   !path.StartsWith("..") &&
                   !path.Contains("/") &&
                   !path.Contains("\\");
        }
        catch
        {
            return false;
        }
    }

    public static CommentCommandOptions Parse(string[] args)
    {
        var options = new CommentCommandOptions();
        var commentArgs = new List<string>();

        // commentコマンド以降の引数を収集
        bool inCommentSection = false;
        foreach (var arg in args)
        {
            if (arg == "comment")
            {
                inCommentSection = true;
                continue;
            }

            if (inCommentSection)
            {
                commentArgs.Add(arg);
            }
        }

        options = ParseCommentOptions(args, options);
        options = options with { Comments = ParseComments(commentArgs.ToArray()) };

        return options;
    }

    private static CommentCommandOptions ParseCommentOptions(string[] args, CommentCommandOptions current)
    {
        var options = current;

        for (int i = 0; i < args.Length; i++)
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
                case "--approve":
                    options = options with { Approve = true };
                    break;
            }
        }

        return options;
    }

    private static List<CommentTarget> ParseComments(string[] commentArgs)
    {
        var comments = new List<CommentTarget>();

        // オプション引数（--owner, --repo, --pr, --approve）とその値をスキップしながらパース
        // ただし、--suggestionはコメント処理内で扱う

        var skipNext = false;

        for (int i = 0; i < commentArgs.Length; i++)
        {
            // 前の引数がオプションの値だった場合はスキップ
            if (skipNext)
            {
                skipNext = false;
                continue;
            }

            var arg = commentArgs[i];

            // --owner, --repo, --pr, --approve などのオプションはスキップ
            if (arg == "--owner" || arg == "-o" || arg == "--repo" || arg == "-r" ||
                arg == "--pr" || arg == "-p" || arg == "--number" || arg == "--approve")
            {
                skipNext = true;
                continue;
            }

            // --suggestion は最後のコメントにsuggestionを追加
            if (arg == "--suggestion" || arg.Equals("--suggestion", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < commentArgs.Length && comments.Count > 0)
                {
                    var suggestionText = commentArgs[i + 1];
                    if (!string.IsNullOrWhiteSpace(suggestionText))
                    {
                        var lastComment = comments[^1];
                        comments[^1] = lastComment with { SuggestionText = suggestionText };
                    }
                    i++; // --suggestionの値もスキップ
                }
                continue;
            }

            // @で始まる、または@を含む引数はコメントとして処理
            if (arg.StartsWith("@") || arg.Contains("@"))
            {
                string lineRange;

                if (arg.StartsWith("@"))
                {
                    // @123 形式
                    lineRange = arg.Substring(1);
                }
                else
                {
                    // src/file.cs@123 形式 - 全体を渡す
                    lineRange = arg;
                }

                if (TryParseLineRange(lineRange, out var lineNumber, out var filePath) && lineNumber > 0)
                {
                    // 次の引数をコメントテキストとして取得
                    if (i + 1 < commentArgs.Length)
                    {
                        var textArg = commentArgs[i + 1];

                        // 次の引数がオプションでない場合はコメントテキストとして扱う
                        if (!textArg.StartsWith("-") && !string.IsNullOrWhiteSpace(textArg))
                        {
                            comments.Add(new CommentTarget(
                                LineNumber: lineNumber,
                                FilePath: filePath ?? "src/index.cs",
                                CommentText: textArg,
                                SuggestionText: null // 後で --suggestion で設定される
                            ));
                            i++; // コメントテキストをスキップ
                        }
                    }
                }
            }
        }

        return comments;
    }

    private static bool TryParseLineRange(string lineRange, out int lineNumber, out string? filePath)
    {
        lineNumber = 0;
        filePath = null;

        if (string.IsNullOrWhiteSpace(lineRange))
            return false;

        // ファイルパスを含む形式（例: src/file.cs@123）を最初にチェック
        if (lineRange.Contains("@"))
        {
            var parts = lineRange.Split('@', 2);
            if (parts.Length == 2 &&
                int.TryParse(parts[1], out var fileLine) &&
                fileLine > 0 &&  // 行数は正の数のみ
                !string.IsNullOrWhiteSpace(parts[0]))
            {
                lineNumber = fileLine;
                filePath = parts[0];
                return true;
            }
        }

        // 行範囲指定形式（例: 123, 45-67）
        if (lineRange.Contains("-"))
        {
            var parts = lineRange.Split('-', 2);
            if (parts.Length == 2 && int.TryParse(parts[0], out var startLine) && startLine > 0)
            {
                lineNumber = startLine;
                return true;
            }
        }
        // 単一行指定形式（例: 123）
        else if (int.TryParse(lineRange, out var singleLine) && singleLine > 0)
        {
            lineNumber = singleLine;
            return true;
        }

        return false;
    }
}

public record CommentTarget(
    int LineNumber,
    string FilePath,
    string CommentText,
    string? SuggestionText
);