namespace TimeEcho
{
    public interface IRewindable
    {
        void Capture(float timelineTime);
        void Restore(float timelineTime);
        void TrimFuture(float timelineTime);
        void BeginRewind();
        void EndRewind();
    }
}
