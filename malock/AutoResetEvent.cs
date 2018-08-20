namespace malock
{
    using global::malock.Client;

    public class AutoResetEvent : IEventWaitHandle
    {
        private readonly EventWaitHandle handle;

        private sealed class WaitHandle : EventWaitHandle
        {
            public WaitHandle(object owner, string key, MalockClient malock) : base(owner, key, malock)
            {

            }

            protected override IWaitable NewWaitable()
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
            this.handle = new WaitHandle(this, key, malock);
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
