using System.Diagnostics;
using BinAnalyzer.Core;
using BinAnalyzer.Core.Decoded;
using BinAnalyzer.Core.Models;
using BinAnalyzer.Engine;

namespace BinAnalyzer.Fuzz.Tests;

public enum OutcomeKind
{
    /// <summary>例外なく完了。</summary>
    Ok,
    /// <summary><see cref="DecodeException"/> で終了（不正入力に対する正規の失敗）。</summary>
    DecodeError,
    /// <summary>それ以外の例外（<c>IndexOutOfRange</c> / <c>NullReference</c> / <c>InvalidCast</c> 等 = バグ）。</summary>
    Unexpected,
    /// <summary>タイムアウト（無限ループ・指数的な処理の疑い）。</summary>
    Timeout,
}

public sealed record DecodeOutcome(ErrorMode Mode, OutcomeKind Kind, DecodedStruct? Root, IReadOnlyList<DecodeError> Errors, Exception? Exception, TimeSpan Elapsed)
{
    public bool IsCrash => Kind is OutcomeKind.Unexpected or OutcomeKind.Timeout;
}

/// <summary>
/// 1 回のデコードを専用スレッド（大きめのスタック）で走らせ、タイムアウトと例外の種類で結果を分類する。
/// タイムアウトしたスレッドは止められない（バックグラウンドスレッドなのでプロセス終了時に消える）。
/// </summary>
public static class FuzzRunner
{
    private const int StackSize = 64 * 1024 * 1024;

    public static DecodeOutcome Decode(FormatDefinition format, byte[] data, ErrorMode mode, TimeSpan? timeout = null, BinaryDecoder? decoder = null)
    {
        decoder ??= new BinaryDecoder();
        DecodeOutcome? outcome = null;
        var sw = Stopwatch.StartNew();
        var thread = new Thread(() =>
        {
            try
            {
                if (mode == ErrorMode.Stop)
                {
                    var root = decoder.Decode(data, format);
                    outcome = new DecodeOutcome(mode, OutcomeKind.Ok, root, [], null, sw.Elapsed);
                }
                else
                {
                    var result = decoder.DecodeWithRecovery(data, format, ErrorMode.Continue);
                    outcome = new DecodeOutcome(mode, OutcomeKind.Ok, result.Root, result.Errors, null, sw.Elapsed);
                }
            }
            catch (DecodeException ex)
            {
                outcome = new DecodeOutcome(mode, OutcomeKind.DecodeError, null, [], ex, sw.Elapsed);
            }
            catch (Exception ex)
            {
                outcome = new DecodeOutcome(mode, OutcomeKind.Unexpected, null, [], ex, sw.Elapsed);
            }
        }, StackSize) { IsBackground = true, Name = "fuzz-decode", Priority = ThreadPriority.Lowest };
        thread.Start();
        if (!thread.Join(timeout ?? FuzzConfig.Timeout))
            return new DecodeOutcome(mode, OutcomeKind.Timeout, null, [], null, sw.Elapsed);
        return outcome!;
    }

    /// <summary>失敗時のメッセージ。フォーマット・モード・シード・入力（Base64）を含み、そのまま再現に使える。</summary>
    public static string Describe(string formatFile, DecodeOutcome outcome, byte[] data, string origin)
    {
        var head = outcome.Kind switch
        {
            OutcomeKind.Timeout => $"timeout after {outcome.Elapsed.TotalSeconds:F1}s",
            OutcomeKind.Unexpected => $"{outcome.Exception!.GetType().Name}: {outcome.Exception.Message}",
            OutcomeKind.DecodeError => $"DecodeException: {outcome.Exception!.Message}",
            _ => "ok",
        };
        var trace = outcome.Exception is { } ex ? "\n" + string.Join("\n", (ex.StackTrace ?? "").Split('\n').Take(8)) : "";
        var payload = data.Length <= 8192 ? Convert.ToBase64String(data) : $"<{data.Length} bytes, omitted>";
        return $"{formatFile} [{outcome.Mode}] {origin} (seed {FuzzConfig.Seed}): {head}{trace}\ninput ({data.Length} B) base64: {payload}";
    }
}
