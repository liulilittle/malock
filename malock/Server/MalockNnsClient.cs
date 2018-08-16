namespace malock.Server
{
    using global::malock.Common;
    using global::malock.NN;
    using malock.Auxiliary;
    using System;
    using System.IO;
    using MSG = global::malock.Common.MalockNameNodeMessage;
    using NnsClient = global::malock.NN.NnsClient;

    internal class MalockNnsClient : NnsClient
    {
        private readonly MalockConfiguration configuration = null;

        internal MalockNnsClient(MalockConfiguration configuration) : 
            base(configuration.NnsId, configuration.NnsNode, configuration.NnsStandbyNode, configuration)
        {
            this.configuration = configuration;
            base.Run();
        }

        protected override int GetListenPort()
        {
            MalockConfiguration configuration = (MalockConfiguration)this.GetStateObject();
            return configuration.Port;
        }

        private bool TryRegisterHostEntryMessage(IMalockSocket malock)
        {
            if (malock == null)
            {
                return false;
            }
            MSG msg = new MSG();
            msg.Command = MSG.SERVER_NDN_COMMAND_REGISTERHOSTENTRYINFO;
            msg.Sequence = MSG.NewId();
            using (MemoryStream ms = new MemoryStream())
            {
                msg.Serialize(ms);
                do
                {
                    HostEntry entry = new HostEntry();
                    entry.Primary.Address = Ipep.ToIpepString(GetEtherAddress(malock), this.configuration.Port);
                    entry.Standby.Address = configuration.StandbyNode;
                    entry.Serialize(ms);
                } while (false);
                return MSG.TrySendMessage(malock, ms);
            }
        }

        protected override void OnMessage(MalockNetworkMessage<MSG> e)
        {
            base.OnMessage(e);
        }

        protected override int GetLinkMode()
        {
            return MSG.LINK_MODE_SERVER;
        }

        protected virtual MalockConfiguration GetConfiguration()
        {
            return this.configuration;
        }

        protected override void OnAborted(EventArgs e)
        {
            base.OnAborted(e);
        }

        protected override void OnConnected(EventArgs e)
        {
            this.TryRegisterHostEntryMessage(e as IMalockSocket);
            base.OnConnected(e);
        }
    }
}
