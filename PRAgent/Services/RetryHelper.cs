using System.Net;
using Microsoft.Extensions.Logging;

namespace PRAgent.Services;

/// <summary>
/// HTTP 429エラー時のリトライ処理を提供するヘルパークラス
/// </summary>
public static class RetryHelper
{
    private const int MaxRetries = 30;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(1);

    /// <summary>
    /// 429エラー時にリトライを行いながら非同期操作を実行します
    /// </summary>
    /// <typeparam name="T">戻り値の型</typeparam>
    /// <param name="operation">実行する操作</param>
    /// <param name="operationName">操作名（ログ用）</param>
    /// <param name="logger">ロガー</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    /// <returns>操作の結果</returns>
    public static async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        string operationName,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        var attempt = 0;

        while (true)
        {
            attempt++;

            try
            {
                return await operation();
            }
            catch (Exception ex) when (IsRetryableError(ex))
            {
                if (attempt >= MaxRetries)
                {
                    logger?.LogError(
                        "{OperationName} failed after {MaxRetries} attempts. Last error: {ErrorMessage}",
                        operationName, MaxRetries, ex.Message);
                    throw;
                }

                var retryDelay = GetRetryDelay(ex);

                logger?.LogWarning(
                    "{OperationName} attempt {Attempt}/{MaxRetries} failed with retryable error: {ErrorMessage}. " +
                    "Waiting {DelaySeconds} seconds before retry...",
                    operationName, attempt, MaxRetries, ex.Message, retryDelay.TotalSeconds);

                await Task.Delay(retryDelay, cancellationToken);
            }
        }
    }

    /// <summary>
    /// 429エラー時にリトライを行いながら非同期操作を実行します（戻り値なし）
    /// </summary>
    /// <param name="operation">実行する操作</param>
    /// <param name="operationName">操作名（ログ用）</param>
    /// <param name="logger">ロガー</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    public static async Task ExecuteWithRetryAsync(
        Func<Task> operation,
        string operationName,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        var attempt = 0;

        while (true)
        {
            attempt++;

            try
            {
                await operation();
                return;
            }
            catch (Exception ex) when (IsRetryableError(ex))
            {
                if (attempt >= MaxRetries)
                {
                    logger?.LogError(
                        "{OperationName} failed after {MaxRetries} attempts. Last error: {ErrorMessage}",
                        operationName, MaxRetries, ex.Message);
                    throw;
                }

                var retryDelay = GetRetryDelay(ex);

                logger?.LogWarning(
                    "{OperationName} attempt {Attempt}/{MaxRetries} failed with retryable error: {ErrorMessage}. " +
                    "Waiting {DelaySeconds} seconds before retry...",
                    operationName, attempt, MaxRetries, ex.Message, retryDelay.TotalSeconds);

                await Task.Delay(retryDelay, cancellationToken);
            }
        }
    }

    /// <summary>
    /// ストリーミングレスポンスを収集してリトライ処理を行います
    /// ストリーミング中にエラーが発生した場合、最初からやり直します
    /// </summary>
    /// <typeparam name="T">ストリーミング要素の型</typeparam>
    /// <param name="operationFactory">ストリーミング操作を作成する関数</param>
    /// <param name="operationName">操作名（ログ用）</param>
    /// <param name="logger">ロガー</param>
    /// <param name="cancellationToken">キャンセルトークン</param>
    /// <returns>収集された結果のリスト</returns>
    public static async Task<List<T>> ExecuteStreamingWithRetryAsync<T>(
        Func<IAsyncEnumerable<T>> operationFactory,
        string operationName,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        var attempt = 0;

        while (true)
        {
            attempt++;
            var results = new List<T>();

            try
            {
                await foreach (var item in operationFactory())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    results.Add(item);
                }

                return results;
            }
            catch (Exception ex) when (IsRetryableError(ex))
            {
                if (attempt >= MaxRetries)
                {
                    logger?.LogError(
                        "{OperationName} streaming failed after {MaxRetries} attempts. Last error: {ErrorMessage}",
                        operationName, MaxRetries, ex.Message);
                    throw;
                }

                var retryDelay = GetRetryDelay(ex);

                logger?.LogWarning(
                    "{OperationName} streaming attempt {Attempt}/{MaxRetries} failed with retryable error: {ErrorMessage}. " +
                    "Waiting {DelaySeconds} seconds before retry...",
                    operationName, attempt, MaxRetries, ex.Message, retryDelay.TotalSeconds);

                await Task.Delay(retryDelay, cancellationToken);
            }
        }
    }

    /// <summary>
    /// エラーがリトライ可能かどうかを判定します
    /// </summary>
    private static bool IsRetryableError(Exception ex)
    {
        // HTTP 429 (Too Many Requests)
        if (ex is HttpRequestException httpEx)
        {
            if (httpEx.StatusCode == HttpStatusCode.TooManyRequests)
            {
                return true;
            }
        }

        // OpenAI API のレート制限エラー
        var message = ex.Message.ToLowerInvariant();
        if (message.Contains("429") ||
            message.Contains("rate limit") ||
            message.Contains("too many requests") ||
            message.Contains("quota exceeded") ||
            message.Contains("requests per minute") ||
            message.Contains("tokens per minute"))
        {
            return true;
        }

        // 内部例外をチェック
        if (ex.InnerException != null)
        {
            return IsRetryableError(ex.InnerException);
        }

        return false;
    }

    /// <summary>
    /// リトライ待機時間を取得します
    /// </summary>
    private static TimeSpan GetRetryDelay(Exception ex)
    {
        // Retry-Afterヘッダーから待機時間を取得しようとする
        // デフォルトは1分
        return RetryDelay;
    }
}
