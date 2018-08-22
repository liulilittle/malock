namespace malock.Client
{
    using malock.Common;
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using MSG = global::malock.Common.MalockNodeMessage;

    public class MalockClient : MalockMixClient<MSG>
    {
        private static readonly HandleInfo[] emptryhandleinfos = new HandleInfo[0];

        public const int kERROR_NOERROR = 0;
        public const int kERROR_ABORTED = 1;
        public const int kERROR_TIMEOUT = 2;
        public const int kERROR_ERRORNO = 3;

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

        protected internal virtual IEnumerable<HandleInfo> GetAllInfo()
        {
            IEnumerable<HandleInfo> infos;
            Exception exception = null;
            TryGetAllInfo(out infos, ref exception);
            if (exception != null)
            {
                throw exception;
            }
            return infos;
        }

        protected internal virtual bool TryGetAllInfo(out IEnumerable<HandleInfo> infos, ref Exception exception)
        {
            return TryGetAllInfo(Malock.DefaultTimeout, out infos, ref exception);
        }

        protected internal virtual bool TryGetAllInfo(int timeout, out IEnumerable<HandleInfo> infos, ref Exception exception)
        {
            IList<HandleInfo> out_infos = emptryhandleinfos;
            Exception out_exception = null;
            try
            {
                if (timeout <= 0 && timeout != -1)
                {
                    out_exception = EventWaitHandle.NewTimeoutException();
                    return false;
                }
                using (ManualResetEvent events = new ManualResetEvent(false))
                {
                    bool success = false;
                    bool abort = false;
                    if (!MalockMessage.TryInvokeAsync(this, MalockNodeMessage.New(null, this.Identity, 
                        MalockNodeMessage.CLIENT_COMMAND_GETALLINFO, timeout), timeout,
                        (errno, message, stream) =>
                        {
                            if (errno == MalockMessage.Mappable.ERROR_NOERROR)
                            {
                                if (message.Command == MalockNodeMessage.CLIENT_COMMAND_GETALLINFO)
                                {
                                    out_infos = new List<HandleInfo>();
                                    success = HandleInfo.Fill(out_infos, stream);
                                }
                            }
                            else if (errno == MalockMessage.Mappable.ERROR_ABORTED)
                            {
                                abort = true;
                            }
                            else if (errno == MalockMessage.Mappable.ERROR_TIMEOUT)
                            {
                                out_exception = EventWaitHandle.NewTimeoutException();
                            }
                            events.Set();
                        }, ref out_exception) || out_exception != null)
                    {
                        return false;
                    }
                    events.WaitOne();
                    if (abort)
                    {
                        out_exception = EventWaitHandle.NewAbortedException();
                        return false;
                    }
                    return success;
                }
            }
            finally
            {
                infos = out_infos;
                exception = out_exception;
            }
        }

        protected internal virtual int TryGetAllInfo(out IEnumerable<HandleInfo> infos)
        {
            return TryGetAllInfo(Malock.DefaultTimeout, out infos);
        }

        protected internal virtual int TryGetAllInfo(int timeout, out IEnumerable<HandleInfo> infos)
        {
            IEnumerable<HandleInfo> result_handles = emptryhandleinfos;
            int result_errno = kERROR_ERRORNO;
            if (timeout <= 0 && timeout != -1)
            {
                result_errno = kERROR_TIMEOUT;
            }
            else
            {
                using (ManualResetEvent events = new ManualResetEvent(false))
                {
                    GetAllInfoAsync(timeout, (errno, models) =>
                    {
                        result_errno = errno;
                        result_handles = models;
                        events.Set();
                    });
                    events.WaitOne();
                }
            }
            infos = result_handles;
            return result_errno;
        }

        protected internal virtual void GetAllInfoAsync(Action<int, IEnumerable<HandleInfo>> state)
        {
            GetAllInfoAsync(Malock.DefaultTimeout, state);
        }

        protected internal virtual void GetAllInfoAsync(int timeout, Action<int, IEnumerable<HandleInfo>> state)
        {
            if (state == null)
            {
                throw new ArgumentNullException("callback");
            }
            if (timeout <= 0 && timeout != -1)
            {
                state(kERROR_TIMEOUT, emptryhandleinfos);
            }
            else
            {
                Exception exception = null;
                if (!MalockMessage.TryInvokeAsync(this,
                    MalockNodeMessage.New(null, this.Identity, MalockNodeMessage.CLIENT_COMMAND_GETALLINFO, timeout), timeout,
                       (errno, message, stream) =>
                       {
                           if (errno == MalockMessage.Mappable.ERROR_NOERROR)
                           {
                               bool error = true;
                               if (message.Command == MalockNodeMessage.CLIENT_COMMAND_GETALLINFO)
                               {
                                   IList<HandleInfo> results = new List<HandleInfo>();
                                   if (HandleInfo.Fill(results, stream))
                                   {
                                       error = false;
                                       state(kERROR_NOERROR, results);
                                   }
                               }
                               if (error)
                               {
                                   state(kERROR_ERRORNO, emptryhandleinfos);
                               }
                           }
                           else if (errno == MalockMessage.Mappable.ERROR_ABORTED)
                           {
                               state(kERROR_ABORTED, emptryhandleinfos);
                           }
                           else if (errno == MalockMessage.Mappable.ERROR_TIMEOUT)
                           {
                               state(kERROR_TIMEOUT, emptryhandleinfos);
                           }
                       }, ref exception))
                {
                    state(kERROR_ABORTED, emptryhandleinfos);
                }
            }
        }
    }
}
