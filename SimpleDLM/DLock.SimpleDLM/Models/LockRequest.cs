namespace DLock.SimpleDLM.Models
{
    public class LockRequest
    {
        public string LockId { get; set; }
        public string ConnectionId { get; set; }
        public int TimeoutMs { get; set; }
    }
}
