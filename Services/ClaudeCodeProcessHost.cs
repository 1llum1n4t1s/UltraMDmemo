using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace UltraMDmemo.Services;

/// <summary>
/// ローカルインストールした Node.js + cli.js を経由して Claude Code を実行する。
/// IClaudeCodeSetupService からパスを取得し、node cli.js -p &lt;prompt&gt; を実行する。
/// </summary>
public sealed class ClaudeCodeProcessHost : IClaudeCodeProcessHost
{
    private const int TimeoutMs = 120_000;
    private readonly IClaudeCodeSetupService _setupService;

    public ClaudeCodeProcessHost(IClaudeCodeSetupService setupService)
    {
        _setupService = setupService;
    }

    public async Task<string> ExecuteAsync(string prompt, string stdinText, CancellationToken ct = default)
    {
        var nodePath = _setupService.GetNodePath();
        var cliJsPath = _setupService.GetCliJsPath();

        if (!File.Exists(nodePath))
            throw new InvalidOperationException("Node.js が見つかりません。セットアップを実行してください。");
        if (!File.Exists(cliJsPath))
            throw new InvalidOperationException("Claude Code CLI が見つかりません。セットアップを実行してください。");

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeoutMs);

        var psi = new ProcessStartInfo
        {
            FileName = nodePath,
            ArgumentList = { cliJsPath, "-p", prompt },
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardInputEncoding = Encoding.UTF8,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        // 非対話モードで実行
        psi.Environment["CI"] = "true";
        // PATH にローカル Node.js の bin ディレクトリを追加
        ClaudeCodeSetupService.AddNodeToPath(psi);

        using var process = new Process { StartInfo = psi };
        process.Start();

        await process.StandardInput.WriteAsync(stdinText.AsMemory(), timeoutCts.Token);
        process.StandardInput.Close();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
        var stderrTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);

        try
        {
            await Task.WhenAll(stdoutTask, stderrTask);
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            throw new TimeoutException($"Claude CLI が {TimeoutMs / 1000} 秒以内に応答しませんでした。");
        }

        var stdout = stdoutTask.Result;
        var stderr = stderrTask.Result;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Claude CLI がコード {process.ExitCode} で終了しました: {stderr}");
        }

        return stdout;
    }

    public Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        // ローカルインストールの場合、ファイル存在チェックで判定
        return Task.FromResult(_setupService.IsNodeJsInstalled && _setupService.IsCliInstalled);
    }
}
