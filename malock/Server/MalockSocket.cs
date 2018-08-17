namespace malock.Server
{
    using global::malock.Auxiliary;
    using global::malock.Common;
    using global::malock.Core;
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using MalockInnetSocket = global::malock.Client.MalockSocket;

    public unsafe class MalockSocket : EventArgs, IMalockSocket
    {
        private readonly object syncobj = new object();
        private readonly Socket socket = null;
        private string address = null;
        private readonly SpinLock connectwait = new SpinLock(); 
        private bool connected = false;
        private string identity = null;
        private int remoteport = 0;
        private EndPoint remoteep = null;
        private MalockSocketAuxiliary auxiliary = null;
        private Func<MemoryStream, bool> socketsendproc = null;
        private static readonly byte[] emptrybufs = new byte[0];

        public event EventHandler Aborted = null;
        public event EventHandler Connected = null;
        public event EventHandler<MalockSocketStream> Received = null;

        internal IPAddress GetRemoteEtherAddress()
        {
            IPEndPoint ep = this.remoteep as IPEndPoint;
            if (ep == null)
            {
                return null;
            }
            return ep.Address;
        }

        public MalockSocket(Socket socket)
        {
            if (socket == null)
            {
                throw new ArgumentNullException("socket");
            }
            this.socket = socket;
            this.socket.NoDelay = true;
            this.remoteep = socket.RemoteEndPoint;
            this.socketsendproc = (ms) => auxiliary.Send(ms.GetBuffer(), 0, unchecked((int)ms.Length));
            this.auxiliary = new MalockSocketAuxiliary(this.syncobj, this.ProcessAborted, this.ProcessReceived);
            this.auxiliary.SocketObject = socket;
        }

        public void Run()
        {
            this.auxiliary.Run();
        }

        public string Address
        {
            get
            {
                return this.address;
            }
        }

        public string Identity
        {
            get
            {
                return this.identity;
            }
        }

        public int LinkMode
        {
            get;
            private set;
        }

        public object Tag
        {
            get;
            set;
        }

        public object UserToken
        {
            get;
            set;
        }

        public int GetRemotePort()
        {
            return this.remoteport;
        }

        public bool Available
        {
            get
            {
                bool localTaken = false;
                this.connectwait.Enter(ref localTaken);
                if (localTaken)
                {
                    try
                    {
                        return this.connected;
                    }
                    finally
                    {
                        this.connectwait.Exit();
                    }
                }
                return false;
            }
        }

        private void ProcessAborted()
        {
            lock (this.syncobj)
            {
                Socket socket = this.socket;
                if (socket != null)
                {
                    MalockInnetSocket.Close(socket);
                }
                this.connected = false;
            }
            this.OnAborted(EventArgs.Empty);
        }

        public void Abort()
        {
            this.ProcessAborted();
        }

        protected virtual void OnAborted(EventArgs e)
        {
            EventHandler evt = this.Aborted;
            if (evt != null)
            {
                evt(this, e);
            }
        }

        protected virtual void OnConnected(EventArgs e)
        {
            EventHandler evt = this.Connected;
            if (evt != null)
            {
                evt(this, e);
            }
        }

        protected virtual void OnReceived(MalockSocketStream e)
        {
            EventHandler<MalockSocketStream> evt = this.Received;
            if (evt != null)
            {
                evt(this, e);
            }
        }

        private void ProcessReceived(MemoryStream stream)
        {
            using (stream)
            {
                if (stream.Position >= stream.Length)
                {
                    return;
                }
                bool debarkation = false;
                do
                {
                    bool localTaken = false;
                    this.connectwait.Enter(ref localTaken);
                    if (localTaken)
                    {
                        debarkation = this.connected;
                        if (!debarkation)
                        {
                            this.connected = true;
                        }
                        this.connectwait.Exit();
                    }
                } while (false);
                if (!debarkation)
                {
                    using (BinaryReader br = new BinaryReader(stream))
                    {
                        IPEndPoint ipep = this.remoteep as IPEndPoint;
                        if (ipep != null)
                        {
                            this.LinkMode = br.ReadByte();
                            int port = br.ReadUInt16();
                            this.identity = MalockMessage.FromStringInReadStream(br);
                            this.remoteport = ipep.Port;
                            this.address = Ipep.ToIpepString(ipep.Address.ToString(), port);
                        }
                    }
                    if (string.IsNullOrEmpty(this.identity))
                    {
                        this.Abort();
                    }
                    else
                    {
                        this.OnConnected(EventArgs.Empty);
                    }
                }
                else if (string.IsNullOrEmpty(this.identity))
                {
                    this.Abort();
                }
                else
                {
                    this.OnReceived(new MalockSocketStream(this, stream));
                }
            }
        }

        public bool Send(byte[] buffer, int ofs, int len)
        {
            return auxiliary.Combine(buffer, ofs, len, this.socketsendproc);
        }
    }
}
