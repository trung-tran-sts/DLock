using DLock.LockClient.Services;
using System;
using System.Threading.Tasks;

namespace DLock.LockClient
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            int choice;

            do
            {
                Console.WriteLine("1. Simple DLM");
                Console.Write("Choose a lock implementation: ");
            }
            while (!int.TryParse(Console.ReadLine(), out choice));

            switch (choice)
            {
                case 1: await RunSimpleDLM(); break;
                default: Console.WriteLine("Invalid choice"); break;
            }
        }

        static async Task RunSimpleDLM()
        {
            SimpleDLMLockClient lockClient = new SimpleDLMLockClient("https://localhost:5001/hub/locks");

            await lockClient.TryInitializeAsync();

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
                    Console.Write("Input lock id: ");
                    string lockId = Console.ReadLine();
                    Console.Write("Input lock timeout ms (TTL): ");
                    int timeoutMs = int.Parse(Console.ReadLine());
                    Console.Write("Input wait timeout ms: ");
                    int waitTimeoutMs = int.Parse(Console.ReadLine());
                    Console.Write("Input process time ms: ");
                    int processTimeMs = int.Parse(Console.ReadLine());

                    Console.WriteLine($"Acquiring lock at {DateTime.UtcNow}");
                    await lockClient.AcquireLockAsync(lockId, timeoutMs, TimeSpan.FromMilliseconds(waitTimeoutMs));
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
