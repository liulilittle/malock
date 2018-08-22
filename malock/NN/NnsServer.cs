namespace malock.NN
{
    using global::malock.Common;
    using global::malock.Server;
    using System;
    using System.IO;

    public unsafe sealed class NnsServer
    {
        private MalockSocketListener malockListener = null;
        private NnsTable nnsTable = null;
        private EventHandler onAboredHandler = null;
        private EventHandler onConnectedHandler = null;
        private EventHandler<MalockSocketStream> onReceivedHandler = null;
        private NnsStanbyClient nnsStanbyClient = null;

        public NnsServer(string identity, int port, string standbyNode)
        {
            if (port <= 0 || port > short.MaxValue)
            {
                throw new ArgumentOutOfRangeException("The specified server listening port is outside the 0~65535 range");
            }
            if (string.IsNullOrEmpty(standbyNode))
            {
                throw new ArgumentOutOfRangeException("You have specified an invalid standby server host address that is not allowed to be null or empty");
            }
            if (string.IsNullOrEmpty(identity))
            {
                throw new ArgumentOutOfRangeException("You have specified an invalid node Identity");
            }
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
            this.nnsStanbyClient = new NnsStanbyClient(this.nnsTable, identity, standbyNode, port);
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
            /*
             *  
             */
        }

        private void ProcessReceived(object sender, MalockSocketStream e)
        {
            MalockNnsMessage message = null;
            using (Stream stream = e.Stream)
            {
                MalockNnsMessage.TryDeserialize(stream, out message);
                if (message != null)
                {
                    this.ProcessMessage(e.Socket, message, stream);
                }
            }
        }

        private bool AbortHostEntry(string identity, string address)
        {
            if (string.IsNullOrEmpty(identity) || string.IsNullOrEmpty(address))
            {
                return false;
            }
            lock (this.nnsTable.GetSynchronizationObject())
            {
                if (!this.nnsTable.ContainsIdentity(identity))
                {
                    return false;
                }
                this.nnsTable.SetAvailable(identity, address, false);
                if (!this.nnsTable.IsAvailable(identity))
                {
                    this.nnsTable.Unregister(identity);
                }
                return true;
            }
        }

        private void QueryHostEntry(MalockSocket socket, int sequence, string key)
        {
            string identity;
            HostEntry entry = this.nnsTable.GetEntry(key, out identity);
            MalockNnsMessage message = new MalockNnsMessage();
            message.Key = key;
            message.Sequence = sequence;
            message.Command = MalockMessage.COMMON_COMMAND_ERROR;
            if (entry != null)
            {
                message.Command = MalockNnsMessage.CLIENT_COMMAND_QUERYHOSTENTRYINFO;
            }
            using (MemoryStream stream = new MemoryStream())
            {
                using (BinaryWriter bw = new BinaryWriter(stream))
                {
                    message.Serialize(bw);
                    if (entry != null)
                    {
                        entry.Serialize(bw);
                    }
                    MalockMessage.TrySendMessage(socket, stream);
                }
            }
            this.PostSynHostEntryMessage(this.nnsStanbyClient, key, identity, entry);
        }

        private bool PostSynHostEntryMessage(IMalockSocket socket, string key, string identity, HostEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(identity) || string.IsNullOrEmpty(key))
            {
                return false;
            }
            MalockNnsMessage message = new MalockNnsMessage();
            message.Key = key;
            message.Identity = identity;
            message.Sequence = MalockMessage.NewId();
            message.Command = MalockNnsMessage.SERVER_NNS_COMMAND_SYN_HOSTENTRYINFO;
            using (MemoryStream stream = new MemoryStream())
            {
                using (BinaryWriter bw = new BinaryWriter(stream))
                {
                    message.Serialize(bw);
                    if (entry != null)
                    {
                        entry.Serialize(bw);
                    }
                    MalockMessage.TrySendMessage(socket, stream);
                }
            }
            return true;
        }

        private void DumpHostEntry(MalockSocket socket, MalockNnsMessage message)
        {
            MalockNnsMessage msg = new MalockNnsMessage();
            msg.Sequence = message.Sequence;
            msg.Command = message.Command;
            do
            {
                var hosts = this.nnsTable.GetAllHosts();
                lock (this.nnsTable)
                {
                    NnsTable.Host.SerializeAll(hosts, (count, stream) =>
                    {
                        using (MemoryStream ms = (MemoryStream)stream)
                        {
                            MalockMessage.TrySendMessage(socket, message, ms.GetBuffer(), 0, unchecked((int)ms.Position));
                        }
                    });
                }
            } while (false);
        }

        private void RegisterHostEntry(MalockSocket socket, int sequence, HostEntry entry)
        {
            MalockNnsMessage message = new MalockNnsMessage();
            message.Sequence = sequence;
            message.Command = MalockMessage.COMMON_COMMAND_ERROR;
            if (entry != null)
            {
                lock (this.nnsTable.GetSynchronizationObject())
                {
                    if (this.nnsTable.Register(socket.Identity, entry))
                    {
                        message.Command = MalockNnsMessage.SERVER_NDN_COMMAND_REGISTERHOSTENTRYINFO;
                    }
                    this.nnsTable.SetAvailable(socket.Identity, socket.Address, true);
                }
            }
            MalockMessage.TrySendMessage(socket, message);
        }

        private void ProcessClient(MalockSocket socket, MalockNnsMessage message)
        {
            if (message.Command == MalockNnsMessage.CLIENT_COMMAND_QUERYHOSTENTRYINFO)
            {
                this.QueryHostEntry(socket, message.Sequence, message.Key);
            }
            else if (message.Command == MalockNnsMessage.CLIENT_COMMAND_DUMPHOSTENTRYINFO)
            {
                this.DumpHostEntry(socket, message);
            }
        }

        private void SynQueryHostEntry(MalockSocket socket, string identity, string key, HostEntry entry)
        {
            if (entry == null || socket == null || string.IsNullOrEmpty(identity) || string.IsNullOrEmpty(key))
            {
                return;
            }
            lock (this.nnsTable.GetSynchronizationObject())
            {
                this.nnsTable.SetEntry(identity, key, entry);
            }
        }

        private void ProcessServer(MalockSocket socket, MalockNnsMessage message, Stream stream)
        {
            if (message.Command == MalockNnsMessage.SERVER_NNS_COMMAND_SYN_HOSTENTRYINFO)
            {
                this.SynQueryHostEntry(socket, message.Identity, message.Key, HostEntry.Deserialize(stream));
            }
            else if (message.Command == MalockNnsMessage.SERVER_NDN_COMMAND_REGISTERHOSTENTRYINFO)
            {
                this.RegisterHostEntry(socket, message.Sequence, HostEntry.Deserialize(stream));
            }
            else if (message.Command == MalockNnsMessage.SERVER_NNS_COMMAND_DUMPHOSTENTRYINFO)
            {
                this.DumpHostEntry(socket, message);
            }
        }

        private void ProcessMessage(MalockSocket socket, MalockNnsMessage message, Stream stream)
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
            if (socket.LinkMode == MalockMessage.LINK_MODE_SERVER)
            {
                this.AbortHostEntry(socket.Identity, socket.Address);
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
