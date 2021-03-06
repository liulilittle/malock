﻿namespace malock.Server
{
    using global::malock.Common;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using HandleInfo = global::malock.Client.HandleInfo;
    using MalockInnetSocket = global::malock.Client.MalockSocket;
    using MalockInnetSocketStream = global::malock.Client.MalockSocketStream;

    internal class MalockStandbyClient : IMalockSocket
    {
        private readonly MalockInnetSocket socket = null;
        private readonly MalockEngine engine = null;
        private readonly MalockConfiguration configuration = null;
        private readonly int listenport = 0;

        public object Tag
        {
            get;
            set;
        }

        public string Identity
        {
            get;
            private set;
        }

        public string Address
        {
            get;
            private set;
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

        public MalockStandbyClient(MalockEngine engine, MalockConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException("configuration");
            }
            if (engine == null)
            {
                throw new ArgumentNullException("engine");
            }
            this.configuration = configuration;
            this.engine = engine;
            do
            {
                this.Identity = configuration.Identity;
                this.Address = this.GetAddress(configuration);
                this.listenport = configuration.Port;
            } while (false);
            this.socket = new MalockInnetSocket(this.Identity, this.Address, this.GetListenPort(), MalockMessage.LINK_MODE_SERVER);
            this.socket.Received += this.OnReceived;
            this.socket.Connected += this.OnConnected;
            this.socket.Aborted += this.OnAborted;
            this.socket.Run();
        }

        public MalockStandbyClient(string identity, string address, int listenport)
        {
            this.socket = new MalockInnetSocket(identity, address, listenport, MalockMessage.LINK_MODE_SERVER);
            do
            {
                this.Identity = identity;
                this.Address = address;
                this.listenport = listenport;
            } while (false);
            this.socket.Received += this.OnReceived;
            this.socket.Connected += this.OnConnected;
            this.socket.Aborted += this.OnAborted;
            this.socket.Run();
        }

        protected virtual void OnAborted(object sender, EventArgs e)
        {
            /*
             *
             */
        }

        protected virtual void OnConnected(object sender, EventArgs e)
        {
            MalockMessage message = MalockNodeMessage.New(null, this.Identity, MalockNodeMessage.SERVER_COMMAND_SYN_LOADALLINFO, -1);
            MalockMessage.TrySendMessage(this, message);
        }

        public MalockStandbyClient(MalockEngine engine, string identity, string address, int listenport)
        {
            if (engine == null)
            {
                throw new ArgumentNullException("engine");
            }
            this.engine = engine;
            do
            {
                this.Identity = identity;
                this.Address = address;
                this.listenport = listenport;
            } while (false);
            this.socket = new MalockInnetSocket(identity, address, listenport, MalockMessage.LINK_MODE_SERVER);
            this.socket.Received += this.OnReceived;
            this.socket.Run();
        }

        protected virtual IMalockSocket GetInnerSocket()
        {
            return this.socket;
        }

        protected virtual string GetAddress(MalockConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException("configuration");
            }
            return configuration.StandbyNode;
        }

        protected virtual int GetListenPort()
        {
            return this.listenport;
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
            lock (malock.GetSynchronizationObject())
            {
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
        }

        private void Exit(MalockNodeMessage message)
        {
            MalockTable malock = this.engine.GetTable();
            malock.Exit(message.Key, message.Identity);
        }

        private void Enter(MalockNodeMessage message)
        {
            MalockTable malock = this.engine.GetTable();
            malock.Enter(message.Key, message.Identity);
        }

        protected virtual void OnReceived(object sender, MalockInnetSocketStream e)
        {
            MalockNodeMessage message = null;
            using (Stream stream = e.Stream)
            {
                if (!MalockNodeMessage.TryDeserialize(e.Stream, out message))
                {
                    this.Abort();
                    return;
                }
                if (message.Command == MalockNodeMessage.SERVER_COMMAND_SYN_LOADALLINFO)
                {
                    this.LoadAllInfo(stream);
                }
                else if (message.Command == MalockNodeMessage.SERVER_COMMAND_SYN_ENTER)
                {
                    this.Enter(message);
                }
                else if (message.Command == MalockNodeMessage.SERVER_COMMAND_SYN_EXIT)
                {
                    this.Exit(message);
                }
            }
        }

        public void Abort()
        {
            this.socket.Abort();
        }
    }
}
