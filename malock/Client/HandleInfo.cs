namespace malock.Client
{
    using global::malock.Common;
    using System;
    using System.Collections.Generic;
    using System.IO;

    public class HandleInfo
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
            this.Key = key;
            this.Identity = identity;
            this.Available = available;
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
            bw.Write(this.Available);
            Message.WriteStringToStream(bw, this.Key);
            Message.WriteStringToStream(bw, this.Identity);
        }

        public static HandleInfo Deserialize(Stream stream)
        {
            BinaryReader br = new BinaryReader(stream);
            HandleInfo info = new HandleInfo();
            info.Available = br.ReadBoolean();
            info.Key = Message.FromStreamInRead(br);
            info.Identity = Message.FromStreamInRead(br);
            return info;
        }

        public static bool Fill(IList<HandleInfo> s, Stream stream)
        {
            if (s == null || stream == null)
            {
                return false;
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
