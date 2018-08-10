namespace malock.Auxiliary
{
    using System;
    using System.IO;
    using System.Net.Sockets;
    using System.Runtime.InteropServices;
    using System.Text;

    internal unsafe class MalockSocketAuxiliary
    {
        public const int FKEY = 0x2E;
        public const int MSS = 1400;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1, Size = 5)]
        private struct MalockPacketHeader
        {
            public byte header_key;
            public int payload_len;
        }

        private readonly AsyncCallback receivecallback;

        private object syncobj = null;
        private int rofs = 0;
        private Socket socket = null;
        private int rlen = sizeof(MalockPacketHeader);
        private byte[] rbuf = new byte[MSS];
        private MemoryStream rstream = null;

        public Socket SocketObject
        {
            get
            {
                return this.socket;
            }
            set
            {
                this.socket = value;
            }
        }

        private Action error = null;
        private Action<MemoryStream> receive = null;

        public MalockSocketAuxiliary(object obj, Action error, Action<MemoryStream> receive)
        {
            this.receivecallback = this.ProcessReceive;
            this.error = error;
            this.receive = receive;
            this.syncobj = obj;
        }

        public void Run()
        {
            this.ProcessReceive(null);
        }

        private void ProcessReceive(IAsyncResult ar)
        {
            Socket socket = null;
            lock (this.syncobj)
            {
                socket = this.SocketObject;
            }
            if (socket != null)
            {
                if (ar == null)
                {
                    int surplus = this.rlen - this.rofs;
                    if (surplus <= 0)
                    {
                        this.error();
                    }
                    else
                    {
                        if (surplus > this.rbuf.Length)
                        {
                            surplus = this.rbuf.Length;
                        }
                        try
                        {
                            int ofs = this.rofs;
                            if (this.rstream != null)
                            {
                                ofs = 0;
                            }
                            if (!socket.Connected)
                            {
                                this.error();
                                return;
                            }
                            socket.BeginReceive(this.rbuf, ofs, surplus, SocketFlags.None, this.receivecallback, null);
                        }
                        catch (Exception)
                        {
                            this.error();
                        }
                    }
                }
                else
                {
                    try
                    {
                        SocketError error = SocketError.SocketError;
                        if (!socket.Connected)
                        {
                            this.error();
                            return;
                        }
                        int len = socket.EndReceive(ar, out error);
                        if (len <= 0)
                        {
                            this.error();
                        }
                        else
                        {
                            do
                            {
                                bool alreadyrecv = false;
                                if (this.rstream != null)
                                {
                                    this.rstream.Write(this.rbuf, 0, len);
                                }
                                this.rofs += len;
                                if (this.rofs == this.rlen)
                                {
                                    if (this.rstream == null)
                                    {
                                        fixed (byte* pinned = this.rbuf)
                                        {
                                            MalockPacketHeader* packet = (MalockPacketHeader*)pinned;
                                            if (packet->header_key != FKEY)
                                            {
                                                this.error();
                                                break;
                                            }
                                            this.rofs = 0;
                                            this.rlen = packet->payload_len;
                                            this.rstream = new MemoryStream(packet->payload_len);
                                            if (packet->payload_len <= 0)
                                            {
                                                alreadyrecv = true;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        alreadyrecv = true;
                                    }
                                }
                                if (alreadyrecv)
                                {
                                    MemoryStream ms = this.rstream;
                                    this.rstream = null;
                                    this.rofs = 0;
                                    this.rlen = sizeof(MalockPacketHeader);
                                    if (ms.Length > 0)
                                    {
                                        if (ms.Position != 0)
                                        {
                                            ms.Position = 0;
                                        }
                                        ms.Seek(0, SeekOrigin.Begin);
                                    }
                                    this.receive(ms);
                                }
                                this.receivecallback(null);
                            } while (false);
                        }
                    }
                    catch (Exception)
                    {
                        this.error();
                    }
                }
            }
        }

        public bool Send(byte[] buffer, int ofs, int len)
        {
            bool success = true;
            try
            {
                SocketError error = SocketError.SocketError;
                Socket socket = this.SocketObject;
                if (socket == null)
                {
                    success = false;
                    this.error();
                }
                else
                {
                    socket.BeginSend(buffer, ofs, len, SocketFlags.None, out error, null, null);
                    if (error != SocketError.Success)
                    {
                        success = false;
                        this.error();
                    }
                } 
            }
            catch (Exception)
            {
                success = false;
                this.error();
            }
            return success;
        }

        public bool Combine(byte[] buffer, int ofs, int len, Func<MemoryStream, bool> callback)
        {
            Socket socket = this.SocketObject;
            if (socket == null)
            {
                return false;
            }
            using (MemoryStream ms = new MemoryStream())
            {
                byte[] header = new byte[sizeof(MalockPacketHeader)];
                fixed (byte* pinned = header)
                {
                    MalockPacketHeader* packet = (MalockPacketHeader*)pinned;
                    packet->header_key = FKEY;
                    packet->payload_len = len;
                }
                ms.Write(header, 0, header.Length);
                ms.Write(buffer, ofs, len);
                callback(ms);
                return true;
            }
        }
    }
}
