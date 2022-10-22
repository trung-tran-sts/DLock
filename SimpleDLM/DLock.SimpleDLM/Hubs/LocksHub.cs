using DLock.SimpleDLM.Services;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Threading.Tasks;

namespace DLock.SimpleDLM.Hubs
{
    public interface ILocksHubClient
    {
        Task NotifyLockAcquired(string resource);
    }

    public class LocksHub : Hub<ILocksHubClient>
    {
        private readonly IDistributedLockManager _distributedLockManager;

        public LocksHub(IDistributedLockManager distributedLockManager)
        {
            _distributedLockManager = distributedLockManager;
        }

        public async Task<string> AcquireLockAsync(string resource, int timeoutMs, int waitTimeoutMs)
        {
            string lockId = await _distributedLockManager.AcquireLockAsync(resource, timeoutMs, waitTimeoutMs, Context.ConnectionId);

            return lockId;
        }

        public async Task ReleaseLockAsync(string lockId)
        {
            Console.WriteLine($"Releasing lock {lockId}");

            await _distributedLockManager.ReleaseLockAsync(lockId);
        }
    }
}
