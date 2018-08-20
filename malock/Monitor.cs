namespace malock
{
    using global::malock.Client;
    using System;

    public sealed class Monitor : IEventWaitHandle
    {
        private readonly EventWaitHandle handle;

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

        public EventWaitHandle Handle
        {
            get
            {
                return this.handle;
            }
        }

        public Monitor(string key, MalockClient malock)
        {
            this.handle = new MonitorWaitHandle(this, key, malock);
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
