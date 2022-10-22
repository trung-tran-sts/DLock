using DLock.SimpleDLM.Hubs;
using DLock.SimpleDLM.Models;
using DLock.SimpleDLM.Shared;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        private readonly ConcurrentDictionary<string, CustomSemaphoreSlim> _resourceSemaphoreMap;

        public DistributedLockManager(IHubContext<LocksHub, ILocksHubClient> locksHubContext)
        {
            _locksHubContext = locksHubContext;
            _lockRequestQueueMap = new Dictionary<string, Queue<LockRequest>>();
            _resourceSemaphoreMap = new ConcurrentDictionary<string, CustomSemaphoreSlim>();
            _currentResourceLockInfoMap = new ConcurrentDictionary<string, AcquiredLockInfo>();
            _lockInfoByIdMap = new ConcurrentDictionary<string, AcquiredLockInfo>();
        }

        public async Task AcquireLockAsync(LockRequest lockRequest)
        {
            CustomSemaphoreSlim semaphore = null;

            try
            {
                do
                {
                    // When we successfully release the lock, we invalidate and remove the semaphore
                    // from the dictionary to save memory usage
                    semaphore = _resourceSemaphoreMap.GetOrAdd(lockRequest.Resource, (_) => new CustomSemaphoreSlim(1, 1));
                    await semaphore.WaitAsync();
                } while (!semaphore.IsValid);

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
                CustomSemaphoreSlim semaphore = _resourceSemaphoreMap.GetOrAdd(currentLock.Resource, (_) => new CustomSemaphoreSlim(1, 1));

                try
                {
                    await semaphore.WaitAsync();

                    if (_lockInfoByIdMap.Remove(currentLock.LockId, out _)
                        && _currentResourceLockInfoMap.Remove(currentLock.Resource, out _)
                        && _resourceSemaphoreMap.Remove(currentLock.Resource, out _))
                    {
                        if (_lockRequestQueueMap.TryGetValue(currentLock.Resource, out Queue<LockRequest> queue))
                        {
                            bool handled;

                            do
                            {
                                if (queue.TryDequeue(out LockRequest nextLockRequest))
                                {
                                    if (queue.Count == 0)
                                    {
                                        handled = true;

                                        _lockRequestQueueMap.Remove(currentLock.Resource);
                                    }

                                    if (nextLockRequest.WaitUntil > DateTime.UtcNow)
                                    {
                                        handled = true;

                                        if (!await TryGiveLockAsync(nextLockRequest))
                                        {
                                            throw new Exception(
                                                $"Cannot give lock {nextLockRequest.Resource}" +
                                                $" to lock id {nextLockRequest.LockId}");
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine($"Skipped lock request {nextLockRequest.LockId} on resource {nextLockRequest.Resource}");

                                        handled = false;
                                    }
                                }
                                else
                                {
                                    handled = true;
                                }
                            } while (!handled);

                        }
                    }
                }
                finally
                {
                    semaphore?.Invalidate();
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
                    _currentResourceLockInfoMap.Remove(currentLock.Resource, out _);
                    _lockInfoByIdMap.Remove(currentLock.LockId, out _);
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
