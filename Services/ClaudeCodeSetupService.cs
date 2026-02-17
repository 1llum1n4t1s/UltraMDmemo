using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
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
    private const int LoginPollIntervalMs = 10_000;
    private const int LoginMaxPolls = 60; // 10分

    private static readonly HttpClient HttpClient = new();

    /// <summary>OS とアーキテクチャに応じた Node.js ダウンロード URL を返す。</summary>
    private static string GetNodeDownloadUrl()
    {
        if (OperatingSystem.IsMacOS())
        {
            var arch = RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "arm64" : "x64";
            return $"https://nodejs.org/dist/{NodeVersion}/node-{NodeVersion}-darwin-{arch}.tar.gz";
        }

        return $"https://nodejs.org/dist/{NodeVersion}/node-{NodeVersion}-win-x64.zip";
    }

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

        var downloadUrl = GetNodeDownloadUrl();
        var tempArchive = Path.Combine(Path.GetTempPath(),
            OperatingSystem.IsMacOS()
                ? $"node-{NodeVersion}-darwin.tar.gz"
                : $"node-{NodeVersion}-win-x64.zip");
        try
        {
            using var response = await HttpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            await using var fileStream = File.Create(tempArchive);
            await response.Content.CopyToAsync(fileStream, ct);
            fileStream.Close();

            progress?.Report("Node.js を展開中...");

            var tempExtract = Path.Combine(Path.GetTempPath(), $"node-extract-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempExtract);

            if (OperatingSystem.IsMacOS())
            {
                // macOS: tar.gz を展開
                var tarPsi = new ProcessStartInfo
                {
                    FileName = "tar",
                    ArgumentList = { "xzf", tempArchive, "-C", tempExtract },
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                };
                using var tarProc = Process.Start(tarPsi)!;
                var tarStderr = await tarProc.StandardError.ReadToEndAsync(ct);
                await tarProc.WaitForExitAsync(ct);
                if (tarProc.ExitCode != 0)
                    throw new InvalidOperationException($"Node.js tar.gz の展開に失敗しました: {tarStderr}");
            }
            else
            {
                // Windows: ZIP を展開
                ZipFile.ExtractToDirectory(tempArchive, tempExtract, overwriteFiles: true);
            }

            // 展開先のサブフォルダを見つける (node-vXX.XX.X-<platform>-<arch>/)
            var dirs = Directory.GetDirectories(tempExtract);
            if (dirs.Length == 0)
                throw new InvalidOperationException("Node.js アーカイブの展開に失敗しました: サブディレクトリが見つかりません。");
            var extractedDir = dirs[0];

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
            if (File.Exists(tempArchive))
                File.Delete(tempArchive);
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
        // Windows: nodejs/node_modules/npm/bin/npm-cli.js
        // macOS:   nodejs/lib/node_modules/npm/bin/npm-cli.js
        var npmCliJs = OperatingSystem.IsMacOS()
            ? Path.Combine(AppPaths.NodeJsDir, "lib", "node_modules", "npm", "bin", "npm-cli.js")
            : Path.Combine(AppPaths.NodeJsDir, "node_modules", "npm", "bin", "npm-cli.js");
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

    /// <summary>
    /// ~/.claude/.credentials.json の認証トークン直接チェック（高速）。
    /// ファイルが存在し accessToken が含まれていれば true を返す。
    /// </summary>
    private static readonly string CredentialsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".claude", ".credentials.json");

    private static bool CheckCredentialsFile()
    {
        try
        {
            if (!File.Exists(CredentialsPath))
                return false;

            var json = File.ReadAllText(CredentialsPath);
            using var doc = JsonDocument.Parse(json);

            // claudeAiOauth.accessToken が存在し空でなければログイン済み
            if (doc.RootElement.TryGetProperty("claudeAiOauth", out var oauth)
                && oauth.TryGetProperty("accessToken", out var token)
                && token.GetString() is { Length: > 0 })
            {
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            Logger.LogException("credentials.json の読み取りに失敗", ex);
            return false;
        }
    }

    public async Task<bool> IsLoggedInAsync(CancellationToken ct = default)
    {
        if (!IsNodeJsInstalled || !IsCliInstalled)
            return false;

        // 高速パス: 認証トークンファイルを直接チェック
        var fileCheck = CheckCredentialsFile();
        Logger.Log($"[LoginCheck] credentials.json チェック: {fileCheck}", LogLevel.Debug);
        if (fileCheck)
            return true;

        // フォールバック: CLI プロセスで確認（ファイルがない or 構造が異なる場合）
        Logger.Log("[LoginCheck] credentials.json なし/不正 → CLI フォールバック開始", LogLevel.Debug);
        return await IsLoggedInViaCli(ct);
    }

    /// <summary>CLI プロセスを起動してログイン状態を確認する（従来方式）。</summary>
    private async Task<bool> IsLoggedInViaCli(CancellationToken ct)
    {
        try
        {
            using var proc = new Process();
            proc.StartInfo.FileName = AppPaths.NodeExePath;
            proc.StartInfo.ArgumentList.Add(AppPaths.CliJsPath);
            proc.StartInfo.ArgumentList.Add("config");
            proc.StartInfo.ArgumentList.Add("get");
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

            // stdout/stderr を読み取らないとバッファ満杯でデッドロックする
            var stdoutTask = proc.StandardOutput.ReadToEndAsync(cts.Token);
            var stderrTask = proc.StandardError.ReadToEndAsync(cts.Token);
            await Task.WhenAll(stdoutTask, stderrTask);
            await proc.WaitForExitAsync(cts.Token);
            return proc.ExitCode == 0;
        }
        catch (Exception ex)
        {
            Logger.LogException("CLI ログインチェックに失敗", ex);
            return false;
        }
    }

    public async Task RunLoginAsync(IProgress<string>? progress = null, CancellationToken ct = default)
    {
        if (!IsNodeJsInstalled || !IsCliInstalled)
            throw new InvalidOperationException("Node.js または Claude Code CLI がインストールされていません。");

        progress?.Report("ブラウザで認証を開始します...");

        // "claude auth login" サブコマンドでブラウザ認証を直接起動する。
        // 対話モード（引数なし）だと CLI コンソールが表示されてしまうため auth login を使用する。
        var psi = new ProcessStartInfo
        {
            FileName = AppPaths.NodeExePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        };
        psi.ArgumentList.Add(AppPaths.CliJsPath);
        psi.ArgumentList.Add("auth");
        psi.ArgumentList.Add("login");
        AddNodeToPath(psi);
        // ネストセッション検出を回避する
        psi.Environment.Remove("CLAUDECODE");

        using var loginProcess = Process.Start(psi);
        // stdout/stderr を非同期で読み捨てる（バッファ詰まり防止）
        if (loginProcess is not null)
        {
            _ = loginProcess.StandardOutput.ReadToEndAsync(ct);
            _ = loginProcess.StandardError.ReadToEndAsync(ct);
        }

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

            // stdout/stderr を読み取らないとバッファ満杯でデッドロックする
            var stdoutTask = process.StandardOutput.ReadToEndAsync(cts.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(cts.Token);
            await Task.WhenAll(stdoutTask, stderrTask);
            await process.WaitForExitAsync(cts.Token);
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            Logger.LogException("接続検証に失敗", ex);
            return false;
        }
    }

    /// <summary>
    /// ProcessStartInfo の PATH 環境変数にローカル Node.js の bin ディレクトリを先頭追加する。
    /// Windows: NodeJsDir をそのまま追加（node.exe が直下にある）。
    /// macOS: NodeJsDir/bin を追加（node が bin/ 配下にある）。
    /// </summary>
    internal static void AddNodeToPath(ProcessStartInfo psi)
    {
        var currentPath = psi.Environment.TryGetValue("PATH", out var existing) ? existing : "";
        var separator = OperatingSystem.IsWindows() ? ";" : ":";
        var nodeDir = OperatingSystem.IsMacOS()
            ? Path.Combine(AppPaths.NodeJsDir, "bin")
            : AppPaths.NodeJsDir;
        psi.Environment["PATH"] = $"{nodeDir}{separator}{currentPath}";
    }
}
