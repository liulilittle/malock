namespace malock.Client
{
    using System;
    using System.IO;

    public class MalockSocketStream : EventArgs
    {
        public Stream Stream
        {
            get;
            private set;
        }

        public MalockSocket Socket
        {
            get;
            private set;
        }

        internal MalockSocketStream(MalockSocket socket, Stream stream)
        {
            if (socket == null)
            {
                throw new ArgumentNullException("socket");
            }
            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }
            this.Stream = stream;
            this.Socket = socket;
        }
    }
}
