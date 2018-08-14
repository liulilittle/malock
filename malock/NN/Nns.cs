namespace malock.NN
{
    using global::malock.Client;
    using System;
    using MSG = global::malock.Common.MalockNameNodeMessage;

    public class Nns : MalockMixClient<MSG> 
    {
        public Nns(string identity, string mainuseMachine, string standbyMachine) :
            base(identity, mainuseMachine, standbyMachine)
        {

        }

        public new Nns Run()
        {
            return (Nns)base.Run();
        }

        protected override int GetLinkMode()
        {
            return MSG.LINK_MODE_CLIENT;
        }

        protected override bool TryDeserializeMessage(MalockSocketStream stream, out MSG message)
        {
            return MSG.TryDeserialize(stream.Stream, out message);
        }

        public void QueryHostEntryAsync(string key, Action<NnsError> state)
        {
            this.QueryHostEntryAsync(key, 3000, state);
        }

        public void QueryHostEntryAsync(string key, int timeout, Action<NnsError> state)
        {
            if (state == null)
            {
                throw new ArgumentNullException("state");
            }
            if (key == null)
            {
                throw new ArgumentNullException("key");
            }
            if (key.Length <= 0)
            {
                throw new ArgumentOutOfRangeException("key");
            }
            if (timeout <= 0 && timeout != -1)
            {
                state(NnsError.kTimeout);
            }
            else if (!this.Available)
            {
                state(NnsError.kAborted);
            }
            else
            {
                Exception exception = null;
                if (!MSG.TryInvokeAsync(this, this.NewMessage(key,
                        MSG.CLIENT_COMMAND_QUERYHOSTENTRYINFO), timeout,
                    (errno, message, stream) =>
                {
                    if (errno == MSG.Mappable.ERROR_NOERROR)
                    {
                        state(NnsError.kSuccess);
                    }
                    else if (errno == MSG.Mappable.ERROR_ABORTED)
                    {
                        state(NnsError.kAborted);
                    }
                    else if (errno == MSG.Mappable.ERROR_TIMEOUT)
                    {
                        state(NnsError.kTimeout);
                    }
                }, ref exception))
                {
                    state(NnsError.kAborted);
                }
            }
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
