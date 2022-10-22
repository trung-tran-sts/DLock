using System;
using System.Threading.Tasks;

namespace DLock.LockClient.Services
{
    public interface ILockClient
    {
        string Implementation { get; }
        Task<string> AcquireLockAsync(string lockId, int timeoutMs, TimeSpan waitTimeout);
        Task ReleaseLockAsync(string lockId);
    }
}
