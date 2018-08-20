namespace malock.Client
{
    public interface IWaitable
    {
        bool WaitOne();

        bool WaitOne(int millisecondsTimeout);

        void Set();

        void Reset();

        void Close();
    }
}
