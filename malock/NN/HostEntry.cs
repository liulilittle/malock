namespace malock.NN
{
    using global::malock.Common;
    using System;
    using System.IO;
    using System.Runtime.CompilerServices;

    public class HostEntry // aLIEz
    {
        public static bool operator ==(HostEntry x, HostEntry y)
        {
            object xoo = x;
            object yoo = y;
            if (xoo == yoo || xoo == null && yoo == null)
            {
                return true;
            }
            if (xoo != null && yoo == null)
            {
                return false;
            }
            if (xoo == null && yoo != null)
            {
                return false;
            }
            return x.Equals(y);
        }

        public static bool operator !=(HostEntry x, HostEntry y)
        {
            return !(x == y);
        }

        public override bool Equals(object obj)
        {
            HostEntry key = obj as HostEntry;
            if (key == null)
            {
                return false;
            }
            if (RuntimeHelpers.Equals(this, obj))
            {
                return true;
            }
            if (this.Primary.Address == key.Primary.Address &&
                this.Standby.Address == key.Standby.Address)
            {
                return true;
            }
            if (this.Primary.Address == key.Standby.Address &&
                this.Standby.Address == key.Primary.Address)
            {
                return true;
            }
            return false;
        }

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
                MalockMessage.WriteStringToStream(writer, this.Address);
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
                    if (!MalockMessage.TryFromStringInReadStream(reader, out s))
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

        internal HostEntry() 
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

        public override string ToString()
        {
            return string.Format("Available({0}), Primary({1}), Standby({2})", this.Available, this.Primary, this.Standby);
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

        public static HostEntry Deserialize(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }
            return Deserialize(new BinaryReader(stream));
        }

        public static HostEntry Deserialize(BinaryReader reader)
        {
            HostEntry entry;
            TryDeserialize(reader, out entry);
            return entry;
        }

        public static bool TryDeserialize(Stream stream, out HostEntry entry)
        {
            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }
            return TryDeserialize(new BinaryReader(stream), out entry);
        }

        public static bool TryDeserialize(BinaryReader reader, out HostEntry entry)
        {
            if (reader == null)
            {
                throw new ArgumentNullException("reader");
            }
            entry = null;
            do
            {
                HostEntry host = new HostEntry();
                if (!host.Primary.Deserialize(reader))
                {
                    return false;
                }
                if (!host.Standby.Deserialize(reader))
                {
                    return false;
                }
                entry = host;
            } while (false);
            return true;
        }
    }
}
