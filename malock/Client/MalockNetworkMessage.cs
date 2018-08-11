namespace malock.Client
{
    using malock.Common;
    using System;
    using System.IO;

    public class MalockNetworkMessage : EventArgs
    {
        public Stream Stream
        {
            get;
            private set;
        }

        public MalockClient Client
        {
            get;
            private set;
        }

        public MalockSocket Socket
        {
            get;
            private set;
        }

        public Message Message
        {
            get;
            private set;
        }

        public string GetKey()
        {
            return this.Message.Key;
        }

        public string GetIdentity()
        {
            return this.Message.Identity;
        }

        public int GetCurrentSequence()
        {
            return this.Message.Sequence;
        }

        internal MalockNetworkMessage(MalockClient client, MalockSocket socket, Stream stream, Message message)
        {
            if (client == null)
            {
                throw new ArgumentNullException("client");
            }
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
            this.Client = client;
            this.Socket = socket;
            this.Stream = stream;
            this.Message = message;
        }
    }
}
