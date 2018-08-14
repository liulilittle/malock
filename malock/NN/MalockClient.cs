namespace malock.NN
{
    using global::malock.Client;
    using MSG = global::malock.Common.MalockNameNodeMessage;
    using global::malock.Common;

    public class MalockClient : MalockMixClient<MSG>
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
            return MSG.TryDeserialize(stream.Stream, out message);
        }

        private void QueryKeyInfoAsync(string key) 
        {
            this.NewMessage(key, MSG.CLIENT_COMMAND_QUERYKEYINFO);
        }

        private MSG NewMessage(string key, byte command)
        {
            MSG message = new MSG();
            message.Command = command;
            message.Sequence = MSG.NewId();
            message.Key = key;
            return message;
        }
    }
}
