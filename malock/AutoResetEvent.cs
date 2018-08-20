namespace malock
{
    using global::malock.Client;

    public class AutoResetEvent : IHandle
    {
        private readonly EventWaitHandle handle;

        private sealed class WaitHandle : EventWaitHandle
        {
            public WaitHandle(string key, MalockClient malock) : base(key, malock)
            {

            }

            protected override IWaitableHandler NewWaitable()
            {
                return base.NewWaitable();
            }
        }

        public EventWaitHandle Handle
        {
            get
            {
                return this.handle;
            }
        }

        public AutoResetEvent(string key, MalockClient malock)
        {
            this.handle = new WaitHandle(key, malock);
        }

        public bool WaitOne()
        {
            return this.WaitOne(-1);
        }

        public bool WaitOne(int millisecondsTimeout)
        {
            return this.handle.TryEnter(millisecondsTimeout);
        }

        public void Set()
        {
            this.handle.Exit();
        }
    }
}
