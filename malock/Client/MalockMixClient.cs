namespace malock.Client
{
    using global::malock.Core;
    using System;

    public abstract class MalockMixClient<TMessage> : EventArgs
    {
        private MalockSocket[] sockets = new MalockSocket[2];
        private MalockSocket preferred = null; // 首选服务器索引
        private DateTime firsttime = DateTime.MinValue;
        private readonly object syncobj = new object();
        private readonly MixEvent<EventHandler<MalockNetworkMessage<TMessage>>> messageevents = new MixEvent<EventHandler<MalockNetworkMessage<TMessage>>>();
        private readonly MixEvent<EventHandler> abortedevents = new MixEvent<EventHandler>();

        private const int BESTMAXCONNECTTIME = 1000;

        public virtual event EventHandler<MalockNetworkMessage<TMessage>> Message
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
        /// 创建一个双机热备的 malock 客户端
        /// </summary>
        /// <param name="identity">代表客户端唯一的身份标识</param>
        /// <param name="mainuseMachine">主用服务器主机地址</param>
        /// <param name="standbyMachine">备用服务器主机地址</param>
        public MalockMixClient(string identity, string mainuseMachine, string standbyMachine)
        {
            this.Identity = identity;
            sockets[0] = new MalockSocket(identity, mainuseMachine, this.GetLinkMode());
            sockets[1] = new MalockSocket(identity, standbyMachine, this.GetLinkMode());
            for (int i = 0; i < sockets.Length; i++)
            {
                MalockSocket socket = sockets[i];
                socket.Aborted += this.SocketAborted;
                socket.Connected += this.SocketConnected;
                socket.Received += this.SocketReceived;
            }
        }

        protected abstract int GetLinkMode();

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
                                    socket = this.preferred;
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

        protected abstract bool TryDeserializeMessage(MalockSocketStream stream, out TMessage message);

        protected virtual void OnAborted(EventArgs e)
        {
            this.abortedevents.Invoke((evt) => evt(this, e));
        }

        protected virtual void OnMessage(MalockNetworkMessage<TMessage> e)
        {
            this.messageevents.Invoke((evt) => evt(this, e));
        }

        protected virtual MalockSocket Select(MalockSocket socket)
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
            MalockSocket socket = null;
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

        public MalockSocket GetPreferredSocket()
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

        private void SocketConnected(object sender, EventArgs e)
        {
            MalockSocket currentsocket = null;
            TimeSpan ts = TimeSpan.MinValue;
            bool readying = false;
            lock (this.syncobj)
            {
                ts = unchecked(DateTime.Now - this.firsttime);
                currentsocket = (MalockSocket)sender;
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
                    if (this.AllIsAvailable() || (ts.TotalMilliseconds > BESTMAXCONNECTTIME && this.Available))
                    {
                        readying = true;
                        this.IsReady = true;
                    }
                }
                currentsocket = this.preferred;
                Console.Title = "preferred->" + this.preferred.Address.ToString();
            }
            if (readying)
            {
                this.OnReady(currentsocket);
            }
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
                if (this.preferred != null)
                {
                    Console.Title = "preferred->" + this.preferred.Address.ToString();
                }
            }
            if (aborted)
            {
                this.OnAborted(currentsocket);
            }
        }
    }
}
