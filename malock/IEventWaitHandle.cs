namespace malock
{
    using global::malock.Client;

    public interface IEventWaitHandle
    {
        EventWaitHandle Handle
        {
            get;
        }
    }
}
