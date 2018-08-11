namespace malock.Client
{
    using malock.Common;
    using System;

    public class EventWaitHandlePoll
    {
        public event EventHandler<Message> Message = null;
    }
}
