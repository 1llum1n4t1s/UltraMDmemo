using System;
using System.IO;

namespace UltraMDmemo.Services;

/// <summary>
/// アプリケーションのパス解決を担当する。
/// Velopack 環境 (%LOCALAPPDATA%\UltraMDmemo\current\) と開発環境を自動判定する。
/// </summary>
public static class AppPaths
{
    private static readonly Lazy<string> _baseDir = new(ResolveBaseDir);

    /// <summary>
    /// アプリケーションデータのルートディレクトリ。
    /// Velopack 環境: %LOCALAPPDATA%\UltraMDmemo\
    /// 開発環境: %LOCALAPPDATA%\UltraMDmemo\
    /// </summary>
    public static string BaseDir => _baseDir.Value;

    /// <summary>%LOCALAPPDATA%\UltraMDmemo\lib\</summary>
    public static string LibDir => Path.Combine(BaseDir, "lib");

    /// <summary>%LOCALAPPDATA%\UltraMDmemo\lib\nodejs\</summary>
    public static string NodeJsDir => Path.Combine(LibDir, "nodejs");

    /// <summary>%LOCALAPPDATA%\UltraMDmemo\lib\nodejs\node.exe</summary>
    public static string NodeExePath => Path.Combine(NodeJsDir, "node.exe");

    /// <summary>%LOCALAPPDATA%\UltraMDmemo\lib\npm\</summary>
    public static string NpmDir => Path.Combine(LibDir, "npm");

    /// <summary>%LOCALAPPDATA%\UltraMDmemo\lib\npm-cache\</summary>
    public static string NpmCacheDir => Path.Combine(LibDir, "npm-cache");

    /// <summary>%LOCALAPPDATA%\UltraMDmemo\lib\npm\node_modules\@anthropic-ai\claude-code\cli.js</summary>
    public static string CliJsPath => Path.Combine(NpmDir, "node_modules", "@anthropic-ai", "claude-code", "cli.js");

    /// <summary>%LOCALAPPDATA%\UltraMDmemo\history\</summary>
    public static string HistoryDir => Path.Combine(BaseDir, "history");

    /// <summary>%LOCALAPPDATA%\UltraMDmemo\settings.json</summary>
    public static string SettingsPath => Path.Combine(BaseDir, "settings.json");

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
        // 常に %LOCALAPPDATA%\UltraMDmemo\ を使用
        // Velopack 環境では current\ の親ディレクトリ
        // 開発環境でも同じパスを使用（一貫性のため）
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
