namespace malock.Server
{
    using global::malock.Common;
    using System;
    using System.Diagnostics;
    using System.IO;

    public sealed class MalockServer
    {
        private MalockSocketListener malockListener = null;
        private MalockEngine malockEngine = null;
        private EventHandler onAboredHandler = null;
        private EventHandler onConnectedHandler = null;
        private EventHandler<MalockSocketStream> onReceivedHandler = null;
        /// <summary>
        /// 创建一个双机热备的 malock 服务器
        /// </summary>
        /// <param name="port">指定当前服务器实例侦听连接的端口</param>
        /// <param name="standbyMachine">指定一个有效的备用服务器主机地址</param>
        public MalockServer(int port, string standbyMachine)
        {
            if (port <= 0 || port > short.MaxValue)
            {
                throw new ArgumentOutOfRangeException("The specified server listening port is outside the 0~65535 range");
            }
            if (string.IsNullOrEmpty(standbyMachine))
            {
                throw new ArgumentOutOfRangeException("You have specified an invalid standby server host address that is not allowed to be null or empty");
            }
            this.malockEngine = new MalockEngine(new MalockTable(), standbyMachine);
            this.malockListener = new MalockSocketListener(port);
            do
            {
                this.onAboredHandler = this.ProcessAborted;
                this.onReceivedHandler = this.ProcessReceived;
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

        private void ProcessAccept(object sender, MalockSocket e)
        {
            if (e.LinkMode == MalockMessage.LINK_MODE_CLIENT)
            {
                MalockTable malock = this.malockEngine.GetTable();
                malock.AllocKeyCollection(e.Identity);
            }
            /* 
             * This creates a bug in a distributed atomic-state deadlock. 
                else if (e.LinkMode == Message.LINK_MODE_SERVER)
                {
                    this.malockEngine.GetAllInfo(new MalockTaskInfo()
                    {
                        Identity = e.Identity,
                        Socket = e,
                        Timeout = -1,
                        Type = MalockTaskType.kGetAllInfo
                    });
                }
            */
        }

        private void ProcessAborted(object sender, EventArgs e)
        {
            MalockSocket socket = (MalockSocket)sender;
            if (socket.LinkMode == MalockMessage.LINK_MODE_CLIENT)
            {
                if (!string.IsNullOrEmpty(socket.Identity))
                {
                    this.malockEngine.Abort(new MalockTaskInfo()
                    {
                        Type = MalockTaskType.kAbort,
                        Key = null,
                        Stopwatch = null,
                        Timeout = -1,
                        Sequence = MalockMessage.NewId(),
                        Socket = socket,
                        Identity = socket.Identity,
                    });
                }
            }
            else if (socket.LinkMode == MalockMessage.LINK_MODE_SERVER)
            {
                MalockDataNodeMessage message = (MalockDataNodeMessage)socket.UserToken;
                if (message != null)
                {
                    string[] keys;
                    MalockTable malock = this.malockEngine.GetTable();
                    malock.Exit(message.Identity, out keys);
                    this.malockEngine.AckPipelineEnter(message.Identity, keys);
                }
            }
            lock (socket)
            {
                socket.Aborted -= this.onAboredHandler;
                socket.Connected -= this.onConnectedHandler;
                socket.Received -= this.onReceivedHandler;
            }
        }

        private void ProcessReceived(object sender, MalockSocketStream e)
        {
            MalockDataNodeMessage message = null;
            using (Stream stream = e.Stream)
            {
                MalockDataNodeMessage.TryDeserialize(stream, out message);
            }
            if (message != null)
            {
                this.ProcessMessage(e.Socket, message);
            }
        }

        private void ProcessClient(MalockSocket socket, MalockDataNodeMessage message)
        {
            if (message.Command == MalockDataNodeMessage.CLIENT_COMMAND_LOCK_ENTER ||
                message.Command == MalockDataNodeMessage.CLIENT_COMMAND_LOCK_EXIT ||
                message.Command == MalockDataNodeMessage.CLIENT_COMMAND_GETALLINFO || 
                message.Command == MalockDataNodeMessage.CLIENT_COMMAND_LOCK_ACKPIPELINEEXIT || 
                message.Command == MalockDataNodeMessage.CLIENT_COMMAND_LOCK_ACKPIPELINEENTER)
            {
                MalockTaskInfo info = new MalockTaskInfo();
                switch (message.Command)
                {
                    case MalockDataNodeMessage.CLIENT_COMMAND_LOCK_ENTER:
                        info.Type = MalockTaskType.kEnter;
                        break;
                    case MalockDataNodeMessage.CLIENT_COMMAND_LOCK_EXIT:
                        info.Type = MalockTaskType.kExit;
                        break;
                    case MalockDataNodeMessage.CLIENT_COMMAND_GETALLINFO:
                        info.Type = MalockTaskType.kGetAllInfo;
                        break;
                    case MalockDataNodeMessage.CLIENT_COMMAND_LOCK_ACKPIPELINEEXIT:
                        info.Type = MalockTaskType.kAckPipelineExit;
                        break;
                    case MalockDataNodeMessage.CLIENT_COMMAND_LOCK_ACKPIPELINEENTER:
                        info.Type = MalockTaskType.kAckPipelineEnter;
                        break;
                }
                info.Sequence = message.Sequence;
                info.Socket = socket;
                info.Key = message.Key;
                info.Identity = socket.Identity;
                info.Timeout = message.Timeout;
                do
                {
                    Stopwatch sw = new Stopwatch();
                    info.Stopwatch = sw;
                    sw.Start();
                } while (false);
                if (message.Command == MalockDataNodeMessage.CLIENT_COMMAND_LOCK_EXIT)
                {
                    this.malockEngine.Exit(info);
                }
                else if (message.Command == MalockDataNodeMessage.CLIENT_COMMAND_LOCK_ENTER)
                {
                    if (this.malockEngine.Enter(info))
                    {
                        this.malockEngine.AckPipelineEnter(info);
                    }
                    else
                    {
                        this.malockEngine.GetPoll().Add(info);
                    }
                }
                else if (message.Command == MalockDataNodeMessage.CLIENT_COMMAND_LOCK_ACKPIPELINEEXIT)
                {
                    this.malockEngine.AckPipelineExit(info); // anti-deadlock
                }
                else if (message.Command == MalockDataNodeMessage.CLIENT_COMMAND_LOCK_ACKPIPELINEENTER)
                {
                    this.malockEngine.AckPipelineEnter(info);
                }
                else if (message.Command == MalockDataNodeMessage.CLIENT_COMMAND_GETALLINFO)
                {
                    this.malockEngine.GetAllInfo(info);
                }
            }
        }

        private void ProcessServer(MalockSocket socket, MalockDataNodeMessage message)
        {
            if (message.Command == MalockDataNodeMessage.SERVER_COMMAND_SYN_ENTER)
            {
                socket.UserToken = message;
                MalockTable malock = this.malockEngine.GetTable();
                malock.Enter(message.Key, message.Identity);
            }
            else if (message.Command == MalockDataNodeMessage.SERVER_COMMAND_SYN_EXIT)
            {
                MalockTable malock = this.malockEngine.GetTable();
                malock.Exit(message.Key, message.Identity);
                do
                {
                    this.malockEngine.AckPipelineExit(new MalockTaskInfo()
                    {
                        Key = message.Key,
                        Identity = message.Identity,
                    });
                } while (false);
            }
            else if (message.Command == MalockDataNodeMessage.SERVER_COMMAND_SYN_FREE)
            {
                string[] keys;
                MalockTable malock = this.malockEngine.GetTable();
                malock.Exit(message.Identity, out keys);
                this.malockEngine.AckPipelineEnter(message.Identity, keys);
            }
        }

        private void ProcessMessage(MalockSocket socket, MalockDataNodeMessage message)
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

        public void Run()
        {
            this.malockListener.Run();
        }

        public void Close()
        {
            this.malockListener.Close();
        }
    }
}
