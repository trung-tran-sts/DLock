using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace DLock.LockClient.Services
{
    public class SimpleDLMLockClient : ILockClient
    {
        private HubConnection _hubConnection;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _lockWaitMap;

        public string Implementation => "Simple DLM";

        public SimpleDLMLockClient()
        {
            _lockWaitMap = new ConcurrentDictionary<string, SemaphoreSlim>();
        }

        public Task TryInitializeAsync(string lockHubUrl)
        {
            if (_hubConnection == null)
            {
                _hubConnection = new HubConnectionBuilder()
                    .WithUrl(lockHubUrl)
                    .WithAutomaticReconnect()
                    .Build();

                _hubConnection.On<string>("NotifyLockAcquired", (resource) =>
                {
                    ReleaseLocalLock(resource);
                });
            }

            return Task.CompletedTask;
        }

        public async Task<string> AcquireLockAsync(string resource, int timeoutMs, TimeSpan waitTimeout)
        {
            SemaphoreSlim lockWait = _lockWaitMap.GetOrAdd(resource, (_) => new SemaphoreSlim(0, 1));

            await _hubConnection.StopAsync();
            await _hubConnection.StartAsync();
            string lockId = await _hubConnection.InvokeAsync<string>("AcquireLockAsync", resource, timeoutMs);

            bool acquired = lockWait.Wait(waitTimeout);

            if (!acquired)
            {
                throw new Exception("Timeout waiting for the lock");
            }

            return lockId;
        }

        public async Task ReleaseLockAsync(string lockId)
        {
            await _hubConnection.InvokeAsync("ReleaseLockAsync", lockId);
            await _hubConnection.StopAsync();
        }

        private void ReleaseLocalLock(string resource)
        {
            if (_lockWaitMap.TryRemove(resource, out SemaphoreSlim lockWait))
            {
                lockWait.Release();
            }
        }
    }
}
