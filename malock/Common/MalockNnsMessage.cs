namespace malock.Common
{
    using System;
    using System.IO;

    public sealed class MalockNnsMessage : MalockMessage
    {
        internal const byte CLIENT_COMMAND_QUERYHOSTENTRYINFO = 0x01;
        internal const byte CLIENT_COMMAND_DUMPHOSTENTRYINFO = 0x02;

        internal const byte SERVER_NNS_COMMAND_SYN_HOSTENTRYINFO = 0x01;
        internal const byte SERVER_NDN_COMMAND_REGISTERHOSTENTRYINFO = 0x02;
        internal const byte SERVER_NNS_COMMAND_DUMPHOSTENTRYINFO = 0x03;

        public string Key
        {
            get;
            set;
        }

        public string Identity
        {
            get;
            set;
        }

        internal override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);
            WriteStringToStream(writer, this.Key);
            WriteStringToStream(writer, this.Identity);
        }

        internal static bool TryDeserialize(Stream stream, out MalockNnsMessage message)
        {
            message = Deserialize(stream);
            return message != null;
        }

        internal static MalockNnsMessage Deserialize(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }
            BinaryReader br = new BinaryReader(stream);
            var m = new MalockNnsMessage();
            if (!MalockMessage.DeserializeTo(m, br))
            {
                return null;
            }
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
    }
}
