namespace malock.Common
{
    using global::malock.Client;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using Mappable = global::malock.Client.EventWaitHandle.Mappable;
    using Interlocked = System.Threading.Interlocked;
    using Thread = System.Threading.Thread;

    public class Message : EventArgs
    {
        private static volatile int msgseq = 0;
        private static readonly int processid = Process.GetCurrentProcess().Id;

        public const byte CLIENT_COMMAND_LOCK_ENTER = 0;
        public const byte CLIENT_COMMAND_LOCK_EXIT = 1;
        public const byte CLIENT_COMMAND_GETALLINFO = 2;

        public const byte CLIENT_COMMAND_HEARTBEAT = 0xfa;
        public const byte CLIENT_COMMAND_LOCK_ACKPIPELINEENTER = 0xfb;
        public const byte CLIENT_COMMAND_LOCK_ACKPIPELINEEXIT = 0xfc;
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

        internal Message()
        {

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
            Stream stream = reader.BaseStream;
            if (!Message.StreamIsReadable(stream, sizeof(short)))
            {
                return null;
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
            if (!Message.StreamIsReadable(stream, len))
            {
                return null;
            }
            return Encoding.UTF8.GetString(reader.ReadBytes(len));
        }

        public static Message Deserialize(Stream stream)
        {
            BinaryReader br = new BinaryReader(stream);
            Message m = new Message();
            if (!Message.StreamIsReadable(stream, sizeof(byte)))
            {
                return null;
            }
            m.Command = br.ReadByte();
            if (!Message.StreamIsReadable(stream, sizeof(int)))
            {
                return null;
            }
            m.Sequence = br.ReadInt32();
            if (!Message.StreamIsReadable(stream, sizeof(int)))
            {
                return null;
            }
            m.Timeout = br.ReadInt32();
            m.Key = Message.FromStreamInRead(br);
            m.Identity = Message.FromStreamInRead(br);
            return m;
        }

        private static bool StreamIsReadable(Stream stream, int len)
        {
            if (stream == null || !stream.CanRead)
            {
                return false;
            }
            return unchecked(stream.Position + len) <= stream.Length;
        }

        public static bool TryDeserialize(Stream stream, out Message message)
        {
            message = null;
            try
            {
                message = Message.Deserialize(stream);
            }
            catch (Exception) { }
            return message != null;
        }

        public static int NewId()
        {
            return Interlocked.Increment(ref msgseq);
        }

        private static readonly ConcurrentDictionary<int, Mappable> msgmap = 
            new ConcurrentDictionary<int, Mappable>();
        private static Thread timeoutmaintaining = null;
        private static readonly EventHandler<MalockNetworkMessage> onmessagehandler = (sender, e) =>
        {
            Message message = e.Message;
            Mappable map = Message.GetByMap(message.Sequence);
            if (map != null)
            {
                var state = map.State;
                if (state != null)
                {
                    state(Mappable.ERROR_NOERROR, message, e.Stream);
                }
            }
        };
        private static readonly EventHandler onabortedhandler = (sender, e) =>
        {
            MalockClient malock = (MalockClient)sender;
            Message.Abort(malock);
        };

        static Message()
        {
            timeoutmaintaining = EventWaitHandle.Run(() =>
            {
                while (true)
                {
                    foreach (KeyValuePair<int, Mappable> kv in msgmap)
                    {
                        Mappable map = kv.Value;
                        if (map == null || map.Timeout < 0)
                        {
                            continue;
                        }
                        Stopwatch sw = map.Stopwatch;
                        if (sw.ElapsedMilliseconds > map.Timeout)
                        {
                            sw.Stop();

                            Mappable value;
                            msgmap.TryRemove(kv.Key, out value);

                            var state = map.State;
                            if (state != null)
                            {
                                state(Mappable.ERROR_TIMEOUT, null, null);
                            }
                        }
                    }
                    Thread.Sleep(100);
                }
            });
        }

        internal static void Bind(MalockClient malock)
        {
            if (malock == null)
            {
                throw new ArgumentNullException("malock");
            }
            malock.Message += onmessagehandler;
            malock.Aborted += onabortedhandler;
        }

        internal static void Unbind(MalockClient malock)
        {
            if (malock == null)
            {
                throw new ArgumentNullException("malock");
            }
            malock.Message -= onmessagehandler;
            malock.Aborted -= onabortedhandler;
        }

        internal static bool RegisterToMap(int msgid, Mappable map)
        {
            if (map == null)
            {
                throw new ArgumentNullException("map");
            }
            lock (msgmap)
            {
                if (!msgmap.TryAdd(msgid, map))
                {
                    return false;
                }
                else
                {
                    Stopwatch sw = map.Stopwatch;
                    sw.Reset();
                    sw.Start();
                }
                return true;
            }
        }

        internal static bool FromRemoveInMap(int msgid)
        {
            return GetByMap(msgid) != null;
        }

        internal static Mappable GetByMap(int msgid)
        {
            Mappable map = null;
            lock (msgmap)
            {
                if (msgmap.TryGetValue(msgid, out map))
                {
                    Stopwatch sw = map.Stopwatch;
                    sw.Stop();
                    Mappable value;
                    msgmap.TryRemove(msgid, out value);
                }
            }
            return map;
        }

        internal static void Abort(MalockClient malock)
        {
            if (malock == null)
            {
                throw new ArgumentNullException("malock");
            }
            lock (msgmap)
            {
                foreach (KeyValuePair<int, Mappable> kv in msgmap)
                {
                    Mappable map = kv.Value;
                    if (map == null && map.Client != malock)
                    {
                        continue;
                    }
                    else
                    {
                        Mappable mv;
                        msgmap.TryRemove(kv.Key, out mv);
                    }
                    var state = map.State;
                    if (state != null)
                    {
                        state(Mappable.ERROR_ABORTED, null, null);
                    }
                }
            }
        }
    }
}
