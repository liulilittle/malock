namespace malock.Client
{
    using global::malock.Common;
    using global::malock.Core;
    using System;
    using MSG = global::malock.Common.MalockNodeMessage;
    using System.Collections.Concurrent;
    using System.Net;

    public abstract class MalockMixClient<TMessage> : EventArgs, IMalockSocket
        where TMessage : MalockMessage
    {
        private MalockSocket[] sockets = new MalockSocket[2];
        private IMalockSocket preferred = null; // 首选服务器索引
        private DateTime firsttime = DateTime.MinValue;
        private readonly object syncobj = new object();
        private readonly object state = null;
        private readonly MixEvent<EventHandler<MalockNetworkMessage>> messageevents = new MixEvent<EventHandler<MalockNetworkMessage>>();
        private readonly MixEvent<EventHandler> abortedevents = new MixEvent<EventHandler>();
        private readonly MixEvent<EventHandler> cconnectedevents = new MixEvent<EventHandler>();
        private ConcurrentDictionary<IMalockSocket, DateTime> abortedtime = new ConcurrentDictionary<IMalockSocket, DateTime>();

        private const int BESTMAXCONNECTTIME = 1000;

        public virtual event EventHandler<MalockNetworkMessage> Message
        {
            add
            {
                this.messageevents.Add(value);
            }
            remove
            {
                this.messageevents.Remove(value);
            }
        }
        public virtual event EventHandler Connected
        {
            add
            {
                this.cconnectedevents.Add(value);
            }
            remove
            {
                this.cconnectedevents.Remove(value);
            }
        }
        public virtual event EventHandler Aborted
        {
            add
            {
                this.abortedevents.Add(value);
            }
            remove
            {
                this.abortedevents.Remove(value);
            }
        }
        public virtual event EventHandler Ready = null; // 准备就绪
        /// <summary>
        /// 代表客户端唯一的身份标识
        /// </summary>
        public string Identity
        {
            get;
            private set;
        }
        /// <summary>
        /// 测量当前客户端的状态是否可用
        /// </summary>
        public bool Available
        {
            get
            {
                for (int i = 0; i < sockets.Length; i++)
                {
                    MalockSocket socket = sockets[i];
                    if (socket.Available)
                    {
                        return true;
                    }
                }
                return false;
            }
        }
        /// <summary>
        /// 已就绪
        /// </summary>
        public bool IsReady
        {
            get;
            private set;
        }
        /// <summary>
        /// 自定义标记的数据
        /// </summary>
        public object Tag
        {
            get;
            set;
        }
        /// <summary>
        /// 创建一个双机热备的 malock 客户端
        /// </summary>
        /// <param name="identity">代表客户端唯一的身份标识</param>
        /// <param name="mainuseNode">主用服务器主机地址</param>
        /// <param name="standbyNode">备用服务器主机地址</param>
        /// <param name="state">自定标记的状态对象</param>
        internal MalockMixClient(string identity, string mainuseNode, string standbyNode, object state)
        {
            if (identity == null)
            {
                throw new ArgumentNullException("identity");
            }
            if (identity.Length <= 0)
            {
                throw new ArgumentOutOfRangeException("identity");
            }
            if (mainuseNode == null)
            {
                throw new ArgumentNullException("mainuseNode");
            }
            if (mainuseNode.Length <= 0)
            {
                throw new ArgumentOutOfRangeException("mainuseNode");
            }
            if (standbyNode == null)
            {
                throw new ArgumentNullException("standbyNode");
            }
            if (standbyNode.Length <= 0)
            {
                throw new ArgumentOutOfRangeException("standbyNode");
            }
            this.Identity = identity;
            this.state = state;
            int linkMode = this.GetLinkMode();
            int listenport = this.GetListenPort();
            sockets[0] = new MalockSocket(identity, mainuseNode, listenport, linkMode);
            sockets[1] = new MalockSocket(identity, standbyNode, listenport, linkMode);
            for (int i = 0; i < sockets.Length; i++)
            {
                MalockSocket socket = sockets[i];
                socket.Aborted += this.SocketAborted;
                socket.Connected += this.SocketConnected;
                socket.Received += this.SocketReceived;
            }
            this.BindEventToMessage();
        }

        protected virtual void BindEventToMessage()
        {
            MalockMessage.Bind(this);
        }

        protected virtual void UnbindEventInMessage()
        {
            MalockMessage.Unbind(this);
        }

        protected virtual object GetStateObject()
        {
            return this.state;
        }

        protected abstract int GetListenPort();

        protected abstract int GetLinkMode();

        protected virtual IMalockSocket[] GetInnerSockets()
        {
            return this.sockets;
        }

        public MalockMixClient<TMessage> Run()
        {
            lock (this.syncobj)
            {
                if (this.firsttime == DateTime.MinValue && !this.Available)
                {
                    for (int i = 0; i < sockets.Length; i++)
                    {
                        MalockSocket socket = sockets[i];
                        socket.Run();
                    }
                    if (!this.IsReady)
                    {
                        this.firsttime = DateTime.Now;
                        var waitforconn = Malock.NewTimer();
                        waitforconn.Tick += (sender, e) =>
                        {
                            MalockSocket socket = null;
                            bool readying = false;
                            lock (this.syncobj)
                            {
                                if (!this.IsReady)
                                {
                                    socket = (MalockSocket)this.preferred;
                                    if (socket != null && this.Available)
                                    {
                                        readying = true;
                                        this.IsReady = true;
                                    }
                                    waitforconn.Stop();
                                }
                            }
                            if (readying)
                            {
                                this.OnReady(socket);
                            }
                        };
                        waitforconn.Interval = BESTMAXCONNECTTIME;
                        waitforconn.Start();
                    }
                }
                return this;
            }
        }

        protected internal static string GetNetworkAddress(IMalockSocket malock)
        {
            if (malock == null)
            {
                return null;
            }
            do
            {
                MalockSocket socket = malock as MalockSocket;
                if (socket != null)
                {
                    EndPoint ep = socket.Address;
                    if (ep != null)
                    {
                        return ep.ToString();
                    }
                }
            } while (false);
            do
            {
                var socket = malock as global::malock.Server.MalockSocket;
                if (socket != null)
                {
                    return socket.Address;
                }
            } while (false);
            return null;
        }

        protected internal static string GetEtherAddress(IMalockSocket malock)
        {
            if (malock == null)
            {
                return null;
            }
            do
            {
                MalockSocket socket = malock as MalockSocket;
                if (socket != null)
                {
                    IPAddress address = socket.GetLocalEtherAddress();
                    if (address != null)
                    {
                        return address.ToString();
                    }
                }
            } while (false);
            do
            {
                var socket = malock as global::malock.Server.MalockSocket;
                if (socket != null)
                {
                    IPAddress address = socket.GetRemoteEtherAddress();
                    if (address != null)
                    {
                        return address.ToString();
                    }
                }
            } while (false);
            return null;
        }

        protected virtual void SocketReceived(object sender, MalockSocketStream e)
        {
            TMessage message = default(TMessage);
            if (!this.TryDeserializeMessage(e, out message))
            {
                MalockSocket socket = e.Socket;
                socket.Abort();
            }
            else if (message != null)
            {
                this.OnMessage(new MalockNetworkMessage<TMessage>(this, e.Socket, e.Stream, message));
            }
        }

        protected virtual bool TryDeserializeMessage(MalockSocketStream stream, out TMessage message)
        {
            MSG msg;
            if (!MSG.TryDeserialize(stream.Stream, out msg))
            {
                message = default(TMessage);
                return false;
            }
            message = (TMessage)(object)msg;
            return true;
        }

        protected virtual void OnAborted(EventArgs e)
        {
            this.abortedevents.Invoke((evt) => evt(this, e));
        }

        protected virtual void OnConnected(EventArgs e)
        {
            this.abortedevents.Invoke((evt) => evt(this, e));
        }

        protected virtual void OnMessage(MalockNetworkMessage<TMessage> e)
        {
            this.messageevents.Invoke((evt) => evt(this, e));
        }

        protected virtual IMalockSocket Select(IMalockSocket socket)
        {
            if (socket == null)
            {
                MalockSocket malock = null;
                for (int i = 0; i < sockets.Length; i++)
                {
                    MalockSocket current = sockets[i];
                    if (current == null || !current.Available)
                    {
                        continue;
                    }
                    malock = current;
                }
                return malock;
            }
            else
            {
                MalockSocket malock = null;
                if (sockets[0] == socket)
                {
                    malock = sockets[1];
                }
                else
                {
                    malock = sockets[0];
                }
                if (!malock.Available)
                {
                    malock = null;
                }
                return malock;
            }
        }

        public virtual bool Send(byte[] buffer, int ofs, int len)
        {
            IMalockSocket socket = null;
            lock (this.syncobj)
            {
                socket = this.preferred;
            }
            if (socket == null)
            {
                return false;
            }
            return socket.Send(buffer, ofs, len);
        }

        public IMalockSocket GetPreferredSocket()
        {
            return this.preferred;
        }

        protected bool AllIsAvailable()
        {
            bool success = false;
            for (int i = 0; i < sockets.Length; i++)
            {
                MalockSocket socket = sockets[i];
                if (!socket.Available)
                {
                    return false;
                }
                else
                {
                    success = true;
                }
            }
            return success;
        }

        private void UpdateAbortTime(IMalockSocket socket, DateTime dateTime)
        {
            abortedtime.AddOrUpdate(socket, dateTime, (ok, ov) => dateTime);
        }

        private DateTime GetAbortTime(MalockSocket socket)
        {
            DateTime dateTime;
            this.abortedtime.TryGetValue(socket, out dateTime);
            return dateTime;
        }

        private void SocketConnected(object sender, EventArgs e)
        {
            MalockSocket currentsocket = (MalockSocket)sender;
            this.OnConnected(currentsocket);
            do
            {
                TimeSpan ts = TimeSpan.MinValue;
                bool readying = false;
                lock (this.syncobj)
                {
                    ts = unchecked(DateTime.Now - this.firsttime);
                    if (this.preferred == null)
                    {
                        this.preferred = currentsocket;
                    }
                    else if (ts.TotalMilliseconds <= BESTMAXCONNECTTIME)
                    {
                        if (sender == sockets[0])
                        {
                            this.preferred = currentsocket;
                        }
                    }
                    if (!this.IsReady)
                    {
                        if (this.AllIsAvailable() || currentsocket == sockets[0] ||
                            (ts.TotalMilliseconds > BESTMAXCONNECTTIME && this.Available))
                        {
                            readying = true;
                            this.IsReady = true;
                        }
                    }
                    if (currentsocket == sockets[0]) // 它可能只是链接发生中断，但服务器本身是可靠的，所以不需要平滑到备用服务器。
                    {
                        TimeSpan tv = unchecked(DateTime.Now - this.GetAbortTime(currentsocket));
                        if (tv.TotalMilliseconds < Malock.SmoothingInvokeTime)
                        {
                            this.preferred = currentsocket;
                        }
                    }
                    currentsocket = (MalockSocket)this.preferred;
                }
                if (readying)
                {
                    this.OnReady(currentsocket);
                }
            } while (false);
        }

        protected virtual void OnReady(EventArgs e)
        {
            EventHandler evt = this.Ready;
            if (evt != null)
            {
                evt(this, e);
            }
        }

        private void SocketAborted(object sender, EventArgs e)
        {
            MalockSocket currentsocket = (MalockSocket)sender;
            bool aborted = false;
            lock (this.syncobj)
            {
                if (this.preferred == sender)
                {
                    aborted = true;
                    this.preferred = this.Select(this.preferred);
                }
            }
            if (aborted)
            {
                this.OnAborted(currentsocket);
            }
            this.UpdateAbortTime(currentsocket, DateTime.Now);
        }

        void IMalockSocket.Abort()
        {
            for (int i = 0; i < sockets.Length; i++)
            {
                MalockSocket socket = sockets[i];
                socket.Abort();
            }
        }
    }
}
