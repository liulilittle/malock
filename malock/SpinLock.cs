namespace malock
{
    using global::malock.Client;
    using System;
    using System.Diagnostics;
    using Interlocked = System.Threading.Interlocked;
    using Thread = System.Threading.Thread;

    public sealed class SpinLock
    {
        private readonly EventWaitHandle handle;

        private class SpinLockWaitable : IWaitableHandler
        {
            private volatile int signal = 0;
            private bool reduce = false;

            public void Close()
            {
                Interlocked.CompareExchange(ref signal, 0, 1);
            }

            public void Set()
            {
                Interlocked.CompareExchange(ref signal, 1, 0);
            }

            public bool WaitOne()
            {
                return this.WaitOne(-1);
            }

            public SpinLockWaitable(bool reduce)
            {
                this.reduce = reduce;
            }

            public bool WaitOne(int millisecondsTimeout)
            {
                Stopwatch stopwatch = new Stopwatch();
                while (millisecondsTimeout == -1 ||
                    stopwatch.ElapsedMilliseconds < millisecondsTimeout)
                {
                    if (Interlocked.CompareExchange(ref signal, 0, 1) == 1)
                    {
                        return true;
                    }
                    if (this.reduce)
                    {
                        Thread.Sleep(1);
                    }
                }
                return false;
            }

            public void Reset()
            {
                this.Close();
            }
        }

        public EventWaitHandle Handle
        {
            get
            {
                return this.handle;
            }
        }

        private sealed class SpinLockWaitHandle : EventWaitHandle
        {
            private readonly SpinLock locker = default(SpinLock);

            public SpinLockWaitHandle(string key, MalockClient malock, SpinLock locker) : base(key, malock)
            {
                this.locker = locker;
            }

            protected override IWaitableHandler NewWaitable()
            {
                return new SpinLockWaitable(this.locker.Reduce);
            }
        }

        public SpinLock(string key, MalockClient malock) : this(key, malock, true)
        {

        }

        public SpinLock(string key, MalockClient malock, bool reduce)
        {
            this.Reduce = reduce;
            this.handle = new SpinLockWaitHandle(key, malock, this);
        }

        public bool Reduce
        {
            get;
            private set;
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
