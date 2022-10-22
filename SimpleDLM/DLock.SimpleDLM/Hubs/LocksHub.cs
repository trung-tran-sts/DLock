using DLock.SimpleDLM.Models;
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
            string lockId = Guid.NewGuid().ToString();

            Console.WriteLine($"{lockId} is acquiring lock {resource}");

            await Groups.AddToGroupAsync(Context.ConnectionId, lockId);

            await _distributedLockManager.AcquireLockAsync(new LockRequest
            {
                LockId = lockId,
                Resource = resource,
                TimeoutMs = timeoutMs,
                WaitUntil = DateTime.UtcNow.AddMilliseconds(waitTimeoutMs)
            });

            return lockId;
        }

        public async Task ReleaseLockAsync(string lockId)
        {
            Console.WriteLine($"Releasing lock {lockId}");

            await _distributedLockManager.ReleaseLockAsync(lockId);
        }
    }
}
