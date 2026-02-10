using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UltraMDmemo.Models;

namespace UltraMDmemo.Services;

public interface IHistoryService
{
    HistoryPaths GetPaths(string id);
    Task SaveAsync(string id, string inputText, string outputMarkdown, TransformMeta meta, CancellationToken ct = default);
    Task<List<TransformMeta>> LoadIndexAsync(CancellationToken ct = default);
    Task<(string input, string output, TransformMeta meta)> LoadAsync(string id, CancellationToken ct = default);
    Task DeleteAsync(string id, CancellationToken ct = default);
}
