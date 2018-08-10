namespace malock.Common
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading;

    internal class Message : EventArgs
    {
        private static volatile int msgseq = 0;
        private static readonly int processid = Process.GetCurrentProcess().Id;

        public const byte CLIENT_COMMAND_LOCK_ENTER = 0; 
        public const byte CLIENT_COMMAND_LOCK_EXIT = 1;
        public const byte CLIENT_COMMAND_GETALLINFO = 2;

        public const byte CLIENT_COMMAND_TIMEOUT = 0xfe;
        public const byte CLIENT_COMMAND_ERROR = 0xff;

        public const byte SERVER_COMMAND_SYN_ENTER = CLIENT_COMMAND_LOCK_ENTER;
        public const byte SERVER_COMMAND_SYN_EXIT = CLIENT_COMMAND_LOCK_EXIT;
        public const byte SERVER_COMMAND_SYN_LOADALLINFO = CLIENT_COMMAND_GETALLINFO;
        public const byte SERVER_COMMAND_SYN_FREE = 0xf0;

        public const byte LINK_MODE_CLIENT = 0;
        public const byte LINK_MODE_SERVER = 1;
        /// <summary>
        /// 同步块动作
        /// </summary>
        public byte Command
        {
            get;
            set;
        }
        /// <summary>
        /// 消息流水号
        /// </summary>
        public int Sequence
        {
            get;
            set;
        }
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
        /// <summary>
        /// 标记的数据
        /// </summary>
        public object Tag
        {
            get;
            set;
        }

        public virtual Stream Serialize()
        {
            MemoryStream ms = new MemoryStream();
            this.Serialize(ms);
            return ms;
        }

        public virtual void Serialize(Stream stream)
        {
            BinaryWriter bw = new BinaryWriter(stream);
            bw.Write(unchecked((byte)this.Command));
            bw.Write(this.Sequence);
            bw.Write(this.Timeout);
            WriteStringToStream(bw, this.Key);
            WriteStringToStream(bw, this.Identity);
        }

        protected internal static void WriteStringToStream(BinaryWriter writer, string s)
        {
            if (writer == null)
            {
                throw new ArgumentNullException("writer");
            }
            int len = -1;
            if (s != null)
            {
                len = s.Length;
            }
            writer.Write(unchecked((short)len));
            if (len > 0)
            {
                byte[] buffer = Encoding.UTF8.GetBytes(s);
                writer.Write(buffer);
            }
        }

        protected internal static string FromStreamInRead(BinaryReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException("reader");
            }
            int len = reader.ReadInt16();
            if (len < 0)
            {
                return null;
            }
            if (len == 0)
            {
                return string.Empty;
            }
            return Encoding.UTF8.GetString(reader.ReadBytes(len));
        }

        public static Message Deserialize(Stream stream)
        {
            BinaryReader br = new BinaryReader(stream);
            Message m = new Message();
            m.Command = br.ReadByte();
            m.Sequence = br.ReadInt32();
            m.Timeout = br.ReadInt32();
            m.Key = FromStreamInRead(br);
            m.Identity = FromStreamInRead(br);
            return m;
        }

        public static int NewId()
        {
            return Interlocked.Increment(ref msgseq);
        }
    }
}
