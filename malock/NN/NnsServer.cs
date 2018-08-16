namespace malock.NN
{
    using global::malock.Common;
    using global::malock.Server;
    using System;
    using System.IO;
    using System.Net;

    public class NnsServer
    {
        private MalockSocketListener malockListener = null;
        private NnsTable nnsTable = null;
        private EventHandler onAboredHandler = null;
        private EventHandler onConnectedHandler = null;
        private EventHandler<MalockSocketStream> onReceivedHandler = null;

        public NnsServer(int port, string standbyNode)
        {
            this.malockListener = new MalockSocketListener(port);
            this.nnsTable = new NnsTable();
            do
            {
                this.onReceivedHandler = this.ProcessReceived;
                this.onAboredHandler = this.ProcessAborted;
                this.malockListener.Accept += (sender, e) =>
                {
                    MalockSocket socket = (MalockSocket)e;
                    lock (socket)
                    {
                        socket.Received += this.onReceivedHandler;
                        socket.Aborted += this.onAboredHandler;
                        socket.Connected += this.onConnectedHandler;
                        socket.Run();
                    }
                };
            } while (false);
            this.onConnectedHandler = (sender, e) => this.ProcessAccept(sender, (MalockSocket)sender);
        }

        public void Run()
        {
            this.malockListener.Run();
        }

        public void Close()
        {
            this.malockListener.Close();
        }

        private void ProcessAccept(object sender, MalockSocket e)
        {
            
        }

        private void ProcessReceived(object sender, MalockSocketStream e)
        {
            MalockNameNodeMessage message = null;
            using (Stream stream = e.Stream)
            {
                MalockNameNodeMessage.TryDeserialize(stream, out message);
                if (message != null)
                {
                    this.ProcessMessage(e.Socket, message, stream);
                }
            }
        }

        private void QueryHostEntry(MalockSocket socket, int sequence, string key)
        {
            HostEntry entry = this.nnsTable.GetEntry(key);
            MalockNameNodeMessage message = new MalockNameNodeMessage();
            if (entry == null)
            {
                message.Command = MalockMessage.COMMON_COMMAND_ERROR;
            }
            else
            {
                message.Command = MalockNameNodeMessage.CLIENT_COMMAND_QUERYHOSTENTRYINFO;
            }
            message.Key = key;
            message.Sequence = sequence;
            MalockMessage.TrySendMessage(socket, message);
        }

        private void DumpHostEntry(MalockSocket socket)
        {
            
        }

        private void RegisterHostEntry(MalockSocket socket, int sequence, HostEntry entry)
        {
            MalockNameNodeMessage message = new MalockNameNodeMessage();
            message.Sequence = sequence;
            message.Command = MalockMessage.COMMON_COMMAND_ERROR;
            if (entry != null)
            {
                if (this.nnsTable.Register(socket.Identity, entry))
                {
                    message.Command = MalockNameNodeMessage.SERVER_NDN_COMMAND_REGISTERHOSTENTRYINFO;
                }
            }
            MalockMessage.TrySendMessage(socket, message);
        }

        private void ProcessClient(MalockSocket socket, MalockNameNodeMessage message)
        {
            if (message.Command == MalockNameNodeMessage.CLIENT_COMMAND_QUERYHOSTENTRYINFO)
            {
                this.QueryHostEntry(socket, message.Sequence, message.Key);
            }
            else if (message.Command == MalockNameNodeMessage.CLIENT_COMMAND_DUMPHOSTENTRYINFO)
            {
                this.DumpHostEntry(socket);
            }
        }

        private void ProcessServer(MalockSocket socket, MalockNameNodeMessage message, Stream stream)
        {
            if (message.Command == MalockNameNodeMessage.SERVER_NNS_COMMAND_SYN_HOSTENTRYINFO)
            {

            }
            else if (message.Command == MalockNameNodeMessage.SERVER_NDN_COMMAND_REGISTERHOSTENTRYINFO)
            {
                this.RegisterHostEntry(socket, message.Sequence, HostEntry.Deserialize(stream));
            }
        }

        private void ProcessMessage(MalockSocket socket, MalockNameNodeMessage message, Stream stream)
        {
            switch (socket.LinkMode)
            {
                case MalockMessage.LINK_MODE_CLIENT:
                    this.ProcessClient(socket, message);
                    break;
                case MalockMessage.LINK_MODE_SERVER:
                    this.ProcessServer(socket, message, stream);
                    break;
                default:
                    socket.Abort();
                    break;
            }
        }

        private void ProcessAborted(object sender, EventArgs e)
        {
            MalockSocket socket = (MalockSocket)sender;
            lock (this.nnsTable.GetSynchronizationObject())
            {
                this.nnsTable.SetAvailable(socket.Identity, socket.Address, false);
                if (!this.nnsTable.IsAvailable(socket.Identity))
                {
                    this.nnsTable.Unregister(socket.Identity);
                }
            }
            lock (socket)
            {
                socket.Aborted -= this.onAboredHandler;
                socket.Connected -= this.onConnectedHandler;
                socket.Received -= this.onReceivedHandler;
            }
        }
    }
}
