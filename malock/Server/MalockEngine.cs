namespace malock.Server
{
    using global::malock.Common;
    using System;
    using System.IO;
    using HandleInfo = global::malock.Client.HandleInfo;
    using System.Collections.Generic;

    internal sealed class MalockEngine
    {
        private MalockTaskPoll malockTaskPoll = null;
        private MalockTable malockTable = null;
        private MalockConfiguration configuration = null;
        private MalockNnsClient malockNnsClient = null;
        private MalockStandbyClient malockStandbyClient = null;
        private Dictionary<string, int> ackPipelineCounter = new Dictionary<string, int>();

        public MalockEngine(MalockTable malockTable, MalockConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException("configuration");
            }
            if (malockTable == null)
            {
                throw new ArgumentNullException("malockTable");
            }
            this.configuration = configuration;
            this.malockTable = malockTable;
            this.malockTaskPoll = new MalockTaskPoll(this);
            this.malockStandbyClient = new MalockStandbyClient(this, configuration);
            this.malockNnsClient = new MalockNnsClient(configuration.Identity, configuration.NnsNode, configuration.StandbyNode);
        }

        public MalockStandbyClient GetStandbyClient()
        {
            return this.malockStandbyClient;
        }

        public MalockConfiguration GetConfiguration()
        {
            return this.configuration;
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
            using (Stream message = this.NewMessage(info.Key, info.Identity, MalockDataNodeMessage.CLIENT_COMMAND_LOCK_ENTER,
                info.Sequence, info.Timeout).Serialize())
            {
                if (MalockMessage.TrySendMessage(info.Socket, message))
                {
                    MalockMessage.TrySendMessage(this.malockStandbyClient, message);
                    return true;
                }
                else
                {
                    this.malockTable.Exit(info.Key, info.Identity);
                    this.AckPipelineEnter(info);
                }
            }
            return false;
        }

        public void AckPipelineEnter(MalockTaskInfo info)
        {
            string key = this.GetAckPipelineKey(info);
            lock (this.ackPipelineCounter)
            {
                int count = 0;
                if (this.ackPipelineCounter.TryGetValue(key, out count))
                {
                    this.ackPipelineCounter[key] = 0;
                }
            }
        }

        private string GetAckPipelineKey(MalockTaskInfo info)
        {
            return this.GetAckPipelineKey(info.Identity, info.Key);
        }

        private string GetAckPipelineKey(string identity, string key)
        {
            return identity + "|" + key;
        }

        public void AckPipelineExit(MalockTaskInfo info) // anti-deadlock
        {
            string key = this.GetAckPipelineKey(info);
            lock (this.ackPipelineCounter)
            {
                bool entering = this.malockTable.IsEnter(info.Key, info.Identity);
                int count = 0;
                if (!this.ackPipelineCounter.TryGetValue(key, out count))
                {
                    if (entering)
                    {
                        this.ackPipelineCounter.Add(key, ++count);
                    }
                }
                else if (!entering)
                {
                    count = 0; // reset counter
                    this.ackPipelineCounter[key] = 0;
                }
                else
                {
                    this.ackPipelineCounter[key] = ++count;
                }
                if (count > Malock.AckPipelineDeadlockCount)
                {
                    this.ackPipelineCounter[key] = 0;
                    this.Exit(info);
                }
            }
        }

        public bool Exit(MalockTaskInfo info)
        {
            byte errno = MalockDataNodeMessage.CLIENT_COMMAND_LOCK_EXIT;
            if (!this.malockTable.Exit(info.Key, info.Identity))
            {
                errno = MalockMessage.COMMON_COMMAND_ERROR;
            }
            using (Stream message = this.NewMessage(info.Key, info.Identity, errno, info.Sequence, info.Timeout).Serialize())
            {
                if (info.Socket != null)
                {
                    MalockMessage.TrySendMessage(info.Socket, message);
                }
                MalockMessage.TrySendMessage(malockStandbyClient, message);
            }
            this.AckPipelineEnter(info);
            return true;
        }

        public bool Timeout(MalockTaskInfo info)
        {
            MalockMessage message = this.NewMessage(info.Key, info.Identity, MalockMessage.COMMON_COMMAND_TIMEOUT, info.Sequence, info.Timeout);
            MalockMessage.TrySendMessage(info.Socket, message);
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
                this.AckPipelineEnter(info.Identity, keys);
            } while (false);
            MalockMessage message = this.NewMessage(info.Key, info.Identity, MalockDataNodeMessage.SERVER_COMMAND_SYN_FREE, info.Sequence, -1);
            MalockMessage.TrySendMessage(this.malockStandbyClient, message);
            return true;
        }

        public void AckPipelineEnter(string identity, string[] keys)
        {
            if (keys == null || keys.Length <= 0)
            {
                return;
            }
            if (string.IsNullOrEmpty(identity))
            {
                return;
            }
            for (int i = 0; i < keys.Length; i++)
            {
                string key = keys[i];
                this.AckPipelineEnter(new MalockTaskInfo()
                {
                    Key = key,
                    Identity = identity,
                });
            }
        }

        public unsafe bool GetAllInfo(MalockTaskInfo info)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                BinaryWriter bw = new BinaryWriter(ms);
                bw.Write(0);
                int count = 0;
                foreach (var locker in this.malockTable.GetAllLocker())
                {
                    lock (locker)
                    {
                        HandleInfo handleInfo = new HandleInfo(
                            locker.Key, locker.Identity, locker.Available);
                        handleInfo.Serialize(ms);
                    }
                    count++;
                }
                byte[] buffer = ms.GetBuffer();
                fixed (byte* pinned = buffer)
                {
                    *(int*)pinned = count;
                }
                MalockMessage message = this.NewMessage(info.Key, info.Identity, MalockDataNodeMessage.CLIENT_COMMAND_GETALLINFO, info.Sequence, -1);
                return MalockMessage.TrySendMessage(info.Socket, message, buffer, 0, Convert.ToInt32(ms.Position));
            }
        }

        private MalockMessage NewMessage(string key, string identity, byte command, int sequence, int timeout)
        {
            MalockDataNodeMessage message = new MalockDataNodeMessage();
            message.Key = key;
            message.Command = command;
            message.Sequence = sequence;
            message.Timeout = timeout;
            message.Identity = identity;
            return message;
        }
    }
}
