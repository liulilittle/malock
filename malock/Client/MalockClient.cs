namespace malock.Client
{
    using MSG = global::malock.Common.MalockDataNodeMessage;

    public class MalockClient : MalockMixClient<MSG>
    {
        internal MalockClient(string identity, string mainuseNode, string standbyNode) :
            base(identity, mainuseNode, standbyNode, null)
        {

        }

        public new MalockClient Run()
        {
            return (MalockClient)base.Run();
        }

        protected override int GetLinkMode()
        {
            return MSG.LINK_MODE_CLIENT;
        }

        protected override int GetListenPort()
        {
            return 0;
        }

        protected override bool TryDeserializeMessage(MalockSocketStream stream, out MSG message)
        {
            return MSG.TryDeserialize(stream.Stream, out message);
        }
    }
}
