using System;
using System.Threading.Tasks;

namespace SimpleList.Core.Contracts;

public interface IUiDispatcher
{
    bool HasThreadAccess { get; }
    void Enqueue(Action action);
    Task EnqueueAsync(Func<Task> action);
}
