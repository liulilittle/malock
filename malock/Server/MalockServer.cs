namespace malock.Server
{
    using global::malock.Common;
    using System;
    using System.Diagnostics;
    using System.IO;

    public class MalockServer
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
            this.onAboredHandler = this.ProcessAborted;
            this.onReceivedHandler = this.ProcessReceived;
            this.malockEngine = new MalockEngine(new MalockTable(), standbyMachine);
            this.malockListener = new MalockSocketListener(port);
            this.malockListener.Accept += (sender, e) =>
            {
                e.Received += this.onReceivedHandler;
                e.Aborted += this.onAboredHandler;
                e.Connected += this.onConnectedHandler;
                e.Run();
            };
            this.onConnectedHandler = (sender, e) => this.ProcessAccept(sender, (MalockSocket)sender);
        }

        private void ProcessAccept(object sender, MalockSocket e)
        {
            if (e.LinkMode == Message.LINK_MODE_CLIENT)
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
            if (socket.LinkMode == Message.LINK_MODE_CLIENT)
            {
                if (!string.IsNullOrEmpty(socket.Identity))
                {
                    this.malockEngine.Abort(new MalockTaskInfo()
                    {
                        Type = MalockTaskType.kAbort,
                        Key = null,
                        Stopwatch = null,
                        Timeout = -1,
                        Sequence = Message.NewId(),
                        Socket = socket,
                        Identity = socket.Identity,
                    });
                }
            }
            else if (socket.LinkMode == Message.LINK_MODE_SERVER)
            {
                Message message = (Message)socket.UserToken;
                if (message != null)
                {
                    string[] keys;
                    MalockTable malock = this.malockEngine.GetTable();
                    malock.Exit(message.Identity, out keys);
                }
            }
        }

        private void ProcessReceived(object sender, MalockSocketStream e)
        {
            Message message = null;
            using (Stream stream = e.Stream)
            {
                try
                {
                    message = Message.Deserialize(e.Stream);
                }
                catch (Exception) { }
            }
            if (message != null)
            {
                message.Tag = e.Socket;
                this.ProcessMessage(e.Socket, message);
            }
        }

        private void ProcessClient(MalockSocket socket, Message message)
        {
            if (message.Command == Message.CLIENT_COMMAND_LOCK_ENTER ||
                message.Command == Message.CLIENT_COMMAND_LOCK_EXIT ||
                message.Command == Message.CLIENT_COMMAND_GETALLINFO)
            {
                MalockTaskInfo info = new MalockTaskInfo();
                switch (message.Command)
                {
                    case Message.CLIENT_COMMAND_LOCK_ENTER:
                        info.Type = MalockTaskType.kEnter;
                        break;
                    case Message.CLIENT_COMMAND_LOCK_EXIT:
                        info.Type = MalockTaskType.kExit;
                        break;
                    case Message.CLIENT_COMMAND_GETALLINFO:
                        info.Type = MalockTaskType.kGetAllInfo;
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
                if (message.Command == Message.CLIENT_COMMAND_LOCK_EXIT)
                {
                    malockEngine.Exit(info, true);
                }
                else if (message.Command == Message.CLIENT_COMMAND_LOCK_ENTER)
                {
                    if (!malockEngine.Enter(info)) 
                    {
                        malockEngine.GetPoll().Add(socket.Identity, info);
                    }
                }
                else if (message.Command == Message.CLIENT_COMMAND_GETALLINFO)
                {
                    malockEngine.GetAllInfo(info);
                }
            }
        }

        private void ProcessServer(MalockSocket socket, Message message)
        {
            if (message.Command == Message.SERVER_COMMAND_SYN_ENTER)
            {
                socket.UserToken = message;
                MalockTable malock = this.malockEngine.GetTable();
                malock.Enter(message.Key, message.Identity);
            }
            else if (message.Command == Message.SERVER_COMMAND_SYN_EXIT)
            {
                MalockTable malock = this.malockEngine.GetTable();
                malock.Exit(message.Key, message.Identity);
            }
            else if (message.Command == Message.SERVER_COMMAND_SYN_FREE)
            {
                string[] keys;
                MalockTable malock = this.malockEngine.GetTable();
                malock.Exit(message.Identity, out keys);
            }
        }

        private void ProcessMessage(MalockSocket socket, Message message)
        {
            switch (socket.LinkMode)
            {
                case Message.LINK_MODE_CLIENT:
                    this.ProcessClient(socket, message);
                    break;
                case Message.LINK_MODE_SERVER:
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
