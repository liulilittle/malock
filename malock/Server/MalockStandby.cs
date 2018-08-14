namespace malock.Server
{
    using global::malock.Common;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using HandleInfo = global::malock.Client.HandleInfo;
    using MalockInnetSocket = global::malock.Client.MalockSocket;
    using MalockInnetSocketStream = global::malock.Client.MalockSocketStream;

    public class MalockStandby : IMalockSocket
    {
        private readonly MalockInnetSocket socket = null;
        private readonly MalockEngine engine = null;

        public object Tag
        {
            get;
            set;
        }

        public bool Available
        {
            get
            {
                var s = this.socket;
                if (s == null)
                {
                    return false;
                }
                return s.Available;
            }
        }

        public MalockStandby(MalockEngine engine, string address)
        {
            if (engine == null)
            {
                throw new ArgumentNullException("engine");
            }
            this.engine = engine;
            this.socket = new MalockInnetSocket(string.Format("standby{0}", Environment.TickCount), address, MalockMessage.LINK_MODE_SERVER);
            this.socket.Received += this.OnReceived;
            this.socket.Run();
        }

        public bool Send(byte[] buffer, int ofs, int len)
        {
            return this.socket.Send(buffer, ofs, len);
        }

        private void LoadAllInfo(Stream stream)
        {
            IList<HandleInfo> s = new List<HandleInfo>();
            if (!HandleInfo.Fill(s, stream))
            {
                this.socket.Abort();
                return;
            }
            MalockTable malock = this.engine.GetTable();
            foreach (HandleInfo i in s)
            {
                if (i.Available)
                {
                    malock.Exit(i.Key);
                }
                else
                {
                    malock.Enter(i.Key, i.Identity);
                }
            }
        }

        private void Exit(MalockDataNodeMessage message)
        {
            MalockTable malock = this.engine.GetTable();
            malock.Exit(message.Key, message.Identity);
        }

        private void Enter(MalockDataNodeMessage message)
        {
            MalockTable malock = this.engine.GetTable();
            malock.Enter(message.Key, message.Identity);
        }

        private void OnReceived(object sender, MalockInnetSocketStream e)
        {
            MalockDataNodeMessage message = null;
            using (Stream stream = e.Stream)
            {
                try
                {
                    message = MalockDataNodeMessage.Deserialize(e.Stream);
                }
                catch (Exception)
                {
                    this.socket.Abort();
                    return;
                }
                if (message != null)
                {
                    if (message.Command == MalockDataNodeMessage.SERVER_COMMAND_SYN_LOADALLINFO)
                    {
                        this.LoadAllInfo(stream);
                    }
                    else if (message.Command == MalockDataNodeMessage.SERVER_COMMAND_SYN_ENTER)
                    {
                        this.Enter(message);
                    }
                    else if (message.Command == MalockDataNodeMessage.SERVER_COMMAND_SYN_EXIT)
                    {
                        this.Exit(message);
                    }
                }
            }
        }

        public void Abort()
        {
            this.socket.Abort();
        }
    }
}
