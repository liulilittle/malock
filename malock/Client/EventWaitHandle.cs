namespace malock.Client
{
    using global::malock.Common;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Threading;

    public abstract class EventWaitHandle
    {
        private class EventWaitHandleInvokeMapValue
        {
            public const int ERROR_TIMEOUT = 2;
            public const int ERROR_ABORTED = 1;
            public const int ERROR_NOERROR = 0;

            public EventWaitHandle Slot
            {
                get;
                set;
            }

            public Action<int, Message, Stream> State
            {
                get;
                set;
            }

            public Stopwatch Stopwatch
            {
                get;
                private set;
            }

            public int Timeout
            {
                get;
                set;
            }

            public MalockClient Socket
            {
                get;
                set;
            }

            public object Tag
            {
                get;
                set;
            }

            public EventWaitHandleInvokeMapValue()
            {
                this.Stopwatch = new Stopwatch();
            }
        }

        private static ConcurrentDictionary<int, EventWaitHandleInvokeMapValue> msgmap = new ConcurrentDictionary<int, EventWaitHandleInvokeMapValue>();
        private static Thread timeoutmaintaining = null;

        private static bool RegisterToMap(int msgid, EventWaitHandleInvokeMapValue map)
        {
            if (map == null)
            {
                throw new ArgumentNullException("map");
            }
            lock (msgmap)
            {
                if (!msgmap.TryAdd(msgid, map))
                {
                    return false;
                }
                else
                {
                    Stopwatch sw = map.Stopwatch;
                    sw.Reset();
                    sw.Start();
                }
                return true;
            }
        }

        private static bool FromRemoveInMap(int msgid)
        {
            return GetByMap(msgid) != null;
        }

        private static EventWaitHandleInvokeMapValue GetByMap(int msgid)
        {
            EventWaitHandleInvokeMapValue map;
            lock (msgmap)
            {
                if (msgmap.TryGetValue(msgid, out map))
                {
                    Stopwatch sw = map.Stopwatch;
                    sw.Stop();
                    EventWaitHandleInvokeMapValue value;
                    msgmap.TryRemove(msgid, out value);
                }
            }
            return map;
        }

        static EventWaitHandle()
        {
            EventWaitHandle.timeoutmaintaining = Run(() =>
            {
                while (true)
                {
                    foreach (KeyValuePair<int, EventWaitHandleInvokeMapValue> kv in msgmap)
                    {
                        EventWaitHandleInvokeMapValue map = kv.Value;
                        if (map == null || map.Timeout < 0)
                        {
                            continue;
                        }
                        Stopwatch sw = map.Stopwatch;
                        if (sw.ElapsedMilliseconds > map.Timeout)
                        {
                            sw.Stop();

                            EventWaitHandleInvokeMapValue value;
                            msgmap.TryRemove(kv.Key, out value);

                            var state = map.State;
                            if (state != null)
                            {
                                state(EventWaitHandleInvokeMapValue.ERROR_TIMEOUT, null, null);
                            }
                        }
                    }
                    Thread.Sleep(1);
                }
            });
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
        private readonly object syncobj = new object();

        public string Key
        {
            get;
            private set;
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

        internal EventWaitHandle(string key, MalockClient malock)
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
            this.Key = key;
            this.malock = malock;
            this.malock.Received += this.ProcessMessage;
            this.malock.Aborted += this.ProcessAbort;
        }

        private void ProcessAbort(object sender, EventArgs e)
        {
            foreach (KeyValuePair<int, EventWaitHandleInvokeMapValue> kv in msgmap)
            {
                EventWaitHandleInvokeMapValue map = kv.Value;
                if (map == null && map.Socket != this.malock)
                {
                    continue;
                }
                else
                {
                    EventWaitHandleInvokeMapValue mv;
                    msgmap.TryRemove(kv.Key, out mv);
                }
                var state = map.State;
                if (state != null)
                {
                    state(EventWaitHandleInvokeMapValue.ERROR_ABORTED, null, null);
                }
            }
            lock (this.syncobj)
            {
                if (!this.malock.Available)
                {
                    this.enterthread = null;
                    Interlocked.Exchange(ref this.entercount, 0);
                }
            }
        }

        private void ProcessMessage(Message message, Stream stream)
        {
            EventWaitHandle.EventWaitHandleInvokeMapValue map = EventWaitHandle.GetByMap(message.Sequence);
            if (map != null)
            {
                var state = map.State;
                if (state != null)
                {
                    state(EventWaitHandleInvokeMapValue.ERROR_NOERROR, message, stream);
                }
            }
        }

        private void ProcessMessage(object sender, MalockSocketStream e)
        {
            Message message = null;
            using (Stream stream = e.Stream)
            {
                try
                {
                    message = Message.Deserialize(stream);
                }
                catch (Exception)
                {
                    MalockSocket socket = e.Socket;
                    socket.Abort();
                }
                if (message != null)
                {
                    this.ProcessMessage(message, stream);
                }
            }
        }

        protected internal virtual bool TryEnter()
        {
            return this.TryEnter(-1);
        }

        private class Waitable : IWaitableHandler
        {
            private ManualResetEvent signal = new ManualResetEvent(false);

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

        public static IWaitableHandler NewDefaultWaitable()
        {
            return new Waitable();
        }

        protected virtual IWaitableHandler NewWaitable()
        {
            return NewDefaultWaitable();
        }

        private class MalockTryEnterCallback
        {
            public int millisecondsTimeout;
            public bool localTaken;
            public bool aborted; 
            private EventWaitHandle handle;
            private IWaitableHandler signal;

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

            public void Handle(int error, Message message, Stream stream)
            {
                if (error == EventWaitHandleInvokeMapValue.ERROR_ABORTED)
                {
                    aborted = true;
                }
                else if (error == EventWaitHandleInvokeMapValue.ERROR_NOERROR)
                {
                    if (message.Command == Message.CLIENT_COMMAND_LOCK_ENTER)
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
                    Thread.Sleep(Malock.SmoothingTime);
                    return this.InternalTryEnter(millisecondsTimeout, ref exception);
                }
            }
            lock (this.syncobj)
            {
                if (callback.localTaken)
                {
                    this.enterthread = currentThread;
                    Interlocked.Increment(ref entercount);
                }
            }
            return callback.localTaken;
        }

        private static MalockSocketException NewAbortedException()
        {
            return new MalockSocketException(EventWaitHandleInvokeMapValue.ERROR_ABORTED,
                        "An unknown interrupt occurred in the connection between the Malock and the server");
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

        private bool TryPostEnterMessage(int millisecondsTimeout, Action<int, Message, Stream> callback,
            ref Exception exception)
        {
            byte cmd = Message.CLIENT_COMMAND_LOCK_ENTER;
            Message message = this.NewMesssage(cmd, millisecondsTimeout);
            return this.TryInvokeAsync(message, millisecondsTimeout, callback, ref exception);
        }

        private bool TryPostExitMessage(ref Exception exception)
        {
            Message message = this.NewMesssage(Message.CLIENT_COMMAND_LOCK_EXIT, -1);
            return this.TryInvokeAsync(message, -1, null, ref exception);
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
                    lock (this.syncobj)
                    {
                        this.enterthread = null;
                    }
                    this.TryPostExitMessage(ref exception);
                }
            }
            return true;
        }

        private Message NewMesssage(byte command, int timeout)
        {
            Message message = new Message();
            message.Sequence = Message.NewId();
            message.Key = this.Key;
            message.Command = command;
            message.Timeout = timeout;
            message.Identity = this.malock.Identity;
            return message;
        }

        protected internal virtual IEnumerable<HandleInfo> GetAllInfo()
        {
            IEnumerable<HandleInfo> infos;
            Exception exception = null;
            TryGetAllInfo(out infos, ref exception);
            if (exception != null)
            {
                throw exception;
            }
            return infos;
        }

        protected internal virtual bool TryGetAllInfo(out IEnumerable<HandleInfo> infos, ref Exception exception)
        {
            IList<HandleInfo> results = new List<HandleInfo>();
            infos = results;
            using (AutoResetEvent events = new AutoResetEvent(false))
            {
                bool success = false;
                bool abort = false;
                if (!this.TryInvokeAsync(this.NewMesssage(Message.CLIENT_COMMAND_GETALLINFO, -1), -1,
                    (errno, message, stream) =>
                {
                    if (errno == EventWaitHandleInvokeMapValue.ERROR_NOERROR)
                    {
                        if (message.Command == Message.CLIENT_COMMAND_GETALLINFO)
                        {
                            success = HandleInfo.Fill(results, stream);
                        }
                    }
                    else if (errno == EventWaitHandleInvokeMapValue.ERROR_ABORTED)
                    {
                        abort = true;
                    }
                    events.Set();
                }, ref exception) || exception != null)
                {
                    return false;
                }
                events.WaitOne();
                if (abort)
                {
                    exception = NewAbortedException();
                    return false;
                }
                return success;
            }
        }

        private bool TryInvokeAsync(Message message, int timeout, Action<int, Message, Stream> callback, ref Exception exception)
        {
            EventWaitHandleInvokeMapValue mapinfo = new EventWaitHandle.EventWaitHandleInvokeMapValue()
            {
                Slot = this,
                State = callback,
                Tag = null,
                Timeout = timeout,
                Socket = this.malock,
            };
            if (!EventWaitHandle.RegisterToMap(message.Sequence, mapinfo))
            {
                exception = new InvalidOperationException("An internal error cannot add a call to a rpc-task in the map table");
                return false;
            }
            using (MemoryStream ms = (MemoryStream)message.Serialize())
            {
                if (!this.malock.Send(ms.GetBuffer(), 0, unchecked((int)ms.Position)))
                {
                    EventWaitHandle.FromRemoveInMap(message.Sequence);
                    exception = new InvalidOperationException("The poll send returned results do not match the expected");
                    return false;
                }
            }
            return true;
        }
    }
}
