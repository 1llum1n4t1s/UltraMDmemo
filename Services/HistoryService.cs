using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using UltraMDmemo.Models;

namespace UltraMDmemo.Services;

public sealed class HistoryService : IHistoryService
{
    private readonly string _historyDir;

    public HistoryService()
    {
        _historyDir = AppPaths.HistoryDir;
        Directory.CreateDirectory(_historyDir);
    }

    public HistoryPaths GetPaths(string id) => new()
    {
        Input = Path.Combine(_historyDir, $"{id}.input.txt"),
        Output = Path.Combine(_historyDir, $"{id}.output.md"),
        Meta = Path.Combine(_historyDir, $"{id}.meta.json"),
    };

    public async Task SaveAsync(string id, string inputText, string outputMarkdown, TransformMeta meta, CancellationToken ct = default)
    {
        var paths = GetPaths(id);
        await File.WriteAllTextAsync(paths.Input, inputText, ct);
        await File.WriteAllTextAsync(paths.Output, outputMarkdown, ct);
        var json = JsonSerializer.Serialize(meta, AppJsonContext.Default.TransformMeta);
        await File.WriteAllTextAsync(paths.Meta, json, ct);
    }

    public async Task<List<TransformMeta>> LoadIndexAsync(CancellationToken ct = default)
    {
        if (!Directory.Exists(_historyDir))
            return [];

        var metaFiles = Directory.GetFiles(_historyDir, "*.meta.json");
        var items = new List<TransformMeta>();

        foreach (var file in metaFiles)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file, ct);
                var meta = JsonSerializer.Deserialize(json, AppJsonContext.Default.TransformMeta);
                if (meta is not null) items.Add(meta);
            }
            catch
            {
                // skip corrupted meta files
            }
        }

        return items.OrderByDescending(m => m.CreatedAt).ToList();
    }

    public async Task<(string input, string output, TransformMeta meta)> LoadAsync(string id, CancellationToken ct = default)
    {
        var paths = GetPaths(id);
        var input = await File.ReadAllTextAsync(paths.Input, ct);
        var output = await File.ReadAllTextAsync(paths.Output, ct);
        var json = await File.ReadAllTextAsync(paths.Meta, ct);
        var meta = JsonSerializer.Deserialize(json, AppJsonContext.Default.TransformMeta)
            ?? throw new InvalidOperationException($"メタデータの読み込みに失敗: {id}");
        return (input, output, meta);
    }

    public Task DeleteAsync(string id, CancellationToken ct = default)
    {
        var paths = GetPaths(id);
        if (File.Exists(paths.Input)) File.Delete(paths.Input);
        if (File.Exists(paths.Output)) File.Delete(paths.Output);
        if (File.Exists(paths.Meta)) File.Delete(paths.Meta);
        return Task.CompletedTask;
    }
}
