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
        private ConnectionMultiplexer _connection;
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
            if (_connection == null)
            {
                ConfigurationOptions cfg = new ConfigurationOptions
                {
                    AllowAdmin = true,
                };

                foreach (string endpoint in redisEndpoints)
                {
                    cfg.EndPoints.Add(endpoint);
                }

                string connStr = cfg.ToString();

                _connection = ConnectionMultiplexer.Connect(connStr);

                List<RedLockMultiplexer> multiplexers = new List<RedLockMultiplexer>
                {
                    _connection
                };

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
                    _connection?.Dispose();
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
