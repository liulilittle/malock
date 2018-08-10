namespace malock.Client
{
    public interface IWaitableHandler
    {
        bool WaitOne();

        bool WaitOne(int millisecondsTimeout);

        void Set();

        void Reset();

        void Close();
    }
}
