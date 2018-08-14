namespace malock.Common
{
    using System;
    using System.IO;

    public sealed class MalockDataNodeMessage : MalockMessage
    {
        public const byte CLIENT_COMMAND_LOCK_ENTER = 0x00;
        public const byte CLIENT_COMMAND_LOCK_EXIT = 0x01;
        public const byte CLIENT_COMMAND_GETALLINFO = 0x02;

        public const byte CLIENT_COMMAND_LOCK_ACKPIPELINEENTER = 0xfb;
        public const byte CLIENT_COMMAND_LOCK_ACKPIPELINEEXIT = 0xfc;

        public const byte SERVER_COMMAND_SYN_ENTER = CLIENT_COMMAND_LOCK_ENTER;
        public const byte SERVER_COMMAND_SYN_EXIT = CLIENT_COMMAND_LOCK_EXIT;
        public const byte SERVER_COMMAND_SYN_LOADALLINFO = CLIENT_COMMAND_GETALLINFO;
        public const byte SERVER_COMMAND_SYN_FREE = 0xf0;

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

        public override Stream Serialize()
        {
            MemoryStream ms = new MemoryStream();
            this.Serialize(ms);
            return ms;
        }

        public override void Serialize(BinaryWriter writer)
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

        public static MalockDataNodeMessage Deserialize(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }
            BinaryReader br = new BinaryReader(stream);
            MalockDataNodeMessage m = new MalockDataNodeMessage();
            if (!MalockMessage.DeserializeTo(m, br))
            {
                return null;
            }
            m.Timeout = br.ReadInt32();
            string s;
            if (!MalockMessage.TryFromStreamInRead(br, out s))
            {
                return null;
            }
            m.Key = s;
            if (!MalockMessage.TryFromStreamInRead(br, out s))
            {
                return null;
            }
            m.Identity = s;
            return m;
        }

        public static bool TryDeserialize(Stream stream, out MalockDataNodeMessage message)
        {
            return (message = MalockDataNodeMessage.Deserialize(stream)) != null;
        }
    }
}
