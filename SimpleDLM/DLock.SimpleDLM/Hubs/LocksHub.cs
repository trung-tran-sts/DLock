using DLock.SimpleDLM.Models;
using DLock.SimpleDLM.Services;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Threading.Tasks;

namespace DLock.SimpleDLM.Hubs
{
    public interface ILocksHubClient
    {
        Task NotifyLockAcquired(string lockId);
    }

    public class LocksHub : Hub<ILocksHubClient>
    {
        private readonly IDistributedLockManager _distributedLockManager;

        public LocksHub(IDistributedLockManager distributedLockManager)
        {
            _distributedLockManager = distributedLockManager;
        }

        public async Task AcquireLockAsync(string lockId, int timeoutMs)
        {
            Console.WriteLine($"{Context.ConnectionId} is acquiring lock {lockId}");

            await _distributedLockManager.AcquireLockAsync(new LockRequest
            {
                ConnectionId = Context.ConnectionId,
                LockId = lockId,
                TimeoutMs = timeoutMs,
            });
        }

        public async Task ReleaseLockAsync(string lockId)
        {
            Console.WriteLine($"{Context.ConnectionId} is releasing lock {lockId}");

            await _distributedLockManager.ReleaseLockAsync(lockId);
        }
    }
}
