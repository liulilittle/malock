namespace malock.Client
{
    using System;
    using System.IO;

    public class MalockNetworkMessage<TMessage> : EventArgs
    {
        public Stream Stream
        {
            get;
            private set;
        }

        public MalockMixClient<TMessage> Client
        {
            get;
            private set;
        }

        public MalockSocket Socket
        {
            get;
            private set;
        }

        public TMessage Message
        {
            get;
            private set;
        }

        internal MalockNetworkMessage(MalockMixClient<TMessage> client, MalockSocket socket, Stream stream, TMessage message)
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
