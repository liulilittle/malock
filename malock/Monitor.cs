namespace malock
{
    using global::malock.Client;
    using System;

    public sealed class Monitor : IHandle
    {
        private readonly EventWaitHandle handle;

        private sealed class MonitorWaitHandle : EventWaitHandle
        {
            public MonitorWaitHandle(string key, MalockClient malock) : base(key, malock)
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

        public Monitor(string key, MalockClient malock)
        {
            this.handle = new MonitorWaitHandle(key, malock);
        }

        public void Enter()
        {
            if (!this.handle.TryEnter())
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
            return this.handle.TryEnter(millisecondsTimeout);
        }

        public void Exit()
        {
            this.handle.Exit();
        }
    }
}
