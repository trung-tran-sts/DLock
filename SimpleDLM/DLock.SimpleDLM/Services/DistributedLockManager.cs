using DLock.SimpleDLM.Hubs;
using DLock.SimpleDLM.Models;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DLock.SimpleDLM.Services
{
    public interface IDistributedLockManager
    {
        Task AcquireLockAsync(LockRequest lockRequest);
        Task ReleaseLockAsync(string lockId);
    }

    public class DistributedLockManager : IDistributedLockManager
    {
        private readonly IHubContext<LocksHub, ILocksHubClient> _locksHubContext;
        private readonly HashSet<AcquiredLockInfo> _acquiredLockSet;
        private readonly SemaphoreSlim _semaphore;
        private readonly Dictionary<string, Queue<LockRequest>> _lockRequestQueueMap;

        public DistributedLockManager(IHubContext<LocksHub, ILocksHubClient> locksHubContext)
        {
            _locksHubContext = locksHubContext;
            _acquiredLockSet = new HashSet<AcquiredLockInfo>();
            _semaphore = new SemaphoreSlim(1, 1);
            _lockRequestQueueMap = new Dictionary<string, Queue<LockRequest>>();
        }

        public async Task AcquireLockAsync(LockRequest lockRequest)
        {
            try
            {
                await _semaphore.WaitAsync();

                if (!await TryGiveLockAsync(lockRequest))
                {
                    if (!_lockRequestQueueMap.TryGetValue(lockRequest.LockId, out Queue<LockRequest> queue))
                    {
                        queue = new Queue<LockRequest>();
                        _lockRequestQueueMap[lockRequest.LockId] = queue;
                    }

                    queue.Enqueue(lockRequest);

                    Console.WriteLine($"Lock request for {lockRequest.LockId} of {lockRequest.ConnectionId} was enqueued");
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task ReleaseLockAsync(string lockId)
        {
            try
            {
                await _semaphore.WaitAsync();

                _acquiredLockSet.Remove(new AcquiredLockInfo(lockId));

                if (_lockRequestQueueMap.TryGetValue(lockId, out Queue<LockRequest> queue)
                    && queue.TryDequeue(out LockRequest nextLockRequest))
                {
                    if (!await TryGiveLockAsync(nextLockRequest))
                    {
                        throw new Exception(
                            $"Cannot give lock {nextLockRequest.LockId}" +
                            $" to connection {nextLockRequest.ConnectionId}");
                    }
                }

            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async Task<bool> TryGiveLockAsync(LockRequest lockRequest)
        {
            bool acquired;
            AcquiredLockInfo lockId = new AcquiredLockInfo(lockRequest.LockId);

            if (_acquiredLockSet.TryGetValue(lockId, out AcquiredLockInfo currentLock))
            {
                if (currentLock.ValidUntil > DateTime.UtcNow)
                {
                    acquired = false;
                }
                else
                {
                    _acquiredLockSet.Remove(currentLock);
                    acquired = true;
                }
            }
            else
            {
                acquired = true;
            }

            if (acquired)
            {
                _acquiredLockSet.Add(new AcquiredLockInfo(lockRequest.LockId, lockRequest.TimeoutMs));

                Console.WriteLine($"{lockRequest.ConnectionId} acquired lock {lockRequest.LockId}");

                await _locksHubContext.Clients.Client(lockRequest.ConnectionId).NotifyLockAcquired(lockRequest.LockId);

                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
