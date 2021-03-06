﻿namespace malock.Common
{
    using global::malock.Client;
    using System;
    using System.IO;
    using MalockClientSocket = global::malock.Client.MalockSocket;
    using MalockServerSocket = global::malock.Server.MalockSocket;

    public abstract class MalockNetworkMessage : EventArgs
    {
        private readonly IMalockSocket socket = null;

        public Stream Stream
        {
            get;
            private set;
        }

        public MalockMessage Message
        {
            get;
            private set;
        }

        public bool IsClient
        {
            get
            {
                return this.GetClientSocket() != null;
            }
        }

        public bool IsServer
        {
            get
            {
                return this.GetServerSocket() != null;
            }
        }

        public MalockClientSocket GetClientSocket()
        {
            return this.socket as MalockClientSocket;
        }

        public MalockServerSocket GetServerSocket()
        {
            return this.socket as MalockServerSocket;
        }

        public IMalockSocket GetRawSocket()
        {
            return this.socket;
        }

        protected MalockNetworkMessage(IMalockSocket socket, Stream stream, MalockMessage message)
        {
            if (socket == null)
            {
                throw new ArgumentNullException("socket");
            }
            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }
            if (message == null)
            {
                throw new ArgumentNullException("message");
            }
            this.socket = socket;
            this.Stream = stream;
            this.Message = message;
        }
    }

    public sealed class MalockNetworkMessage<TMessage> : MalockNetworkMessage 
        where TMessage : MalockMessage
    {
        public MalockMixClient<TMessage> Client
        {
            get;
            private set;
        }

        public new TMessage Message
        {
            get
            {
                return (TMessage)base.Message;
            }
        }

        internal MalockNetworkMessage(MalockMixClient<TMessage> client, IMalockSocket socket, Stream stream, TMessage message)
            : base(socket, stream, message)
        {
            if (client == null)
            {
                throw new ArgumentNullException("client");
            }
            this.Client = client;
        }
    }
}
