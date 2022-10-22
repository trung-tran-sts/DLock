using System;

namespace DLock.SimpleDLM.Models
{
    public class LockRequest
    {
        public string Resource { get; set; }
        public string LockId { get; set; }
        public int TimeoutMs { get; set; }
        public DateTime WaitUntil { get; set; }
    }
}
