using System;
using System.Threading.Tasks;

namespace DLock.LockClient.Services
{
    public interface ILockClient
    {
        string Implementation { get; }
        Task AcquireLockAsync(string lockId, int timeoutMs, TimeSpan waitTimeout);
        Task ReleaseLockAsync(string lockId);
    }
}
