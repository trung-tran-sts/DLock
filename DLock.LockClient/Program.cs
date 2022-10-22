using DLock.LockClient.Services;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DLock.LockClient
{
    internal class Program
    {
        static IConfigurationRoot _configuration;

        static async Task Main(string[] args)
        {
            _configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .Build();

            int choice;

            do
            {
                Console.WriteLine("1. Simple DLM");
                Console.WriteLine("2. Single Redis DLM");
                Console.Write("Choose a lock implementation: ");
            }
            while (!int.TryParse(Console.ReadLine(), out choice));

            switch (choice)
            {
                case 1: await RunSimpleDLM(); break;
                case 2: await RunSingleRedis(); break;
                default: Console.WriteLine("Invalid choice"); break;
            }
        }

        static async Task RunSimpleDLM()
        {
            string locksHubUrl = _configuration["SimpleDLMLocksHubUrl"];

            SimpleDLMLockClient lockClient = new SimpleDLMLockClient();

            await lockClient.TryInitializeAsync(locksHubUrl);

            await ProcessWithLock(lockClient);
        }

        static async Task RunSingleRedis()
        {
            IEnumerable<string> redisEndpoints = _configuration.GetSection("RedisEndpoints").Get<IEnumerable<string>>();

            RedLockRedisLockClient lockClient = new RedLockRedisLockClient();

            await lockClient.TryInitializeAsync(redisEndpoints);

            await ProcessWithLock(lockClient);
        }

        static async Task ProcessWithLock(ILockClient lockClient)
        {
            while (true)
            {
                try
                {
                    Console.Clear();
                    Console.WriteLine($"=== Welcome to {lockClient.Implementation} ===");
                    Console.Write("Input resource: ");
                    string resource = Console.ReadLine();
                    Console.Write("Input lock timeout ms (TTL): ");
                    int timeoutMs = int.Parse(Console.ReadLine());
                    Console.Write("Input wait timeout ms: ");
                    int waitTimeoutMs = int.Parse(Console.ReadLine());
                    Console.Write("Input process time ms: ");
                    int processTimeMs = int.Parse(Console.ReadLine());

                    Console.WriteLine($"Acquiring lock at {DateTime.UtcNow}");
                    string lockId = await lockClient.AcquireLockAsync(resource, timeoutMs, TimeSpan.FromMilliseconds(waitTimeoutMs));
                    Console.WriteLine($"Acquired lock at {DateTime.UtcNow}");

                    DateTime processEndAt = DateTime.UtcNow.AddMilliseconds(processTimeMs);
                    int time = 1;

                    while (DateTime.UtcNow < processEndAt)
                    {
                        await Task.Delay(1000);
                        Console.WriteLine($"Processing for {time++}s");
                    }

                    Console.WriteLine($"Releasing lock at {DateTime.UtcNow}");
                    await lockClient.ReleaseLockAsync(lockId);
                    Console.WriteLine($"Released lock at {DateTime.UtcNow}");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex);
                }

                Console.WriteLine("\nPress enter to continue!");
                Console.ReadLine();
            }
        }
    }
}
