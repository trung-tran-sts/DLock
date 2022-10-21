using System;
using System.Diagnostics.CodeAnalysis;

namespace DLock.SimpleDLM.Models
{
    public class AcquiredLockInfo : IEquatable<AcquiredLockInfo>
    {
        public AcquiredLockInfo(string lockId, int timeoutMs = 7000)
        {
            LockId = lockId;
            AcquiredTime = DateTime.UtcNow;
            TimeoutMs = timeoutMs;
        }

        public string LockId { get; set; }
        public DateTime AcquiredTime { get; }
        public DateTime ValidUntil => AcquiredTime.AddMilliseconds(TimeoutMs);
        public int TimeoutMs { get; }

        public override bool Equals(object obj)
        {
            return Equals(obj as AcquiredLockInfo);
        }

        public bool Equals([AllowNull] AcquiredLockInfo other)
        {
            return other != null && LockId == other.LockId;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(LockId);
        }
    }
}
