using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace DLock.LockClient.Services
{
    public class SimpleDLMLockClient : ILockClient
    {
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _lockWaitMap;
        private readonly string _locksHubUrl;
        public string Implementation => "Simple DLM";

        public SimpleDLMLockClient(IConfiguration configuration)
        {
            _locksHubUrl = configuration["SimpleDLMLocksHubUrl"];

            _lockWaitMap = new ConcurrentDictionary<string, SemaphoreSlim>();
        }

        public async Task<string> AcquireLockAsync(string resource, int timeoutMs, TimeSpan waitTimeout)
        {
            SemaphoreSlim lockWait = _lockWaitMap.GetOrAdd(resource, (_) => new SemaphoreSlim(0, 1));

            HubConnection hubConnection = GetHubConnection();

            hubConnection.On("NotifyLockAcquired", () =>
            {
                ReleaseLocalLock(resource);
            });

            try
            {
                await hubConnection.StartAsync();

                string lockId = await hubConnection.InvokeAsync<string>("AcquireLockAsync", resource, timeoutMs, waitTimeout.TotalMilliseconds);

                bool acquired = lockWait.Wait(waitTimeout);

                if (!acquired)
                {
                    throw new Exception("Timeout waiting for the lock");
                }

                return lockId;
            }
            finally
            {
                if (hubConnection != null) await hubConnection?.DisposeAsync();
            }
        }

        public async Task ReleaseLockAsync(string lockId)
        {
            HubConnection hubConnection = GetHubConnection();
            try
            {
                await hubConnection.StartAsync();
                await hubConnection.InvokeAsync("ReleaseLockAsync", lockId);
            }
            finally
            {
                if (hubConnection != null) await hubConnection?.DisposeAsync();
            }
        }

        private void ReleaseLocalLock(string resource)
        {
            if (_lockWaitMap.TryRemove(resource, out SemaphoreSlim lockWait))
            {
                lockWait.Release();
            }
        }

        private HubConnection GetHubConnection()
        {
            HubConnection hubConnection = new HubConnectionBuilder()
                .WithUrl(_locksHubUrl)
                .WithAutomaticReconnect()
                .Build();

            return hubConnection;
        }

    }
}
