using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace DLock.LockClient.Services
{
    public class SimpleDLMLockClient : ILockClient
    {
        private readonly string _lockHubUrl;
        private HubConnection _hubConnection;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _lockWaitMap;

        public string Implementation => "Simple DLM";

        public SimpleDLMLockClient(string lockHubUrl)
        {
            _lockHubUrl = lockHubUrl;
            _lockWaitMap = new ConcurrentDictionary<string, SemaphoreSlim>();
        }

        public Task TryInitializeAsync()
        {
            if (_hubConnection == null)
            {
                _hubConnection = new HubConnectionBuilder()
                    .WithUrl(_lockHubUrl)
                    .WithAutomaticReconnect()
                    .Build();

                _hubConnection.On<string>("NotifyLockAcquired", (lockId) =>
                {
                    ReleaseLocalLock(lockId);
                });
            }

            return Task.CompletedTask;
        }

        public async Task AcquireLockAsync(string lockId, int timeoutMs, TimeSpan waitTimeout)
        {
            SemaphoreSlim lockWait = _lockWaitMap.GetOrAdd(lockId, (_) => new SemaphoreSlim(0, 1));

            await _hubConnection.StopAsync();
            await _hubConnection.StartAsync();
            await _hubConnection.InvokeAsync("AcquireLockAsync", lockId, timeoutMs);

            bool acquired = lockWait.Wait(waitTimeout);

            if (!acquired)
            {
                throw new Exception("Timeout waiting for the lock");
            }
        }

        public async Task ReleaseLockAsync(string lockId)
        {
            await _hubConnection.InvokeAsync("ReleaseLockAsync", lockId);
            await _hubConnection.StopAsync();
        }

        private void ReleaseLocalLock(string lockId)
        {
            if (_lockWaitMap.TryRemove(lockId, out SemaphoreSlim lockWait))
            {
                lockWait.Release();
            }
        }
    }
}
