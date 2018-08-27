namespace malock.Client
{
    using global::malock.Common;
    using System;
    using System.IO;
    using System.Threading;
    using Timer = global::malock.Core.Timer;

    public abstract class EventWaitHandle : IEventWaitHandle
    {
        private sealed class DefaultWaitHandle : EventWaitHandle
        {
            public DefaultWaitHandle(object owner, string key, MalockClient malock) : base(owner, key, malock)
            {

            }

            protected override IWaitable NewWaitable()
            {
                return base.NewWaitable();
            }
        }

        protected internal static EventWaitHandle NewDefaultWaitHandle(object owner, string key, MalockClient malock)
        {
            return new DefaultWaitHandle(owner, key, malock);
        }

        public static void Sleep(int millisecondsTimeout)
        {
            Thread.Sleep(millisecondsTimeout);
        }

        public static Thread Run(ThreadStart startRoutine)
        {
            if (startRoutine == null)
            {
                throw new ArgumentNullException("startRoutine");
            }
            Thread thread = new Thread(startRoutine);
            thread.SetApartmentState(ApartmentState.MTA);
            thread.IsBackground = false;
            thread.Priority = ThreadPriority.Highest;
            thread.Start();
            return thread;
        }

        public static Thread Run(ParameterizedThreadStart startRoutine, object state)
        {
            if (startRoutine == null)
            {
                throw new ArgumentNullException("startRoutine");
            }
            Thread thread = new Thread(startRoutine);
            thread.SetApartmentState(ApartmentState.MTA);
            thread.IsBackground = false;
            thread.Priority = ThreadPriority.Highest;
            thread.Start(state);
            return thread;
        }

        private readonly MalockClient malock = null;
        private volatile Thread enterthread = null;
        private volatile int entercount = 0;
        private readonly object owner = null;
        private readonly object syncobj = new object();
        private readonly Timer ackstatetimer = null;

        public virtual string Key
        {
            get;
            private set;
        }

        EventWaitHandle IEventWaitHandle.Handle
        {
            get
            {
                return this;
            }
        }

        public override string ToString()
        {
            return this.Key;
        }

        public virtual MalockClient GetMalock()
        {
            return this.malock;
        }

        public virtual Thread GetEnterThread()
        {
            return this.enterthread;
        }

        public virtual string GetIdentity()
        {
            return this.malock.Identity;
        }

        internal EventWaitHandle(object owner, string key, MalockClient malock)
        {
            if (malock == null)
            {
                throw new ArgumentNullException("Malock can not be null");
            }
            if (key == null)
            {
                throw new ArgumentNullException("Key is absolutely not allowed to be null");
            }
            if (key.Length <= 0)
            {
                throw new ArgumentOutOfRangeException("Key is absolutely not allowed to be an empty string");
            }
            this.owner = owner;
            this.Key = key;
            this.malock = malock;
            this.malock.Aborted += this.ProcessAbort;
            do
            {
                this.ackstatetimer = new Timer();
                this.ackstatetimer.Tick += this.OnAckstatetimerTick;
                this.ackstatetimer.Interval = Malock.AckPipelineInterval;
                this.ackstatetimer.Start();
            } while (false);
        }

        public virtual object GetOwner()
        {
            return this.owner;
        }

        private void OnAckstatetimerTick(object sender, EventArgs e)
        {
            Exception exception = null;
            this.TryPostAckLockStateMessage(ref exception);
        }

        private bool TryPostAckLockStateMessage(ref Exception exception)
        {
            byte errno = MalockNodeMessage.CLIENT_COMMAND_LOCK_ACKPIPELINEENTER;
            lock (this.syncobj)
            {
                if (this.enterthread == null)
                {
                    errno = MalockNodeMessage.CLIENT_COMMAND_LOCK_ACKPIPELINEEXIT;
                }
            }
            MalockMessage message = this.NewMesssage(errno, -1);
            return MalockMessage.TrySendMessage(this.malock, message, ref exception);
        }

        private void ProcessAbort(object sender, EventArgs e)
        {
            lock (this.syncobj)
            {
                if (!this.malock.Available)
                {
                    this.enterthread = null;
                    Interlocked.Exchange(ref this.entercount, 0);
                }
            }
        }

        protected internal virtual bool TryEnter()
        {
            return this.TryEnter(-1);
        }

        private class Waitable : IWaitable
        {
            private readonly ManualResetEvent signal = null;

            public Waitable()
            {
                this.signal = new ManualResetEvent(false);
            }

            public void Set()
            {
                signal.Set();
            }

            public void Reset()
            {
                signal.Reset();
            }

            public bool WaitOne()
            {
                return signal.WaitOne();
            }

            public void Close()
            {
                this.signal.Close();
            }

            public bool WaitOne(int millisecondsTimeout)
            {
                return signal.WaitOne(millisecondsTimeout);
            }
        }

        public static IWaitable NewDefaultWaitable()
        {
            return new Waitable();
        }

        protected virtual IWaitable NewWaitable()
        {
            return NewDefaultWaitable();
        }

        private class MalockTryEnterCallback
        {
            public int millisecondsTimeout;
            public bool localTaken;
            public bool aborted;
            private EventWaitHandle handle;
            private IWaitable signal;

            public MalockTryEnterCallback(EventWaitHandle handle)
            {
                this.handle = handle;
                this.signal = handle.NewWaitable();
            }

            public void Set()
            {
                signal.Set();
            }

            public void Close()
            {
                signal.Close();
            }

            public bool WaitOne(int ms = -1)
            {
                return signal.WaitOne(ms);
            }

            public EventWaitHandle GetWaitHandle()
            {
                return this.handle;
            }

            public void Handle(int error, MalockMessage message, Stream stream)
            {
                if (error == MalockMessage.Mappable.ERROR_ABORTED)
                {
                    aborted = true;
                }
                else if (error == MalockMessage.Mappable.ERROR_NOERROR)
                {
                    if (message.Command == MalockNodeMessage.CLIENT_COMMAND_LOCK_ENTER)
                    {
                        this.localTaken = true;
                    }
                }
                this.Set();
            }
        }

        private bool InternalTryEnter(int millisecondsTimeout, ref Exception exception)
        {
            if (Interlocked.CompareExchange(ref this.entercount, 0, 0) > Malock.MaxEnterCount)
            {
                exception = new InvalidOperationException(string.Format("The number of times the same thread has been reentrant has exceeded the maximum ({0}) limit", Malock.MaxEnterCount));
                return false;
            }
            if (millisecondsTimeout != -1 && millisecondsTimeout < 1000)
            {
                exception = new ArgumentOutOfRangeException("Malock connection may be interrupted while interacting with the server so it is recommended to wait at least 1000ms");
                return false;
            }
            MalockTryEnterCallback callback = new MalockTryEnterCallback(this)
            {
                localTaken = false,
                aborted = false,
                millisecondsTimeout = millisecondsTimeout,
            };
            Thread currentThread = Thread.CurrentThread;
            lock (this.syncobj)
            {
                if (currentThread == this.enterthread)
                {
                    callback.localTaken = true;
                }
            }
            if (!callback.localTaken)
            {
                bool requirereentry = false;
                do
                {
                    if (this.TryPostEnterMessage(millisecondsTimeout, callback.Handle, ref exception))
                    {
                        callback.WaitOne();
                    }
                    else
                    {
                        requirereentry = true;
                    }
                    callback.Close();
                } while (false);
                if (callback.aborted)
                {
                    requirereentry = true;
                }
                if (requirereentry)
                {
                    if (!malock.Available)
                    {
                        exception = EventWaitHandle.NewAbortedException();
                        return false;
                    }
                    Thread.Sleep(Malock.SmoothingInvokeTime);
                    return this.InternalTryEnter(millisecondsTimeout, ref exception);
                }
            }
            lock (this.syncobj)
            {
                if (callback.localTaken)
                {
                    this.enterthread = currentThread;
                    Interlocked.Increment(ref this.entercount);
                }
            }
            return callback.localTaken;
        }

        protected internal static MalockSocketException NewAbortedException()
        {
            return new MalockSocketException(MalockMessage.Mappable.ERROR_ABORTED,
                        "An unknown interrupt occurred in the connection between the Malock and the server");
        }

        protected internal static TimeoutException NewTimeoutException()
        {
            return new TimeoutException("The malock invoke exceeded the expected maximum allowable timeout");
        }

        protected internal virtual bool TryEnter(int millisecondsTimeout)
        {
            Exception exception = null;
            bool localTaken = this.InternalTryEnter(millisecondsTimeout, ref exception);
            if (exception != null)
            {
                throw exception;
            }
            return localTaken;
        }

        private bool TryPostEnterMessage(int millisecondsTimeout, Action<int, MalockMessage, Stream> callback,
            ref Exception exception)
        {
            byte cmd = MalockNodeMessage.CLIENT_COMMAND_LOCK_ENTER;
            MalockMessage message = this.NewMesssage(cmd, millisecondsTimeout);
            return MalockMessage.TryInvokeAsync(this.malock, message, millisecondsTimeout, callback, ref exception);
        }

        private bool TryPostExitMessage(ref Exception exception)
        {
            MalockMessage message = this.NewMesssage(MalockNodeMessage.CLIENT_COMMAND_LOCK_EXIT, -1);
            return MalockMessage.TrySendMessage(this.malock, message, ref exception);
        }

        protected internal virtual bool Exit()
        {
            Exception exception = null;
            bool success = this.TryExit(ref exception);
            if (exception != null)
            {
                throw exception;
            }
            return success;
        }

        protected internal virtual bool TryExit(ref Exception exception)
        {
            Thread currententerthread = null;
            lock (this.syncobj)
            {
                currententerthread = this.enterthread;
                if (Thread.CurrentThread != currententerthread)
                {
                    exception = new InvalidOperationException("The current thread did not acquire a lock and could not perform an exit lock operation");
                    return false;
                }
                if (Interlocked.Decrement(ref this.entercount) <= 0)
                {
                    this.enterthread = null;
                    this.TryPostExitMessage(ref exception);
                }
            }
            return true;
        }

        protected MalockMessage NewMesssage(byte command, int timeout)
        {
            return MalockNodeMessage.New(this.Key, this.GetIdentity(), command, timeout);
        }
    }
}
