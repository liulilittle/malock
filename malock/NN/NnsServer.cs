﻿namespace malock.NN
{
    using global::malock.Common;
    using global::malock.Server;
    using System;
    using System.IO;
    using System.Collections.Concurrent;

    public class NnsServer
    {
        private MalockSocketListener malockListener = null;
        private EventHandler onAboredHandler = null;
        private EventHandler onConnectedHandler = null;
        private EventHandler<MalockSocketStream> onReceivedHandler = null;
        private ConcurrentDictionary<string, HostEntry> malockEntryInfos = null;

        public NnsServer(int port, string standbyMachine)
        {
            this.malockListener = new MalockSocketListener(port);
            this.malockEntryInfos = new ConcurrentDictionary<string, HostEntry>();
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
            }
            if (message != null)
            {
                this.ProcessMessage(e.Socket, message);
            }
        }

        private void QueryHostEntry(MalockSocket socket, string key)
        {
            HostEntry entry = null;
            if (!this.malockEntryInfos.TryGetValue(key, out entry))
            {
                entry = new HostEntry();
            }

        }

        private void DumpHostEntry(MalockSocket socket)
        {

        }

        private void ProcessClient(MalockSocket socket, MalockNameNodeMessage message)
        {
            if (message.Command == MalockNameNodeMessage.CLIENT_COMMAND_QUERYHOSTENTRYINFO)
            {
                this.QueryHostEntry(socket, message.Key);
            }
            else if (message.Command == MalockNameNodeMessage.CLIENT_COMMAND_DUMPHOSTENTRYINFO)
            {
                this.DumpHostEntry(socket);
            }
        }

        private void ProcessServer(MalockSocket socket, MalockNameNodeMessage message)
        {
            if (message.Command == MalockNameNodeMessage.SERVER_COMMAND_SYN_HOSTENTRYINFO)
            {

            }
        }

        private void ProcessMessage(MalockSocket socket, MalockNameNodeMessage message)
        {
            switch (socket.LinkMode)
            {
                case MalockMessage.LINK_MODE_CLIENT:
                    this.ProcessClient(socket, message);
                    break;
                case MalockMessage.LINK_MODE_SERVER:
                    this.ProcessServer(socket, message);
                    break;
                default:
                    socket.Abort();
                    break;
            }
        }

        private void ProcessAborted(object sender, EventArgs e)
        {
            MalockSocket socket = (MalockSocket)sender;
            lock (socket)
            {
                socket.Aborted -= this.onAboredHandler;
                socket.Connected -= this.onConnectedHandler;
                socket.Received -= this.onReceivedHandler;
            }
        }
    }
}
