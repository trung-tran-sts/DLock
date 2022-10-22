using DLock.SimpleDLM.Hubs;
using DLock.SimpleDLM.Models;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Concurrent;
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
        private readonly ConcurrentDictionary<string, AcquiredLockInfo> _currentResourceLockInfoMap;
        private readonly ConcurrentDictionary<string, AcquiredLockInfo> _lockInfoByIdMap;
        private readonly Dictionary<string, Queue<LockRequest>> _lockRequestQueueMap;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _resourceSemaphoreMap;

        public DistributedLockManager(IHubContext<LocksHub, ILocksHubClient> locksHubContext)
        {
            _locksHubContext = locksHubContext;
            _lockRequestQueueMap = new Dictionary<string, Queue<LockRequest>>();
            _resourceSemaphoreMap = new ConcurrentDictionary<string, SemaphoreSlim>();
            _currentResourceLockInfoMap = new ConcurrentDictionary<string, AcquiredLockInfo>();
            _lockInfoByIdMap = new ConcurrentDictionary<string, AcquiredLockInfo>();
        }

        public async Task AcquireLockAsync(LockRequest lockRequest)
        {
            SemaphoreSlim semaphore = _resourceSemaphoreMap.GetOrAdd(lockRequest.Resource, (_) => new SemaphoreSlim(1, 1));

            try
            {
                await semaphore.WaitAsync();

                if (!await TryGiveLockAsync(lockRequest))
                {
                    if (!_lockRequestQueueMap.TryGetValue(lockRequest.Resource, out Queue<LockRequest> queue))
                    {
                        queue = new Queue<LockRequest>();
                        _lockRequestQueueMap[lockRequest.Resource] = queue;
                    }

                    queue.Enqueue(lockRequest);

                    Console.WriteLine($"Lock request for {lockRequest.Resource} with lock id {lockRequest.LockId} was enqueued");
                }
            }
            finally
            {
                semaphore?.Release();
            }
        }

        public async Task ReleaseLockAsync(string lockId)
        {
            if (_lockInfoByIdMap.TryGetValue(lockId, out AcquiredLockInfo currentLock))
            {
                SemaphoreSlim semaphore = _resourceSemaphoreMap.GetOrAdd(currentLock.Resource, (_) => new SemaphoreSlim(1, 1));

                try
                {
                    await semaphore.WaitAsync();

                    if (_lockInfoByIdMap.Remove(currentLock.LockId, out _)
                        && _currentResourceLockInfoMap.Remove(currentLock.Resource, out _)
                        && _resourceSemaphoreMap.Remove(currentLock.Resource, out _))
                    {
                        if (_lockRequestQueueMap.TryGetValue(currentLock.Resource, out Queue<LockRequest> queue)
                            && queue.TryDequeue(out LockRequest nextLockRequest))
                        {
                            if (queue.Count == 0)
                            {
                                _lockRequestQueueMap.Remove(currentLock.Resource);
                            }

                            if (!await TryGiveLockAsync(nextLockRequest))
                            {
                                throw new Exception(
                                    $"Cannot give lock {nextLockRequest.Resource}" +
                                    $" to lock id {nextLockRequest.LockId}");
                            }
                        }
                    }
                }
                finally
                {
                    semaphore?.Release();
                }
            }
        }

        private async Task<bool> TryGiveLockAsync(LockRequest lockRequest)
        {
            bool acquired;

            if (_currentResourceLockInfoMap.TryGetValue(lockRequest.Resource, out AcquiredLockInfo currentLock))
            {
                if (currentLock.ValidUntil > DateTime.UtcNow)
                {
                    acquired = false;
                }
                else
                {
                    _currentResourceLockInfoMap.Remove(lockRequest.Resource, out _);
                    _lockInfoByIdMap.Remove(lockRequest.LockId, out _);
                    acquired = true;
                }
            }
            else
            {
                acquired = true;
            }

            if (acquired)
            {
                AcquiredLockInfo lockInfo = new AcquiredLockInfo(lockRequest.Resource, lockRequest.LockId, lockRequest.TimeoutMs);
                _currentResourceLockInfoMap[lockRequest.Resource] = lockInfo;
                _lockInfoByIdMap[lockRequest.LockId] = lockInfo;

                Console.WriteLine($"Acquired lock {lockRequest.Resource} with lock id {lockRequest.LockId}");

                await _locksHubContext.Clients.Group(lockRequest.LockId).NotifyLockAcquired(lockRequest.Resource);

                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
