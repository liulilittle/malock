namespace malock.NN
{
    using global::malock.Client;
    using global::malock.Common;
    using System;
    using System.Diagnostics;
    using System.IO;
    using MSG = global::malock.Common.MalockNameNodeMessage;

    public class NnsClient : MalockMixClient<MSG> 
    {
        public NnsClient(string identity, string mainuseNode, string standbyNode)
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

        public void QueryHostEntryAsync(string key, Action<NnsError, HostEntry> state)
        {
            this.InternalQueryHostEntryAsync(key, 3000, state, false);
        }

        public void QueryHostEntryAsync(string key, int timeout, Action<NnsError, HostEntry> state)
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
            this.InternalQueryHostEntryAsync(key, timeout, state, false);
        }

        private void InternalQueryHostEntryAsync(string key, int timeout, Action<NnsError, HostEntry> state, bool retrying)
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
                Action<NnsClient> reacquire = (self) =>
                {
                    stopwatch.Stop();
                    if (timeout != -1)
                    {
                        timeout -= Convert.ToInt32(stopwatch.ElapsedMilliseconds);
                    }
                    this.InternalQueryHostEntryAsync(key, timeout, state, timeout == -1 ? false : true);
                };
                Action<int, MalockMessage, Stream> callback = (errno, response, stream) =>
                {
                    if (errno == MSG.Mappable.ERROR_NOERROR)
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
                    else if (errno == MSG.Mappable.ERROR_ABORTED)
                    {
                        if (this.Available)
                        {
                            reacquire(this);
                        }
                        else
                        {
                            state(NnsError.kAborted, null);
                        }
                    }
                    else if (errno == MSG.Mappable.ERROR_TIMEOUT)
                    {
                        state(NnsError.kTimeout, null);
                    }
                };
                MalockMessage message = this.NewMessage(key, MSG.CLIENT_COMMAND_QUERYHOSTENTRYINFO);
                Exception exception = null;
                if (!MSG.TryInvokeAsync(this, message, timeout, callback, ref exception))
                {
                    if (this.Available)
                    {
                        reacquire(this);
                    }
                    else
                    {
                        state(NnsError.kAborted, null);
                    }
                }
                stopwatch.Stop();
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
