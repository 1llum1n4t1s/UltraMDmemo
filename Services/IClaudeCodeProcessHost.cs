using System.Threading;
using System.Threading.Tasks;

namespace UltraMDmemo.Services;

public interface IClaudeCodeProcessHost
{
    Task<string> ExecuteAsync(string prompt, string stdinText, CancellationToken ct = default);
    Task<bool> IsAvailableAsync(CancellationToken ct = default);
}
