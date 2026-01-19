using PRAgent.Models;
using PRAgent.Services;
using Serilog;

namespace PRAgent.CommandLine;

/// <summary>
/// Handles the comment command
/// </summary>
public class CommentCommandHandler : ICommandHandler
{
    private readonly CommentCommandOptions _options;
    private readonly IGitHubService _gitHubService;

    public CommentCommandHandler(CommentCommandOptions options, IGitHubService gitHubService)
    {
        _options = options;
        _gitHubService = gitHubService;
    }

    public async Task<int> ExecuteAsync()
    {
        if (!_options.IsValid(out var errors))
        {
            Log.Error("Invalid options:");
            foreach (var error in errors)
            {
                Log.Error("  - {Error}", error);
            }
            return 1;
        }

        try
        {
            // 最初にPR情報を取得してファイルを確認
            var pr = await _gitHubService.GetPullRequestAsync(_options.Owner!, _options.Repo!, _options.PrNumber);

            Console.WriteLine("以下のコメントを投稿しますか？");
            Console.WriteLine($"PR: {pr.Title} (#{_options.PrNumber})");
            Console.WriteLine();

            foreach (var (comment, index) in _options.Comments.Select((c, i) => (c, i)))
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
                var commentList = _options.Comments
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
                    await _gitHubService.CreateMultipleLineCommentsAsync(
                        _options.Owner!,
                        _options.Repo!,
                        _options.PrNumber,
                        commentList
                    );

                    Console.WriteLine("コメントを投稿しました.");
                }

                // --approveオプションが指定されていた場合はPRを承認
                if (_options.Approve)
                {
                    Console.WriteLine("\nPRを承認しますか？ [承認する] [キャンセル]: ");
                    var approveInput = Console.ReadLine()?.Trim().ToLowerInvariant();

                    if (approveInput == "承認する" || approveInput == "approve" || approveInput == "y" || approveInput == "yes")
                    {
                        await _gitHubService.ApprovePullRequestAsync(
                            _options.Owner!,
                            _options.Repo!,
                            _options.PrNumber,
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
}
