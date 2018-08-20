namespace malock
{
    using global::malock.Client;
    using System;
    using System.Diagnostics;
    using Interlocked = System.Threading.Interlocked;
    using Thread = System.Threading.Thread;

    public sealed class SpinLock : IHandle
    {
        private readonly EventWaitHandle handle;

        private class SpinLockWaitable : IWaitableHandler
        {
            private volatile int signal = 0x00;
            private bool reduce = false;

            public void Close()
            {
                this.Reset();
            }

            public void Set()
            {
                Interlocked.CompareExchange(ref signal, 0x01, 0x00);
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
                    if (Interlocked.CompareExchange(ref this.signal, 0x00, 0x00) != 0x00)
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
                Interlocked.CompareExchange(ref signal, 0x00, 0x01);
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

        public static bool DefaultReduce
        {
            get
            {
                return true;
            }
        }

        public SpinLock(string key, MalockClient malock) : this(key, malock, SpinLock.DefaultReduce)
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
