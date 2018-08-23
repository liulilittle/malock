namespace malock
{
    using global::malock.Client;
    using System;
    using System.Diagnostics;
    using Interlocked = System.Threading.Interlocked;
    using Thread = System.Threading.Thread;

    public sealed class SpinLock : SyncBlockIndex
    {
        private class SpinLockWaitable : IWaitable
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

        private sealed class SpinLockWaitHandle : EventWaitHandle
        {
            private readonly SpinLock locker = default(SpinLock);

            public SpinLockWaitHandle(SpinLock owner, string key, MalockClient malock) :
                base(owner, key, malock)
            {
                this.locker = owner;
            }

            protected override IWaitable NewWaitable()
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

        private SpinLock(string key, MalockClient malock, bool reduce) : base(key, malock)
        {
            this.Reduce = reduce;
        }

        public static SpinLock New(string key, MalockClient malock)
        {
            return New(key, malock, true);
        }

        public static SpinLock New(string key, MalockClient malock, bool reduce)
        {
            return NewOrGet(key, malock, () => new SpinLock(key, malock, reduce));
        }

        public bool Reduce
        {
            get;
            private set;
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
            return new SpinLockWaitHandle(this, key, malock);
        }
    }
}
