namespace malock.Server
{
    using malock.Common;
    using MSG = global::malock.Common.MalockNameNodeMessage;
    using NnsClient = global::malock.NN.NnsClient;

    internal class MalockNnsClient : NnsClient
    {
        public MalockNnsClient(string identity, string mainuseNode, string standbyNode) :
            base(identity, mainuseNode, standbyNode)
        {
            base.Run();
        }

        protected override void OnMessage(MalockNetworkMessage<MSG> e)
        {
            base.OnMessage(e);
        }

        protected override int GetLinkMode()
        {
            return MSG.LINK_MODE_SERVER;
        }
    }
}
