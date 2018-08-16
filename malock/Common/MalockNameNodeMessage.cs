namespace malock.Common
{
    using System;
    using System.IO;

    public sealed class MalockNameNodeMessage : MalockMessage
    {
        public const byte CLIENT_COMMAND_QUERYHOSTENTRYINFO = 0x01;
        public const byte CLIENT_COMMAND_REPORTHOSTENTRYINFO = 0x02;
        public const byte CLIENT_COMMAND_DUMPHOSTENTRYINFO = 0x03;
        public const byte SERVER_COMMAND_SYN_HOSTENTRYINFO = 0x04;

        public string Key
        {
            get;
            set;
        }

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);
            WriteStringToStream(writer, this.Key);
        }

        public static bool TryDeserialize(Stream stream, out MalockNameNodeMessage message)
        {
            return (message = MalockNameNodeMessage.Deserialize(stream)) != null;
        }

        public static MalockNameNodeMessage Deserialize(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }
            BinaryReader br = new BinaryReader(stream);
            var m = new MalockNameNodeMessage();
            if (!MalockMessage.DeserializeTo(m, br))
            {
                return null;
            }
            string s;
            if (!MalockMessage.TryFromStreamInRead(br, out s))
            {
                return null;
            }
            m.Key = s;
            return m;
        }
    }
}
