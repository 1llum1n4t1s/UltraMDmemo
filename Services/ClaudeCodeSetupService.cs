using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace UltraMDmemo.Services;

/// <summary>
/// Node.js と Claude Code CLI のローカルインストール・認証を管理する。
/// ShogunGUI 準拠の実装。
/// </summary>
public sealed class ClaudeCodeSetupService : IClaudeCodeSetupService
{
    private const string NodeVersion = "v20.18.1";
    private const string NodeDownloadUrl = $"https://nodejs.org/dist/{NodeVersion}/node-{NodeVersion}-win-x64.zip";
    private const int LoginPollIntervalMs = 10_000;
    private const int LoginMaxPolls = 60; // 10分

    private static readonly HttpClient HttpClient = new();

    public bool IsNodeJsInstalled => File.Exists(AppPaths.NodeExePath);

    public bool IsCliInstalled => File.Exists(AppPaths.CliJsPath);

    public string GetNodePath() => AppPaths.NodeExePath;

    public string GetCliJsPath() => AppPaths.CliJsPath;

    public async Task EnsureNodeJsAsync(IProgress<string>? progress = null, CancellationToken ct = default)
    {
        if (IsNodeJsInstalled)
        {
            progress?.Report("Node.js は既にインストール済みです。");
            return;
        }

        progress?.Report("Node.js をダウンロード中...");
        AppPaths.EnsureDirectories();

        var tempZip = Path.Combine(Path.GetTempPath(), $"node-{NodeVersion}-win-x64.zip");
        try
        {
            using var response = await HttpClient.GetAsync(NodeDownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            await using var fileStream = File.Create(tempZip);
            await response.Content.CopyToAsync(fileStream, ct);
            fileStream.Close();

            progress?.Report("Node.js を展開中...");

            // ZIP 内は node-vXX.XX.X-win-x64/ フォルダがルート
            var tempExtract = Path.Combine(Path.GetTempPath(), $"node-extract-{Guid.NewGuid():N}");
            ZipFile.ExtractToDirectory(tempZip, tempExtract, overwriteFiles: true);

            // 展開先のサブフォルダを見つける
            var extractedDir = Directory.GetDirectories(tempExtract)[0];

            // 既存のディレクトリを削除して移動
            if (Directory.Exists(AppPaths.NodeJsDir))
                Directory.Delete(AppPaths.NodeJsDir, recursive: true);

            Directory.Move(extractedDir, AppPaths.NodeJsDir);

            // 一時展開先を削除
            if (Directory.Exists(tempExtract))
                Directory.Delete(tempExtract, recursive: true);

            progress?.Report("Node.js のインストールが完了しました。");
        }
        finally
        {
            if (File.Exists(tempZip))
                File.Delete(tempZip);
        }
    }

    public async Task EnsureCliAsync(IProgress<string>? progress = null, CancellationToken ct = default)
    {
        if (IsCliInstalled)
        {
            progress?.Report("Claude Code CLI は既にインストール済みです。");
            return;
        }

        if (!IsNodeJsInstalled)
            throw new InvalidOperationException("Node.js がインストールされていません。先に EnsureNodeJsAsync を実行してください。");

        progress?.Report("Claude Code CLI をインストール中...");
        AppPaths.EnsureDirectories();

        // ローカル Node.js の npm を使ってインストール
        var npmCliJs = Path.Combine(AppPaths.NodeJsDir, "node_modules", "npm", "bin", "npm-cli.js");
        var psi = new ProcessStartInfo
        {
            FileName = AppPaths.NodeExePath,
            ArgumentList =
            {
                npmCliJs,
                "install",
                "--global",
                "--prefix", AppPaths.NpmDir,
                "--cache", AppPaths.NpmCacheDir,
                "@anthropic-ai/claude-code"
            },
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        AddNodeToPath(psi);

        using var process = new Process { StartInfo = psi };
        process.Start();

        var stdout = await process.StandardOutput.ReadToEndAsync(ct);
        var stderr = await process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Claude Code CLI のインストールに失敗しました (exit {process.ExitCode}): {stderr}");
        }

        progress?.Report("Claude Code CLI のインストールが完了しました。");
    }

    public async Task<bool> IsLoggedInAsync(CancellationToken ct = default)
    {
        if (!IsNodeJsInstalled || !IsCliInstalled)
            return false;

        try
        {
            using var proc = new Process();
            proc.StartInfo.FileName = AppPaths.NodeExePath;
            proc.StartInfo.Arguments = $"\"{AppPaths.CliJsPath}\" config get";
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.CreateNoWindow = true;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.RedirectStandardError = true;
            proc.StartInfo.RedirectStandardInput = true;
            proc.StartInfo.WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            proc.StartInfo.Environment["CI"] = "true";
            AddNodeToPath(proc.StartInfo);

            proc.Start();
            proc.StandardInput.Close();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(20_000);
            await proc.WaitForExitAsync(cts.Token);
            return proc.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task RunLoginAsync(IProgress<string>? progress = null, CancellationToken ct = default)
    {
        if (!IsNodeJsInstalled || !IsCliInstalled)
            throw new InvalidOperationException("Node.js または Claude Code CLI がインストールされていません。");

        progress?.Report("ブラウザで認証を開始します...");

        // Claude Code CLI は未ログイン状態で対話モード起動すると自動的にブラウザ認証が開始される。
        // "login" はサブコマンドではないため引数なしで起動する。
        // UseShellExecute=true で新しいコンソールウィンドウを開く。
        var nodeBinDir = Path.GetDirectoryName(AppPaths.NodeExePath)!;
        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"set \"PATH={nodeBinDir};%PATH%\" && \"{AppPaths.NodeExePath}\" \"{AppPaths.CliJsPath}\" || pause\"",
            UseShellExecute = true,
            CreateNoWindow = false,
            WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        };

        using var loginProcess = Process.Start(psi);

        // ポーリングでログイン完了を待つ
        for (var i = 0; i < LoginMaxPolls; i++)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(LoginPollIntervalMs, ct);

            progress?.Report($"認証完了を待っています... ({(i + 1) * LoginPollIntervalMs / 1000}秒経過)");

            if (await IsLoggedInAsync(ct))
            {
                progress?.Report("認証が完了しました。");
                // ログインプロセスがまだ残っていれば終了
                try { if (loginProcess is not null && !loginProcess.HasExited) loginProcess.Kill(); } catch { }
                return;
            }
        }

        // タイムアウト
        try { if (loginProcess is not null && !loginProcess.HasExited) loginProcess.Kill(); } catch { }
        throw new TimeoutException("認証がタイムアウトしました（10分）。アプリを再起動して再試行してください。");
    }

    public async Task<bool> VerifyConnectivityAsync(CancellationToken ct = default)
    {
        if (!IsNodeJsInstalled || !IsCliInstalled)
            return false;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = AppPaths.NodeExePath,
                ArgumentList = { AppPaths.CliJsPath, "-p", "test" },
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            psi.Environment["CI"] = "true";
            AddNodeToPath(psi);

            using var process = Process.Start(psi);
            if (process is null) return false;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(30_000);
            await process.WaitForExitAsync(cts.Token);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// ProcessStartInfo の PATH 環境変数にローカル Node.js の bin ディレクトリを先頭追加する。
    /// </summary>
    internal static void AddNodeToPath(ProcessStartInfo psi)
    {
        var currentPath = psi.Environment.TryGetValue("PATH", out var existing) ? existing : "";
        psi.Environment["PATH"] = $"{AppPaths.NodeJsDir};{currentPath}";
    }
}
