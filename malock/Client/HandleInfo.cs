namespace malock.Client
{
    using global::malock.Common;
    using System;
    using System.Collections.Generic;
    using System.IO;

    public sealed class HandleInfo
    {
        public string Key
        {
            get;
            private set;
        }

        public bool Available
        {
            get;
            private set;
        }

        public string Identity
        {
            get;
            private set;
        }

        private HandleInfo()
        {

        }

        internal HandleInfo(string key, string identity, bool available)
        {
            if (key == null)
            {
                throw new ArgumentNullException("key");
            }
            if (key.Length <= 0)
            {
                throw new ArgumentOutOfRangeException("key");
            }
            this.Key = key;
            this.Identity = identity;
            this.Available = available;
        }

        public Stream Serialize()
        {
            MemoryStream ms = new MemoryStream();
            this.Serialize(ms);
            return ms;
        }

        public void Serialize(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }
            BinaryWriter bw = new BinaryWriter(stream);
            bw.Write(this.Available);
            MalockMessage.WriteStringToStream(bw, this.Key);
            MalockMessage.WriteStringToStream(bw, this.Identity);
        }

        public static HandleInfo Deserialize(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }
            BinaryReader br = new BinaryReader(stream);
            HandleInfo info = new HandleInfo();
            if (!MalockMessage.StreamIsReadable(stream, sizeof(bool)))
            {
                return null;
            }
            info.Available = br.ReadBoolean();
            string s;
            if (!MalockMessage.TryFromStringInReadStream(br, out s))
            {
                return null;
            }
            info.Key = s;
            if (!MalockMessage.TryFromStringInReadStream(br, out s))
            {
                return null;
            }
            info.Identity = s;
            return info;
        }

        public static bool Fill(IList<HandleInfo> s, Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }
            if (s == null)
            {
                throw new ArgumentNullException("s");
            }
            try
            {
                BinaryReader br = new BinaryReader(stream);
                int count = br.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    HandleInfo info = HandleInfo.Deserialize(stream);
                    s.Add(info);
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
