namespace malock.NN
{
    using global::malock.Common;
    using System;
    using System.IO;

    public class HostEntry // aLIEz
    {
        public sealed class Host
        {
            public bool Available
            {
                get;
                set;
            }

            public string Address
            {
                get;
                set;
            }

            public override string ToString()
            {
                return string.Format("Address={0}, Available={1}", this.Address, this.Available);
            }

            internal Host()
            {

            }

            internal void Serialize(BinaryWriter writer)
            {
                if (writer == null)
                {
                    throw new ArgumentNullException("writer");
                }
                writer.Write(this.Available);
                writer.Write(this.Address);
            }

            internal bool Deserialize(BinaryReader reader)
            {
                if (reader == null)
                {
                    throw new ArgumentNullException("reader");
                }
                Stream stream = reader.BaseStream;
                if (!MalockMessage.StreamIsReadable(stream, sizeof(bool)))
                {
                    return false;
                }
                this.Available = reader.ReadBoolean();
                do
                {
                    string s;
                    if (!MalockMessage.TryFromStreamInRead(reader, out s))
                    {
                        return false;
                    }
                    this.Address = s;
                } while (false);
                return true;
            }
        }

        public Host Primary
        {
            get;
            private set;
        }

        public Host Standby
        {
            get;
            private set;
        }

        public object Tag
        {
            get;
            set;
        }

        public HostEntry()
        {
            this.Primary = new Host();
            this.Standby = new Host();
        }

        public bool Available
        {
            get
            {
                return this.Primary.Available || this.Standby.Available;
            }
        }

        public Host Select(string address)
        {
            if (this.Primary.Address == address)
            {
                return this.Primary;
            }
            else if (this.Standby.Address == address)
            {
                return this.Standby;
            }
            return null;
        }

        public string MeasureKey(bool positive)
        {
            string[] addresss = new string[2];
            Host host = positive ? this.Primary : this.Standby;
            if (host != null)
            {
                addresss[0] = host.Address;
            }
            host = positive ? this.Standby : this.Primary;
            if (host != null)
            {
                addresss[1] = host.Address;
            }
            if (string.IsNullOrEmpty(addresss[0]) && string.IsNullOrEmpty(addresss[1]))
            {
                return null;
            }
            return string.Format("{0}|{1}", addresss[0], addresss[1]); // %s|%s
        }

        public override int GetHashCode()
        {
            string key = this.MeasureKey(false);
            return key.GetHashCode();
        }

        public virtual Stream Serialize()
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
            this.Serialize(bw);
        }

        public virtual void Serialize(BinaryWriter writer)
        {
            if (writer == null)
            {
                throw new ArgumentNullException("writer");
            }
            this.Primary.Serialize(writer);
            this.Standby.Serialize(writer);
        }

        public static HostEntry Deserialize(BinaryReader reader)
        {
            HostEntry entry;
            TryDeserialize(reader, out entry);
            return entry;
        }

        public static bool TryDeserialize(BinaryReader reader, out HostEntry entry)
        {
            entry = null;
            do
            {
                HostEntry hostentry = new HostEntry();
                if (!hostentry.Primary.Deserialize(reader))
                {
                    return false;
                }
                Host standby = new Host();
                if (!hostentry.Standby.Deserialize(reader))
                {
                    return false;
                }
                entry = hostentry;
            } while (false);
            return true;
        }
    }
}
