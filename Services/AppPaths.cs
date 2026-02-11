using System;
using System.IO;

namespace UltraMDmemo.Services;

/// <summary>
/// アプリケーションのパス解決を担当する。
/// Windows: Velopack 環境 (%LOCALAPPDATA%\UltraMDmemo\current\) と開発環境を自動判定する。
/// macOS: ~/Library/Application Support/UltraMDmemo/ を使用する。
/// </summary>
public static class AppPaths
{
    private static readonly Lazy<string> _baseDir = new(ResolveBaseDir);

    /// <summary>
    /// アプリケーションデータのルートディレクトリ。
    /// Windows: %LOCALAPPDATA%\UltraMDmemo\
    /// macOS: ~/Library/Application Support/UltraMDmemo/
    /// </summary>
    public static string BaseDir => _baseDir.Value;

    /// <summary>Windows: %LOCALAPPDATA%\UltraMDmemo\lib\ / macOS: ~/Library/Application Support/UltraMDmemo/lib/</summary>
    public static string LibDir => Path.Combine(BaseDir, "lib");

    /// <summary>Windows: %LOCALAPPDATA%\UltraMDmemo\lib\nodejs\ / macOS: .../lib/nodejs/</summary>
    public static string NodeJsDir => Path.Combine(LibDir, "nodejs");

    /// <summary>
    /// Windows: lib\nodejs\node.exe
    /// macOS: lib/nodejs/bin/node
    /// </summary>
    public static string NodeExePath =>
        OperatingSystem.IsMacOS()
            ? Path.Combine(NodeJsDir, "bin", "node")
            : Path.Combine(NodeJsDir, "node.exe");

    /// <summary>Windows: %LOCALAPPDATA%\UltraMDmemo\lib\npm\ / macOS: .../lib/npm/</summary>
    public static string NpmDir => Path.Combine(LibDir, "npm");

    /// <summary>Windows: %LOCALAPPDATA%\UltraMDmemo\lib\npm-cache\ / macOS: .../lib/npm-cache/</summary>
    public static string NpmCacheDir => Path.Combine(LibDir, "npm-cache");

    /// <summary>Windows: ..\lib\npm\node_modules\@anthropic-ai\claude-code\cli.js</summary>
    public static string CliJsPath => Path.Combine(NpmDir, "node_modules", "@anthropic-ai", "claude-code", "cli.js");

    /// <summary>Windows: %LOCALAPPDATA%\UltraMDmemo\history\ / macOS: .../UltraMDmemo/history/</summary>
    public static string HistoryDir => Path.Combine(BaseDir, "history");

    /// <summary>Windows: %LOCALAPPDATA%\UltraMDmemo\settings.json / macOS: .../UltraMDmemo/settings.json</summary>
    public static string SettingsPath => Path.Combine(BaseDir, "settings.json");

    /// <summary>Windows: %LOCALAPPDATA%\UltraMDmemo\logs\ / macOS: .../UltraMDmemo/logs/</summary>
    public static string LogDirectory => Path.Combine(BaseDir, "logs");

    /// <summary>
    /// Velopack 環境かどうかを判定する。
    /// AppDomain.CurrentDomain.BaseDirectory が \current\ で終わる場合は Velopack 環境。
    /// </summary>
    public static bool IsVelopackEnvironment
    {
        get
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            return baseDir.EndsWith("current", StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string ResolveBaseDir()
    {
        // Windows: %LOCALAPPDATA%\UltraMDmemo\
        // macOS:   ~/Library/Application Support/UltraMDmemo/
        if (OperatingSystem.IsMacOS())
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, "Library", "Application Support", "UltraMDmemo");
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "UltraMDmemo");
    }

    /// <summary>
    /// 必要なディレクトリをすべて作成する。
    /// </summary>
    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(LibDir);
        Directory.CreateDirectory(NodeJsDir);
        Directory.CreateDirectory(NpmDir);
        Directory.CreateDirectory(NpmCacheDir);
        Directory.CreateDirectory(HistoryDir);
    }
}
