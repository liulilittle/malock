namespace malock
{
    using global::malock.Client;

    public class AutoResetEvent : SyncBlockIndex
    {
        private AutoResetEvent(string key, MalockClient malock) : base(key, malock)
        {
            
        }

        public static AutoResetEvent New(string key, MalockClient malock)
        {
            return NewOrGet(key, malock, () => new AutoResetEvent(key, malock));
        }

        public bool WaitOne()
        {
            return this.WaitOne(-1);
        }

        public bool WaitOne(int millisecondsTimeout)
        {
            return this.Handle.TryEnter(millisecondsTimeout);
        }

        public void Set()
        {
            this.Handle.Exit();
        }

        protected override EventWaitHandle NewWaitHandle(string key, MalockClient malock)
        {
            return EventWaitHandle.NewDefaultWaitHandle(this, key, malock);
        }
    }
}
