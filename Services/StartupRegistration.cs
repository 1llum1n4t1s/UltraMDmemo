using System;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace UltraMDmemo.Services;

/// <summary>
/// Windows スタートアップへのアプリケーション登録を管理するクラス。
/// ログイン時にサイレント更新チェックを実行するために使用。
/// </summary>
[SupportedOSPlatform("windows")]
public static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string EntryName = "UltraMDmemo";

    /// <summary>
    /// スタートアップにアプリケーションを登録する。
    /// インストール時・更新時に呼び出される。
    /// </summary>
    public static void Register()
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
            {
                Console.Error.WriteLine("スタートアップ登録: 実行ファイルパスを取得できませんでした。");
                return;
            }

            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key == null)
            {
                Console.Error.WriteLine("スタートアップ登録: レジストリキーを開けませんでした。");
                return;
            }

            var value = $"\"{exePath}\" --update-check";
            key.SetValue(EntryName, value);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"スタートアップ登録に失敗しました: {ex.Message}");
        }
    }

    /// <summary>
    /// スタートアップからアプリケーションの登録を解除する。
    /// アンインストール時に呼び出される。
    /// </summary>
    public static void Unregister()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key == null) return;

            if (key.GetValue(EntryName) != null)
            {
                key.DeleteValue(EntryName);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"スタートアップ登録解除に失敗しました: {ex.Message}");
        }
    }
}
