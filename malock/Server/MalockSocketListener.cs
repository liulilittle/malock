namespace malock.Server
{
    using System;
    using System.Net;
    using System.Net.Sockets;
    using MalockInnetSocket = global::malock.Client.MalockSocket;

    public class MalockSocketListener
    {
        private Socket socket = null;

        public event EventHandler<MalockSocket> Accept = null;

        public MalockSocketListener(int port)
        {
            this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            this.socket.NoDelay = true;
            this.socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            this.socket.Bind(new IPEndPoint(IPAddress.Any, port));
            this.socket.Listen(short.MaxValue);
        }

        public virtual void Close()
        {
            lock (this)
            {
                Socket socket = this.socket;
                if (socket != null)
                {
                    MalockInnetSocket.Close(socket);
                }
                this.socket = default(Socket);
            }
        }

        public void Run()
        {
            lock (this)
            {
                this.StartAccept(null);
            }
        }

        private void StartAccept(SocketAsyncEventArgs e)
        {
            Socket server = null;
            lock (this)
            {
                server = this.socket;
            }
            if (server == null)
            {
                return;
            }
            if (e == null)
            {
                e = new SocketAsyncEventArgs();
                e.Completed += this.ProcessAccept;
            }
            try
            {
                if (!server.AcceptAsync(e))
                {
                    this.ProcessAccept(server, e);
                }
            }
            catch (Exception)
            {
                this.Close();
            }
        }

        private void ProcessAccept(object sender, SocketAsyncEventArgs e)
        {
            Socket socket = e.AcceptSocket;
            e.AcceptSocket = null;
            if (socket != null)
            {
                this.OnAccept(new MalockSocket(socket));
            }
            this.StartAccept(e); 
        }

        protected void OnAccept(MalockSocket e)
        {
            EventHandler<MalockSocket> evt = this.Accept;
            if (evt != null)
            {
                evt(this, e);
            }
        }
    }
}
