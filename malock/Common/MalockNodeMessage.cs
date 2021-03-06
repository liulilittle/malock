﻿namespace malock.Common
{
    using System;
    using System.IO;

    public sealed class MalockNodeMessage : MalockMessage
    {
        internal const byte CLIENT_COMMAND_LOCK_ENTER = 0x00;
        internal const byte CLIENT_COMMAND_LOCK_EXIT = 0x01;
        internal const byte CLIENT_COMMAND_GETALLINFO = 0x02;

        internal const byte CLIENT_COMMAND_LOCK_ACKPIPELINEENTER = 0xfb;
        internal const byte CLIENT_COMMAND_LOCK_ACKPIPELINEEXIT = 0xfc;

        internal const byte SERVER_COMMAND_SYN_ENTER = 0x00;
        internal const byte SERVER_COMMAND_SYN_EXIT = 0x01;
        internal const byte SERVER_COMMAND_SYN_LOADALLINFO = 0x02;
        internal const byte SERVER_COMMAND_SYN_FREE = 0xf0;

        /// <summary>
        /// 超时时间
        /// </summary>
        public int Timeout
        {
            get;
            set;
        }
        /// <summary>
        /// 同步块索引
        /// </summary>
        public string Key
        {
            get;
            set;
        }
        /// <summary>
        /// 身份标识
        /// </summary>
        public string Identity
        {
            get;
            set;
        }

        internal override Stream Serialize()
        {
            MemoryStream ms = new MemoryStream();
            this.Serialize(ms);
            return ms;
        }

        internal override void Serialize(BinaryWriter writer)
        {
            if (writer == null)
            {
                throw new ArgumentNullException("writer");
            }
            base.Serialize(writer);
            writer.Write(this.Timeout);
            WriteStringToStream(writer, this.Key);
            WriteStringToStream(writer, this.Identity);
        }

        internal static MalockNodeMessage Deserialize(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }
            BinaryReader br = new BinaryReader(stream);
            MalockNodeMessage m = new MalockNodeMessage();
            if (!MalockMessage.DeserializeTo(m, br))
            {
                return null;
            }
            m.Timeout = br.ReadInt32();
            string s;
            if (!MalockMessage.TryFromStringInReadStream(br, out s))
            {
                return null;
            }
            m.Key = s;
            if (!MalockMessage.TryFromStringInReadStream(br, out s))
            {
                return null;
            }
            m.Identity = s;
            return m;
        }

        internal static MalockNodeMessage New(string key, string identity, byte command, int sequence, int timeout)
        {
            MalockNodeMessage message = new MalockNodeMessage();
            message.Sequence = sequence;
            message.Key = key;
            message.Command = command;
            message.Timeout = timeout;
            message.Identity = identity;
            return message;
        }

        internal static MalockNodeMessage New(string key, string identity, byte command, int timeout)
        {
            return New(key, identity, command, MalockMessage.NewId(), timeout);
        }

        internal static bool TryDeserialize(Stream stream, out MalockNodeMessage message)
        {
            return (message = MalockNodeMessage.Deserialize(stream)) != null;
        }
    }
}
