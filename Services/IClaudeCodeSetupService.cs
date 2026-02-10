using System;
using System.Threading;
using System.Threading.Tasks;

namespace UltraMDmemo.Services;

/// <summary>
/// Node.js と Claude Code CLI のローカルインストール・認証を管理する。
/// </summary>
public interface IClaudeCodeSetupService
{
    /// <summary>Node.js がインストール済みかどうか。</summary>
    bool IsNodeJsInstalled { get; }

    /// <summary>Claude Code CLI がインストール済みかどうか。</summary>
    bool IsCliInstalled { get; }

    /// <summary>ローカル Node.js 実行ファイルパス。</summary>
    string GetNodePath();

    /// <summary>Claude Code CLI の cli.js パス。</summary>
    string GetCliJsPath();

    /// <summary>Node.js を %LOCALAPPDATA%\UltraMDmemo\lib\nodejs にダウンロード・展開する。</summary>
    Task EnsureNodeJsAsync(IProgress<string>? progress = null, CancellationToken ct = default);

    /// <summary>Claude Code CLI を npm 経由でインストールする。</summary>
    Task EnsureCliAsync(IProgress<string>? progress = null, CancellationToken ct = default);

    /// <summary>ログイン済みかどうかを確認する（claude config get の終了コードで判定）。</summary>
    Task<bool> IsLoggedInAsync(CancellationToken ct = default);

    /// <summary>ブラウザ認証を起動し、ログイン完了までポーリングで待つ。</summary>
    Task RunLoginAsync(IProgress<string>? progress = null, CancellationToken ct = default);

    /// <summary>接続検証（claude -p "test" で疎通確認）。</summary>
    Task<bool> VerifyConnectivityAsync(CancellationToken ct = default);
}
