namespace malock
{
    using global::malock.Client;

    public interface IHandle
    {
        EventWaitHandle Handle
        {
            get;
        }
    }
}
