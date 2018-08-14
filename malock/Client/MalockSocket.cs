namespace malock.Client
{
    using global::malock.Auxiliary;
    using global::malock.Common;
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Runtime.InteropServices;
    using System.Text;
    using Interlocked = System.Threading.Interlocked;

    public unsafe class MalockSocket : EventArgs, IMalockSocket
    {
        private readonly EndPoint address = null;
        private SocketWorkContext context = null;
        private readonly string identity = null;
        private volatile int connected = 0;

        [DllImport("ws2_32.dll", SetLastError = true)]
        private static extern SocketError shutdown([In] IntPtr socketHandle, [In] SocketShutdown how);

        public static void Close(Socket socket)
        {
            if (socket != null)
            {
                PlatformID platform = Environment.OSVersion.Platform;
                if (platform == PlatformID.Win32NT)
                {
                    shutdown(socket.Handle, SocketShutdown.Both);
                }
                else
                {
                    try
                    {
                        socket.Shutdown(SocketShutdown.Both);
                    }
                    catch (Exception) { }
                }
                socket.Close();
            }
        }

        public static bool LAN(EndPoint ep)
        {
            if (ep == null)
            {
                throw new ArgumentNullException("You may not provide a null endpoint");
            }
            IPEndPoint ipep = ep as IPEndPoint;
            if (ipep == null)
            {
                throw new ArgumentOutOfRangeException("The endpoint you offer is not a ipep");
            }
            return LAN(ipep.Address);
        }

        public static bool LAN(IPAddress address)
        {
            if (address == null)
            {
                throw new ArgumentNullException("Need to provide test address not allowed null");
            }
            if (address.AddressFamily != AddressFamily.InterNetwork)
            {
                throw new NotSupportedException("Only IPV4 segment address can be provided to test");
            }
            byte[] buffer = address.GetAddressBytes();
            if (buffer[0] == 10) // A: 10.0.0.0-10.255.255.255
            {
                return true;
            }
            if (buffer[0] == 172 && buffer[1] >= 16 && buffer[1] <= 31) // B: 172.16.0.0-172.31.255.255 
            {
                return true;
            }
            if (buffer[0] == 192 && buffer[1] == 168) // C: 192.168.0.0-192.168.255.255
            {
                return true;
            }
            if (buffer[0] == 127) // L: 127.0.0.0-127.255.255.255
            {
                return true;
            }
            return false;
        }

        private class SocketWorkContext
        {
            private readonly MalockSocket malock = null;
            private Socket socket = null;
            private readonly AsyncCallback connectcallback = null;
            private readonly object syncobj = new object();
            private bool currentconnected = false;
            private byte[] identitybuf = null;
            private MalockSocketAuxiliary auxiliary = null;
            private static readonly byte[] emptybufs = new byte[0];

            public SocketWorkContext(MalockSocket malock)
            {
                if (malock == null)
                {
                    throw new ArgumentNullException("malock");
                }
                this.malock = malock;
                using (MemoryStream ms = new MemoryStream())
                {
                    ms.WriteByte(Convert.ToByte(malock.LinkMode));
                    if (!string.IsNullOrEmpty(malock.identity))
                    {
                        this.identitybuf = Encoding.UTF8.GetBytes(malock.identity);
                        ms.Write(this.identitybuf, 0, this.identitybuf.Length);
                        this.identitybuf = ms.ToArray();
                    }
                }
                this.auxiliary = new MalockSocketAuxiliary(this.syncobj, this.OnError, this.OnReceive);
                this.connectcallback = this.StartConnect;
            }

            private void ProcessConnect(object sender, SocketAsyncEventArgs e)
            {
                lock (this.syncobj)
                {
                    if (e == null)
                    {
                        e = new SocketAsyncEventArgs();
                        e.AcceptSocket = this.socket;
                        e.RemoteEndPoint = malock.address;
                        e.Completed += this.ProcessConnect;
                    }
                    if (sender == null)
                    {
                        this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        this.socket.NoDelay = true;
                        if (!socket.ConnectAsync(e))
                        {
                            this.ProcessConnect(socket, e);
                        }
                    }
                    else
                    {
                        try
                        {
                            if (e.SocketError != SocketError.Success)
                            {
                                this.OnError();
                            }
                            else
                            {
                                this.OnConnected();
                            }
                        }
                        finally
                        {
                            e.Dispose();
                        }
                    }
                }
            }

            private void StartConnect(IAsyncResult ar)
            {
                lock (this.syncobj)
                {
                    if (this.socket != null)
                    {
                        return;
                    }
                    this.ProcessConnect(null, null);
                }
            }

            private void OnReceive(MemoryStream stream)
            {
                using (stream)
                {
                    if (stream.Position < stream.Length)
                    {
                        malock.OnReceive(new MalockSocketStream(malock, stream));
                    }
                }
            }

            private void SendDebarkationBuffer()
            {
                this.Send(identitybuf, 0, identitybuf.Length);
            }

            private void OnConnected()
            {
                this.auxiliary.SocketObject = this.socket;
                lock (this.syncobj)
                {
                    this.currentconnected = true;
                    this.SendDebarkationBuffer();
                }
                this.malock.OnConnected(EventArgs.Empty);
                this.auxiliary.Run();
            }

            private void OnDisconnected()
            {
                this.malock.OnAborted(EventArgs.Empty);
            }

            private void OnError()
            {
                this.InternalAbort(true);
                lock (this.syncobj)
                {
                    var delayconnecttmr = Malock.NewTimer();
                    delayconnecttmr.Interval = Malock.ReconnectionTime;
                    delayconnecttmr.Tick += (sender, e) =>
                    {
                        lock (this.syncobj)
                        {
                            delayconnecttmr.Close();
                            this.connectcallback(null);
                        }
                    };
                    delayconnecttmr.Start();
                }
            }

            public void Abort()
            {
                this.OnError();
            }

            public void Close()
            {
                this.InternalAbort(false);
            }

            private void InternalAbort(bool notifyEvent)
            {
                bool lastisconnected = false;
                lock (this.syncobj)
                {
                    lastisconnected = this.currentconnected;
                    this.currentconnected = false;
                    if (this.socket != null)
                    {
                        MalockSocket.Close(socket);
                    }
                    this.socket = null;
                }
                if (notifyEvent && lastisconnected)
                {
                    this.OnDisconnected();
                }
            }

            public void Run()
            {
                this.connectcallback(null);
            }

            public bool Send(byte[] buffer, int ofs, int len)
            {
                return this.auxiliary.Combine(buffer, ofs, len, (ms) =>
                {
                    lock (this.syncobj)
                    {
                        if (!this.currentconnected)
                        {
                            return false;
                        }
                        else
                        {
                            return this.auxiliary.Send(ms.GetBuffer(), 0, unchecked((int)ms.Position));
                        }
                    }
                });
            }
        }

        internal MalockSocket(string identity, string address) : this(identity, address, MalockMessage.LINK_MODE_CLIENT)
        {

        }

        internal MalockSocket(string identity, string address, int linkMode) : this(identity, Ipep.ToIpep(address), linkMode)
        {

        }

        internal MalockSocket(string identity, EndPoint address) : this(identity, address, MalockMessage.LINK_MODE_CLIENT)
        {

        }

        internal MalockSocket(string identity, EndPoint address, int linkMode)
        {
            if (address == null)
            {
                throw new ArgumentNullException("The address of the connection may not be null");
            }
            if (string.IsNullOrEmpty(identity))
            {
                throw new ArgumentNullException("The identity is absolutely not nullable and it must be specified that each link is unique before you can");
            }
            if (!MalockSocket.LAN(address))
            {
                throw new ArgumentOutOfRangeException("Malock does not accept IPv4 addresses for not ABC class LAN segments");
            }
            this.identity = identity;
            this.address = address;
            this.LinkMode = linkMode;
        }

        public int LinkMode
        {
            get;
            private set;
        }

        public string Identity
        {
            get
            {
                return this.identity;
            }
        }

        public EndPoint Address
        {
            get
            {
                return this.address;
            }
        }

        public bool Available
        {
            get
            {
                return Interlocked.CompareExchange(ref this.connected, 0, 0) == 1;
            }
        }

        public object Tag
        {
            get;
            set;
        }

        public virtual event EventHandler<MalockSocketStream> Received;
        public virtual event EventHandler Aborted;
        public virtual event EventHandler Connected;

        public virtual void Run()
        {
            Exception exception = null;
            lock (this)
            {
                SocketWorkContext context = this.context;
                if (context != null)
                {
                    exception = new InvalidOperationException("The current state does not allow duplicate start job tasks");
                }
                else
                {
                    context = new SocketWorkContext(this);
                    this.context = context;
                }
                if (context != null)
                {
                    context.Run();
                }
            }
            if (exception != null)
            {
                throw exception;
            }
        }

        public virtual void Stop()
        {
            lock (this)
            {
                SocketWorkContext context = this.context;
                if (context != null)
                {
                    context.Close();
                }
                this.context = null;
            }
        }

        public virtual void Abort()
        {
            SocketWorkContext context = this.context;
            if (context != null)
            {
                context.Abort();
            }
        }

        protected virtual void OnAborted(EventArgs e)
        {
            Interlocked.CompareExchange(ref this.connected, 0, 1);
            EventHandler evt = this.Aborted;
            if (evt != null)
            {
                evt(this, e);
            }
        }

        protected virtual void OnConnected(EventArgs e)
        {
            Interlocked.CompareExchange(ref this.connected, 1, 0);
            EventHandler evt = this.Connected;
            if (evt != null)
            {
                evt(this, e);
            }
        }

        protected virtual void OnReceive(MalockSocketStream e)
        {
            if (e == null)
            {
                throw new ArgumentNullException("e");
            }
            EventHandler<MalockSocketStream> evt = this.Received;
            if (evt != null)
            {
                evt(this, e);
            }
        }

        public virtual bool Send(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException("Input stream should not be null");
            }
            MemoryStream ms = stream as MemoryStream;
            if (ms != null)
            {
                return this.Send(ms.GetBuffer(), 0, unchecked((int)ms.Length));
            }
            else
            {
                if (stream.Length > int.MaxValue)
                {
                    throw new ArgumentOutOfRangeException("The size of the flow you have entered is beyond the maximum range");
                }
                byte[] buffer = new byte[unchecked((int)stream.Length)];
                int len = 0;
                int count = 0;
                while ((len = stream.Read(buffer, 0, buffer.Length)) > 0) count += len;
                return this.Send(buffer, 0, count);
            }
        }

        public virtual bool Send(byte[] buffer, int ofs, int len)
        {
            Exception exception = null;
            SocketWorkContext context = null;
            lock (this)
            {
                context = this.context;
            }
            if (context == null)
            {
                exception = new InvalidOperationException("The job context does not exist and this operation is not allowed"); ;
            }
            else
            {
                return context.Send(buffer, ofs, len);
            }
            if (exception == null)
            {
                return false;
            }
            else
            {
                throw exception;
            }
        }
    }
}
