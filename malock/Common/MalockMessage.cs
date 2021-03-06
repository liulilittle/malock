﻿namespace malock.Common
{
    using global::malock.Client;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using Interlocked = System.Threading.Interlocked;
    using Thread = System.Threading.Thread;

    public abstract class MalockMessage : EventArgs
    {
        private static volatile int msgseq = 0;
        private static readonly int processid = Process.GetCurrentProcess().Id;

        internal const byte COMMON_COMMAND_TIMEOUT = 0xfe;
        internal const byte COMMON_COMMAND_ERROR = 0xff;
        internal const byte COMMON_COMMAND_HEARTBEAT = 0xfa;

        internal const byte LINK_MODE_CLIENT = 0;
        internal const byte LINK_MODE_SERVER = 1;
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
        /// 标记的数据
        /// </summary>
        public object Tag
        {
            get;
            set;
        }

        internal MalockMessage()
        {

        }

        internal virtual Stream Serialize()
        {
            MemoryStream ms = new MemoryStream();
            this.Serialize(ms);
            return ms;
        }

        internal void Serialize(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }
            BinaryWriter bw = new BinaryWriter(stream);
            this.Serialize(bw);
        }

        internal virtual void Serialize(BinaryWriter writer)
        {
            if (writer == null)
            {
                throw new ArgumentNullException("writer");
            }
            writer.Write(unchecked((byte)this.Command));
            writer.Write(this.Sequence);
        }

        internal static bool DeserializeTo(MalockMessage message, BinaryReader reader)
        {
            if (message == null)
            {
                throw new ArgumentNullException("message");
            }
            if (reader == null)
            {
                throw new ArgumentNullException("reader");
            }
            Stream stream = reader.BaseStream;
            if (!MalockMessage.StreamIsReadable(stream, sizeof(byte)))
            {
                return false;
            }
            message.Command = reader.ReadByte();
            if (!MalockMessage.StreamIsReadable(stream, sizeof(int)))
            {
                return false;
            }
            message.Sequence = reader.ReadInt32();
            return true;
        }

        internal static void WriteStringToStream(BinaryWriter writer, string s)
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

        internal static string FromStringInReadStream(BinaryReader reader)
        {
            string s;
            if (!MalockMessage.TryFromStringInReadStream(reader, out s))
            {
                throw new EndOfStreamException();
            }
            return s;
        }

        internal static bool TryFromStringInReadStream(BinaryReader reader, out string s)
        {
            s = null;
            if (reader == null)
            {
                throw new ArgumentNullException("reader");
            }
            Stream stream = reader.BaseStream;
            if (!MalockMessage.StreamIsReadable(stream, sizeof(short)))
            {
                return false;
            }
            int len = reader.ReadInt16();
            if (len < 0)
            {
                return true;
            }
            if (len == 0)
            {
                s = string.Empty;
                return true;
            }
            if (!MalockMessage.StreamIsReadable(stream, len))
            {
                return false;
            }
            s = Encoding.UTF8.GetString(reader.ReadBytes(len));
            return true;
        }

        internal static bool StreamIsReadable(Stream stream, int len)
        {
            if (stream == null || !stream.CanRead)
            {
                return false;
            }
            return unchecked(stream.Position + len) <= stream.Length;
        }

        internal static int NewId()
        {
            return Interlocked.Increment(ref msgseq);
        }

        private static readonly ConcurrentDictionary<int, Mappable> msgmap =
            new ConcurrentDictionary<int, Mappable>();
        private static Thread timeoutmaintaining = null;
        private static readonly EventHandler<MalockNetworkMessage> onmessagehandler = (sender, e) =>
        {
            MalockMessage message = e.Message;
            Mappable map = MalockMessage.GetByMap(message.Sequence);
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
            IMalockSocket malock = (IMalockSocket)sender;
            MalockMessage.Abort(malock);
        };

        static MalockMessage()
        {
            timeoutmaintaining = EventWaitHandle.Run(() =>
            {
                while (true)
                {
                    foreach (KeyValuePair<int, Mappable> kv in msgmap)
                    {
                        Mappable map = kv.Value;
                        if (map == null || map.Timeout == -1)
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

        internal static void Bind<TMessage>(MalockMixClient<TMessage> malock)
            where TMessage : MalockMessage
        {
            if (malock == null)
            {
                throw new ArgumentNullException("malock");
            }
            malock.Message += onmessagehandler;
            malock.Aborted += onabortedhandler;
        }

        internal static void Unbind<TMessage>(MalockMixClient<TMessage> malock)
            where TMessage : MalockMessage
        {
            if (malock == null)
            {
                throw new ArgumentNullException("malock");
            }
            malock.Message -= onmessagehandler;
            malock.Aborted -= onabortedhandler;
        }

        internal static void Abort(IMalockSocket malock)
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

        internal static bool TrySendMessage(IMalockSocket socket, MalockMessage message, byte[] buffer, int ofs, int len)
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

        internal static bool TrySendMessage(IMalockSocket socket, Stream message)
        {
            if (socket == null || message == null)
            {
                return false;
            }
            MemoryStream ms = (MemoryStream)message;
            return socket.Send(ms.GetBuffer(), 0, Convert.ToInt32(ms.Position));
        }

        internal static bool TrySendMessage(IMalockSocket malock, MalockMessage message)
        {
            Exception exception = null;
            return TrySendMessage(malock, message, ref exception);
        }

        internal static bool TrySendMessage(IMalockSocket malock, MalockMessage message, ref Exception exception)
        {
            if (malock == null)
            {
                throw new ArgumentNullException("malock");
            }
            if (message == null)
            {
                throw new ArgumentNullException("message");
            }
            using (MemoryStream ms = (MemoryStream)message.Serialize())
            {
                if (!malock.Send(ms.GetBuffer(), 0, unchecked((int)ms.Position)))
                {
                    exception = new InvalidOperationException("The malock send returned results do not match the expected");
                    return false;
                }
            }
            return true;
        }

        internal class Mappable
        {
            public const int ERROR_TIMEOUT = 2;
            public const int ERROR_ABORTED = 1;
            public const int ERROR_NOERROR = 0;

            public Action<int, MalockMessage, Stream> State
            {
                get;
                set;
            }

            public Stopwatch Stopwatch
            {
                get;
                private set;
            }

            public int Timeout
            {
                get;
                set;
            }

            public IMalockSocket Client
            {
                get;
                set;
            }

            public object Tag
            {
                get;
                set;
            }

            public Mappable()
            {
                this.Stopwatch = new Stopwatch();
            }
        }

        internal static bool TryInvokeAsync(IMalockSocket malock, MalockMessage message, int timeout, Action<int, MalockMessage, Stream> callback)
        {
            Exception exception = null;
            return TryInvokeAsync(malock, message, timeout, callback, ref exception);
        }

        internal static bool TryInvokeAsync(IMalockSocket malock, MalockMessage message, int timeout, Action<int, MalockMessage, Stream> callback, ref Exception exception)
        {
            if (malock == null)
            {
                throw new ArgumentNullException("malock");
            }
            if (message == null)
            {
                throw new ArgumentNullException("message");
            }
            Mappable mapinfo = new Mappable()
            {
                State = callback,
                Tag = null,
                Timeout = timeout,
                Client = malock,
            };
            if (!MalockMessage.RegisterToMap(message.Sequence, mapinfo))
            {
                exception = new InvalidOperationException("An internal error cannot add a call to a rpc-task in the map table");
                return false;
            }
            return TrySendMessage(malock, message, ref exception);
        }
    }
}
