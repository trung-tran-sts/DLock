using System;
using System.Diagnostics.CodeAnalysis;

namespace DLock.SimpleDLM.Models
{
    public class AcquiredLockInfo : IEquatable<AcquiredLockInfo>
    {
        public AcquiredLockInfo(string resource, string lockId = null, int timeoutMs = 7000)
        {
            Resource = resource;
            LockId = lockId;
            AcquiredTime = DateTime.UtcNow;
            TimeoutMs = timeoutMs;
        }

        public string Resource { get; set; }
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
