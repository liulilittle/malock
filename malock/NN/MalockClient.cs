namespace malock.NN
{
    using global::malock.Client;
    using global::malock.Common;
    using MSG = global::malock.Common.Message;

    public class MalockClient : MalockMixClient<Message>
    {
        public MalockClient(string identity, string mainuseMachine, string standbyMachine) :
            base(identity, mainuseMachine, standbyMachine)
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

        protected override bool TryDeserializeMessage(MalockSocketStream stream, out MSG message)
        {
            throw new System.NotImplementedException();
        }
    }
}
