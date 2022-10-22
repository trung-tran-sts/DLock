using System.Threading;

namespace DLock.SimpleDLM.Shared
{
    public class CustomSemaphoreSlim : SemaphoreSlim
    {
        public bool IsValid { get; private set; } = true;

        public CustomSemaphoreSlim(int initialCount) : base(initialCount)
        {
        }

        public CustomSemaphoreSlim(int initialCount, int maxCount) : base(initialCount, maxCount)
        {
        }

        public void Invalidate()
        {
            IsValid = false;
        }
    }
}
