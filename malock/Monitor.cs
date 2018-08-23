namespace malock
{
    using global::malock.Client;
    using System;

    public sealed class Monitor : SyncBlockIndex
    {
        private sealed class MonitorWaitHandle : EventWaitHandle
        {
            public MonitorWaitHandle(object owner, string key, MalockClient malock) : base(owner, key, malock)
            {

            }

            protected override IWaitable NewWaitable()
            {
                return base.NewWaitable();
            }
        }

        private Monitor(string key, MalockClient malock) : base(key, malock)
        {

        }

        public static Monitor New(string key, MalockClient malock)
        {
            return NewOrGet(key, malock, () => new Monitor(key, malock));
        }

        public void Enter()
        {
            if (!this.Handle.TryEnter())
            {
                throw new InvalidOperationException("The state of the current local lock causes the lock not to be acquired");
            }
        }

        public bool TryEnter()
        {
            return this.TryEnter(-1);
        }

        public bool TryEnter(int millisecondsTimeout)
        {
            return this.Handle.TryEnter(millisecondsTimeout);
        }

        public void Exit()
        {
            this.Handle.Exit();
        }

        protected override EventWaitHandle NewWaitHandle(string key, MalockClient malock)
        {
            return new MonitorWaitHandle(this, key, malock);
        }
    }
}
