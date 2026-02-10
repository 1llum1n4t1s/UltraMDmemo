using System.Threading;
using System.Threading.Tasks;
using UltraMDmemo.Models;

namespace UltraMDmemo.Services;

public interface ITransformService
{
    Task<TransformResult> TransformAsync(TransformRequest request, CancellationToken ct = default);
}
