namespace malock.NN
{
    using global::malock.Common;
    using global::malock.Server;
    using System;
    using System.IO;
    using MalockSocketStream = global::malock.Client.MalockSocketStream;

    internal class NnsStanbyClient : MalockStandbyClient
    {
        private readonly NnsTable nnsTable = null;

        public NnsStanbyClient(NnsTable nnsTable, string identity, string address, int listenport) : base(identity, address, listenport)
        {
            if (nnsTable == null)
            {
                throw new ArgumentNullException("nnsTable");
            }
            this.nnsTable = nnsTable;
        }

        protected override void OnReceived(object sender, MalockSocketStream e)
        {
            MalockNameNodeMessage message = null;
            using (Stream stream = e.Stream)
            {
                if (!MalockNameNodeMessage.TryDeserialize(e.Stream, out message))
                {
                    this.Abort();
                    return;
                }
                if (message.Command == MalockNameNodeMessage.SERVER_NNS_COMMAND_DUMPHOSTENTRYINFO)
                {
                    this.DumpHostEntry(stream);
                }
            }
        }

        private void DumpHostEntry(Stream stream)
        {
            BinaryReader br = new BinaryReader(stream);
            int count = br.ReadInt32();
            lock (this.nnsTable.GetSynchronizationObject())
            {
                for (int i = 0; i < count; i++)
                {
                    NnsTable.Host host;
                    if (!NnsTable.Host.TryDeserialize(br, out host))
                    {
                        break;
                    }
                    this.nnsTable.Register(host.Identity, host.Entry);
                }
            }
        }

        protected override void OnConnected(object sender, EventArgs e)
        {
            MalockNameNodeMessage message = new MalockNameNodeMessage();
            message.Command = MalockNameNodeMessage.SERVER_NNS_COMMAND_DUMPHOSTENTRYINFO;
            message.Sequence = MalockMessage.NewId();
            MalockMessage.TrySendMessage(this, message);
        }
    }
}
