using System.Threading;
using System.Threading.Tasks;

namespace Fub.Interfaces.Game;

public interface IGameLoop
{
    Task RunAsync(CancellationToken cancellationToken = default);
    bool IsRunning { get; }
}

