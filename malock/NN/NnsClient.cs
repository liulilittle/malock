namespace malock.NN
{
    using global::malock.Client;
    using global::malock.Common;
    using global::malock.Core;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Threading;
    using MSG = global::malock.Common.MalockNnsMessage;

    public class NnsClient : MalockMixClient<MSG>
    {
        private class HostEntryCache
        {
            public HostEntry entry = null;
            public DateTime ts = DateTime.MinValue;
            private AsyncAutoResetEvent events = new AsyncAutoResetEvent(true);

            public void WaitOne(Action<bool> state)
            {
                this.events.WaitOne(state);
            }

            public void Set(bool state)
            {
                this.events.Set(state);
            }

            public bool IsExpired()
            {
                TimeSpan ts = unchecked(DateTime.Now - this.ts);
                return ts.TotalMilliseconds > Malock.CacheExpiredTime;
            }
        }

        private readonly Dictionary<string, HostEntryCache> caches = new Dictionary<string, HostEntryCache>();
        private static readonly HostEntry[] emptryentries = new HostEntry[0];

        internal NnsClient(string identity, string mainuseNode, string standbyNode)
            : this(identity, mainuseNode, standbyNode, null)
        {

        }

        internal NnsClient(string identity, string mainuseNode, string standbyNode, object state) :
            base(identity, mainuseNode, standbyNode, state)
        {

        }

        public new NnsClient Run()
        {
            return (NnsClient)base.Run();
        }

        protected override int GetLinkMode()
        {
            return MSG.LINK_MODE_CLIENT;
        }

        protected override bool TryDeserializeMessage(MalockSocketStream stream, out MSG message)
        {
            return MSG.TryDeserialize(stream.Stream, out message);
        }

        public void GetAllHostEntryAsync(Action<NnsError, IEnumerable<HostEntry>> state)
        {
            GetAllHostEntryAsync(Malock.DefaultTimeout, state);
        }

        public void GetAllHostEntryAsync(int timeout, Action<NnsError, IEnumerable<HostEntry>> state)
        {
            if (state == null)
            {
                throw new ArgumentNullException("state");
            }
            if (timeout <= 0 && timeout != -1)
            {
                state(NnsError.kTimeout, emptryentries);
            }
            else
            {
                Exception exception = null;
                if (!MalockMessage.TryInvokeAsync(this, this.NewMessage(null, MSG.CLIENT_COMMAND_DUMPHOSTENTRYINFO),
                    timeout, (errno, message, stream) =>
                    {
                        if (errno == MalockMessage.Mappable.ERROR_NOERROR)
                        {
                            IList<HostEntry> hosts = new List<HostEntry>();
                            NnsTable.Host.DeserializeAll(stream, (host) =>
                            {
                                HostEntry entry = host.Entry;
                                hosts.Add(entry);
                            });
                            state(NnsError.kSuccess, hosts);
                        }
                        else if (errno == MalockMessage.Mappable.ERROR_TIMEOUT)
                        {
                            state(NnsError.kTimeout, emptryentries);
                        }
                        else if (errno == MalockMessage.Mappable.ERROR_ABORTED)
                        {
                            state(NnsError.kAborted, emptryentries);
                        }
                    }, ref exception))
                {
                    state(NnsError.kAborted, emptryentries);
                }
            }
        }

        public NnsError TryGetAllHostEntry(out IEnumerable<HostEntry> entries)
        {
            return TryGetAllHostEntry(Malock.DefaultTimeout, out entries);
        }

        public NnsError TryGetAllHostEntry(int timeout, out IEnumerable<HostEntry> entries)
        {
            return TryInternalInvoke((callback) => GetAllHostEntryAsync(timeout, callback), out entries);
        }

        public NnsError TryGetHostEntry(string key, out HostEntry entry)
        {
            return TryGetHostEntry(key, Malock.DefaultTimeout, out entry);
        }

        public NnsError TryGetHostEntry(string key, int timeout, out HostEntry entry)
        {
            if (key == null)
            {
                throw new ArgumentNullException("key");
            }
            if (key.Length <= 0)
            {
                throw new ArgumentOutOfRangeException("key");
            }
            return TryInternalInvoke((callback) => GetHostEntryAsync(key, timeout, callback), out entry);
        }

        private NnsError TryInternalInvoke<TModel>(Action<Action<NnsError, TModel>> callback, out TModel model)
        {
            NnsError result_errno = NnsError.kError;
            TModel result_model = default(TModel);
            model = default(TModel);
            using (ManualResetEvent events = new ManualResetEvent(false))
            {
                callback((errno, info) =>
                {
                    result_errno = errno;
                    if (errno == NnsError.kSuccess)
                    {
                        result_model = info;
                    }
                    events.Set();
                });
                events.WaitOne();
                if (result_errno == NnsError.kSuccess)
                {
                    model = result_model;
                }
            }
            return result_errno;
        }

        public void GetHostEntryAsync(string key, Action<NnsError, HostEntry> state)
        {
            this.InternalGetHostEntryAsync(key, Malock.DefaultTimeout, state);
        }

        public void GetHostEntryAsync(string key, int timeout, Action<NnsError, HostEntry> state)
        {
            if (state == null)
            {
                throw new ArgumentNullException("state");
            }
            if (key == null)
            {
                throw new ArgumentNullException("key");
            }
            if (key.Length <= 0)
            {
                throw new ArgumentOutOfRangeException("key");
            }
            this.InternalGetHostEntryAsync(key, timeout, state);
        }

        private void InternalGetHostEntryAsync(string key, int timeout, Action<NnsError, HostEntry> state)
        {
            HostEntryCache cache;
            lock (this.caches)
            {
                if (!this.caches.TryGetValue(key, out cache))
                {
                    cache = new HostEntryCache();
                    this.caches.Add(key, cache);
                }
            }
            if (!cache.IsExpired())
            {
                state(NnsError.kSuccess, cache.entry);
            }
            else
            {
                cache.WaitOne((callbackstate) =>
                {
                    if (!cache.IsExpired())
                    {
                        cache.Set(callbackstate);
                        state(NnsError.kSuccess, cache.entry);
                    }
                    else
                    {
                        this.InternalRemoteGetHostEntryAsync(key, timeout, (error, entry) =>
                        {
                            if (error == NnsError.kSuccess)
                            {
                                cache.entry = entry;
                                cache.ts = DateTime.Now;
                            }
                            state(error, entry);
                            cache.Set(callbackstate);
                        }, false);
                    }
                });
            }
        }

        private void InternalRemoteGetHostEntryAsync(string key, int timeout, Action<NnsError, HostEntry> state, bool retrying)
        {
            if ((retrying && timeout <= 0) || (timeout <= 0 && timeout != -1))
            {
                state(NnsError.kTimeout, null);
            }
            else if (!this.Available)
            {
                state(NnsError.kAborted, null);
            }
            else
            {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                Action<NnsClient> onaborted = (self) =>
                {
                    if (!this.Available)
                    {
                        state(NnsError.kAborted, null);
                    }
                    else
                    {
                        var delaytick = Malock.NewTimer();
                        delaytick.Interval = Malock.SmoothingTime;
                        delaytick.Tick += delegate
                        {
                            long elapsedMilliseconds = stopwatch.ElapsedMilliseconds;
                            {
                                stopwatch.Stop();
                                delaytick.Stop();
                            }
                            if (!this.Available)
                            {
                                state(NnsError.kAborted, null);
                            }
                            else
                            {
                                if (timeout != -1)
                                {
                                    timeout -= Convert.ToInt32(elapsedMilliseconds);
                                }
                                this.InternalRemoteGetHostEntryAsync(key, timeout, state, timeout == -1 ? false : true);
                            }
                        };
                        delaytick.Start();
                    }
                };
                Action<int, MalockMessage, Stream> callback = (errno, response, stream) =>
                {
                    bool closedstopwatch = true;
                    if (errno == MalockMessage.Mappable.ERROR_NOERROR)
                    {
                        if (response.Command != MSG.CLIENT_COMMAND_QUERYHOSTENTRYINFO)
                        {
                            state(NnsError.kError, null);
                        }
                        else
                        {
                            HostEntry entry;
                            if (!HostEntry.TryDeserialize(stream, out entry))
                            {
                                state(NnsError.kError, null);
                            }
                            else
                            {
                                state(NnsError.kSuccess, entry);
                            }
                        }
                    }
                    else if (errno == MalockMessage.Mappable.ERROR_ABORTED)
                    {
                        closedstopwatch = false;
                        onaborted(this);
                    }
                    else if (errno == MalockMessage.Mappable.ERROR_TIMEOUT)
                    {
                        state(NnsError.kTimeout, null);
                    }
                    if (closedstopwatch)
                    {
                        stopwatch.Stop();
                    }
                };
                MalockMessage message = this.NewMessage(key, MSG.CLIENT_COMMAND_QUERYHOSTENTRYINFO);
                Exception exception = null;
                if (!MalockMessage.TryInvokeAsync(this, message, timeout, callback, ref exception))
                {
                    onaborted(this);
                }
            }
        }

        private MSG NewMessage(string key, byte command)
        {
            MSG message = new MSG();
            message.Command = command;
            message.Sequence = MSG.NewId();
            message.Key = key;
            return message;
        }

        protected override int GetListenPort()
        {
            return 0;
        }
    }
}
