namespace malock.Server
{
    using global::malock.Common;
    using System;
    using System.IO;
    using HandleInfo = global::malock.Client.HandleInfo;
    using System.Collections.Generic;

    public class MalockEngine
    {
        private MalockTaskPoll malockTaskPoll = null;
        private MalockTable malockTable = null;
        private MalockStandby malockStandby = null;
        private Dictionary<string, int> ackDeadlock = new Dictionary<string, int>();

        public MalockEngine(MalockTable malockTable, string standbyMachine)
        {
            if (malockTable == null)
            {
                throw new ArgumentNullException("malockTable");
            }
            this.malockTable = malockTable;
            this.malockTaskPoll = new MalockTaskPoll(this);
            this.malockStandby = new MalockStandby(this, standbyMachine);
        }

        public MalockStandby GetStandby()
        {
            return this.malockStandby;
        }

        public MalockTaskPoll GetPoll()
        {
            return this.malockTaskPoll;
        }

        public MalockTable GetTable()
        {
            return this.malockTable;
        }

        public bool Enter(MalockTaskInfo info)
        {
            if (!this.malockTable.Enter(info.Key, info.Identity))
            {
                return false;
            }
            using (Stream message = this.NewMessage(info.Key, info.Identity, Message.CLIENT_COMMAND_LOCK_ENTER,
                info.Sequence, info.Timeout).Serialize())
            {
                if (this.SendMessage(info.Socket, message))
                {
                    this.SendMessage(this.malockStandby, message);
                    return true;
                }
                else
                {
                    this.malockTable.Exit(info.Key, info.Identity);
                }
            }
            return false;
        }

        public void AckEnter(MalockTaskInfo info)
        {
            string key = this.GetAckKey(info);
            lock (this.ackDeadlock)
            {
                int count = 0;
                if (this.ackDeadlock.TryGetValue(key, out count))
                {
                    this.ackDeadlock[key] = 0;
                }
            }
        }

        private string GetAckKey(MalockTaskInfo info)
        {
            return this.GetAckKey(info.Identity, info.Key);
        }

        private string GetAckKey(string identity, string key)
        {
            return identity + "|" + key;
        }

        public void AckExit(MalockTaskInfo info)
        {
            string key = this.GetAckKey(info);
            lock (this.ackDeadlock)
            {
                int count = 0;
                if (!this.ackDeadlock.TryGetValue(key, out count))
                {
                    this.ackDeadlock.Add(key, ++count);
                }
                else
                {
                    this.ackDeadlock[key] = ++count;
                }
                if (count > Malock.AckNumberOfDeadlock)
                {
                    this.ackDeadlock[key] = 0;
                    this.Exit(info);
                }
            }
        }

        public bool Exit(MalockTaskInfo info)
        {
            byte errno = Message.CLIENT_COMMAND_LOCK_EXIT;
            if (!this.malockTable.Exit(info.Key, info.Identity))
            {
                errno = Message.CLIENT_COMMAND_ERROR;
            }
            using (Stream message = this.NewMessage(info.Key, info.Identity, errno, info.Sequence, info.Timeout).Serialize())
            {
                this.SendMessage(info.Socket, message);
                this.SendMessage(malockStandby, message);
            }
            this.AckEnter(info);
            return true;
        }

        public bool Timeout(MalockTaskInfo info)
        {
            Message message = this.NewMessage(info.Key, info.Identity, Message.CLIENT_COMMAND_TIMEOUT, info.Sequence, info.Timeout);
            this.SendMessage(info.Socket, message);
            return true;
        }

        public bool Abort(MalockTaskInfo info)
        {
            if (string.IsNullOrEmpty(info.Identity))
            {
                return false;
            }
            this.malockTaskPoll.Remove(info.Identity);
            do
            {
                string[] keys;
                this.malockTable.FreeKeyCollection(info.Identity, out keys);
            } while (false);
            Message message = this.NewMessage(info.Key, info.Identity, Message.SERVER_COMMAND_SYN_FREE, info.Sequence, -1);
            this.SendMessage(this.malockStandby, message);
            return true;
        }

        public unsafe bool GetAllInfo(MalockTaskInfo info)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                BinaryWriter bw = new BinaryWriter(ms);
                bw.Write(0);
                int count = 0;
                foreach (var i in this.malockTable.GetAllLocker())
                {
                    HandleInfo handleInfo = new HandleInfo(i.Key, i.Identity, i.Available);
                    handleInfo.Serialize(ms);
                    count++;
                }
                byte[] buffer = ms.GetBuffer();
                fixed (byte* pinned = buffer)
                {
                    *(int*)pinned = count;
                }
                Message message = this.NewMessage(info.Key, info.Identity, Message.CLIENT_COMMAND_GETALLINFO, info.Sequence, -1);
                return this.SendMessage(info.Socket, message, buffer, 0, Convert.ToInt32(ms.Position));
            }
        }

        private bool SendMessage(IMalockSender socket, Message message)
        {
            return this.SendMessage(socket, message, null, 0, 0);
        }

        private bool SendMessage(IMalockSender socket, Stream stream)
        {
            if (socket == null || stream == null)
            {
                return false;
            }
            MemoryStream ms = (MemoryStream)stream;
            return socket.Send(ms.GetBuffer(), 0, Convert.ToInt32(ms.Position));
        }

        private bool SendMessage(IMalockSender socket, Message message, byte[] buffer, int ofs, int len)
        {
            if (socket == null || message == null)
            {
                return false;
            }
            using (MemoryStream ms = (MemoryStream)message.Serialize())
            {
                if (buffer != null)
                {
                    ms.Write(buffer, ofs, len);
                }
                return socket.Send(ms.GetBuffer(), 0, Convert.ToInt32(ms.Position));
            }
        }

        private Message NewMessage(string key, string identity, byte command, int sequence, int timeout)
        {
            Message message = new Message();
            message.Key = key;
            message.Command = command;
            message.Sequence = sequence;
            message.Timeout = timeout;
            message.Identity = identity;
            return message;
        }
    }
}
