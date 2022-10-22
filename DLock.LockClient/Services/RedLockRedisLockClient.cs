using RedLockNet;
using RedLockNet.SERedis;
using RedLockNet.SERedis.Configuration;
using StackExchange.Redis;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DLock.LockClient.Services
{
    public class RedLockRedisLockClient : ILockClient, IDisposable
    {
        private RedLockFactory _redlockFactory;
        private bool disposedValue;
        private readonly ConcurrentDictionary<string, IRedLock> _lockMap;

        public RedLockRedisLockClient()
        {
            _lockMap = new ConcurrentDictionary<string, IRedLock>();
        }

        public string Implementation => "RedLock Redis DLM";

        public async Task<string> AcquireLockAsync(string lockId, int timeoutMs, TimeSpan waitTimeout)
        {
            IRedLock redLock = await _redlockFactory.CreateLockAsync(
                lockId, TimeSpan.FromMilliseconds(timeoutMs), waitTimeout, TimeSpan.FromSeconds(0.5));

            if (redLock.IsAcquired)
            {
                _lockMap[redLock.LockId] = redLock;
                return redLock.LockId;
            }
            else
            {
                throw new Exception("RedLock couldn't be acquired");
            }
        }

        public async Task ReleaseLockAsync(string lockId)
        {
            if (_lockMap.TryGetValue(lockId, out IRedLock redLock))
            {
                await redLock.DisposeAsync();
            }
        }

        public Task TryInitializeAsync(IEnumerable<string> redisEndpoints)
        {
            if (_redlockFactory == null)
            {
                List<RedLockMultiplexer> multiplexers = new List<RedLockMultiplexer>();

                foreach (string endpoint in redisEndpoints)
                {
                    ConfigurationOptions cfg = new ConfigurationOptions
                    {
                        AllowAdmin = true,
                    };

                    cfg.EndPoints.Add(endpoint);

                    string connStr = cfg.ToString();

                    ConnectionMultiplexer conn = ConnectionMultiplexer.Connect(connStr);

                    multiplexers.Add(conn);
                }

                _redlockFactory = RedLockFactory.Create(multiplexers);
            }

            return Task.CompletedTask;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _redlockFactory?.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
