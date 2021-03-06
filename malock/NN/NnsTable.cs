﻿namespace malock.NN
{
    using global::malock.Common;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;

    internal sealed class NnsTable
    {
        private ConcurrentDictionary<string, Host> entrys = null;
        private ConcurrentDictionary<string, Host> hosts = null;
        private IList<string> buckets = null;

        private readonly object syncobj = new object();

        public object GetSynchronizationObject()
        {
            return this.syncobj;
        }

        public class Host
        {
            public HostEntry Entry
            {
                get;
                internal set;
            }

            public string Identity
            {
                get;
                private set;
            }

            public int Quantity
            {
                get;
                set;
            }

            public bool Available
            {
                get
                {
                    HostEntry entry = this.Entry;
                    if (entry == null)
                    {
                        return false;
                    }
                    return entry.Available;
                }
            }

            internal Host(string identity, HostEntry entry)
            {
                if (identity == null)
                {
                    throw new ArgumentNullException("identity");
                }
                if (identity.Length <= 0)
                {
                    throw new ArgumentOutOfRangeException("identity");
                }
                if (entry == null)
                {
                    throw new ArgumentNullException("entry");
                }
                this.Entry = entry;
                this.Identity = identity;
            }

            internal void Serialize(BinaryWriter writer)
            {
                this.Entry.Serialize(writer);
                MalockMessage.WriteStringToStream(writer, this.Identity);
            }

            internal static Host Deserialize(BinaryReader reader)
            {
                Host host;
                TryDeserialize(reader, out host);
                return host;
            }

            internal static bool TryDeserialize(BinaryReader reader, out Host host)
            {
                host = null;
                HostEntry entry;
                if (!HostEntry.TryDeserialize(reader, out entry))
                {
                    return false;
                }
                string identity;
                if (!MalockMessage.TryFromStringInReadStream(reader, out identity))
                {
                    return false;
                }
                host = new Host(identity, entry);
                return true;
            }

            internal static int DeserializeAll(Stream stream, Action<Host> callback)
            {
                if (stream == null)
                {
                    throw new ArgumentNullException("stream");
                }
                if (callback == null)
                {
                    throw new ArgumentNullException("callback");
                }
                BinaryReader br = new BinaryReader(stream);
                if (!MalockMessage.StreamIsReadable(stream, sizeof(int)))
                {
                    return 0;
                }
                int len = br.ReadInt32();
                int count = 0;
                for (int i = 0; i < len; i++)
                {
                    Host host;
                    if (!Host.TryDeserialize(br, out host))
                    {
                        break;
                    }
                    count++;
                    callback(host);
                }
                return count;
            }

            internal unsafe static void SerializeAll(IEnumerable<Host> hosts, Action<int, Stream> callback)
            {
                SerializeAll(hosts, new MemoryStream(), callback);
            }

            internal unsafe static int SerializeAll(IEnumerable<Host> hosts, Stream stream, Action<int, Stream> callback)
            {
                if (hosts == null)
                {
                    throw new ArgumentNullException("hosts");
                }
                if (stream == null)
                {
                    throw new ArgumentNullException("stream");
                }
                MemoryStream ms = stream as MemoryStream;
                if (ms == null)
                {
                    throw new ArgumentOutOfRangeException("stream");
                }
                if (callback == null)
                {
                    throw new ArgumentNullException("callback");
                }
                BinaryWriter bw = new BinaryWriter(stream);
                bw.Write(0);
                int count = 0;
                foreach (var host in hosts)
                {
                    host.Serialize(bw);
                    count++;
                }
                fixed (byte* pinned = ms.GetBuffer())
                {
                    *(int*)pinned = count;
                }
                callback(count, stream);
                return count;
            }
        }

        public ICollection<Host> GetAllHosts()
        {
            return this.hosts.Values;
        }

        public IEnumerable<string> GetAllKeys()
        {
            return this.hosts.Keys;
        }

        public NnsTable()
        {
            this.entrys = new ConcurrentDictionary<string, Host>();
            this.hosts = new ConcurrentDictionary<string, Host>();
            this.buckets = new List<string>();
        }

        private int QueryHashIndex(string key)
        {
            if (key == null)
            {
                throw new ArgumentNullException("key");
            }
            uint hashcode = unchecked((uint)key.GetHashCode());
            if (hashcode == 0)
            {
                return -1;
            }
            int count = this.hosts.Count;
            if (count <= 0)
            {
                return -1;
            }
            return unchecked((int)((hashcode * hashcode) / 1000 % count));
        }

        public bool SetEntry(string identity, string key, HostEntry entry)
        {
            if (identity == null)
            {
                throw new ArgumentNullException("identity");
            }
            if (identity.Length <= 0)
            {
                throw new ArgumentOutOfRangeException("identity");
            }
            if (key == null)
            {
                throw new ArgumentNullException("key");
            }
            if (key.Length <= 0)
            {
                throw new ArgumentOutOfRangeException("key");
            }
            if (entry == null)
            {
                return false;
            }
            lock (this.syncobj)
            {
                this.Register(identity, entry);
                Host host = null;
                do
                {
                    if (this.entrys.TryGetValue(key, out host))
                    {
                        this.entrys.TryRemove(key, out host);
                    }
                } while (false);
                if (!this.hosts.TryGetValue(identity, out host))
                {
                    return false;
                }
                if (host == null || host.Entry != entry)
                {
                    return false;
                }
                return this.entrys.TryAdd(key, host);
            }
        }

        public HostEntry GetEntry(string key, out string identity)
        {
            identity = null;
            if (key == null)
            {
                throw new ArgumentNullException("key");
            }
            if (key.Length <= 0)
            {
                throw new ArgumentOutOfRangeException("key");
            }
            lock (this.syncobj)
            {
                Host host;
                if (this.entrys.TryGetValue(key, out host))
                {
                    if (host.Available)
                    {
                        identity = host.Identity;
                        return host.Entry;
                    }
                    else if (--host.Quantity < 0)
                    {
                        host.Quantity = 0;
                    }
                    this.entrys.TryRemove(key, out host);
                }
                int position = this.QueryHashIndex(key);
                if (position < 0)
                {
                    return null;
                }
                var ko = this.buckets[position];
                if (ko == null)
                {
                    return null;
                }
                else
                {
                    host = Min(this.hosts.Values, this.hosts[ko], true);
                    do
                    {
                        host.Quantity++;
                    } while (false);
                    this.entrys.TryAdd(key, host);
                }
                identity = host.Identity;
                return host.Entry;
            }
        }

        private static Host Min(IEnumerable<Host> s, Host key, bool mustAvailable)
        {
            if (s == null || key == null)
            {
                return key;
            }
            foreach (Host h in s)
            {
                if (mustAvailable && !h.Available)
                {
                    continue;
                }
                if (key.Quantity > h.Quantity)
                {
                    key = h;
                }
            }
            return key;
        }

        private static int FindIndex<T>(IList<T> buckets, T key)
        {
            if (buckets == null)
            {
                return -1;
            }
            int len = buckets.Count;
            if (len <= 0)
            {
                return -1;
            }
            bool mid = (len & 1) != 0;
            int cycle = len / 2;
            do
            {
                if (mid)
                {
                    int i = len == 1 ? 0 : cycle + 1;
                    T item = buckets[i];
                    if ((item == null && key == null) || item.Equals(key))
                    {
                        return i;
                    }
                }
                for (int i = 0; i < cycle; i++)
                {
                    T item = buckets[i];
                    if ((item == null && key == null) || item.Equals(key))
                    {
                        return i;
                    }
                    int j = len - (i + 1);
                    item = buckets[j];
                    if ((item == null && key == null) || item.Equals(key))
                    {
                        return j;
                    }
                }
            } while (false);
            return -1;
        }

        public bool Unregister(string identity)
        {
            HostEntry entry;
            return this.Unregister(identity, out entry);
        }

        public bool Unregister(string identity, out HostEntry entry)
        {
            if (identity == null)
            {
                throw new ArgumentNullException("key");
            }
            if (identity.Length <= 0)
            {
                throw new ArgumentOutOfRangeException("key");
            }
            entry = null;
            lock (this.syncobj)
            {
                Host host;
                if (!this.hosts.TryRemove(identity, out host))
                {
                    return false;
                }
                else
                {
                    int i = FindIndex(this.buckets, identity);
                    if (i > -1)
                    {
                        this.buckets.RemoveAt(i);
                    }
                    entry = host.Entry;
                }
                return true;
            }
        }

        public bool SetAvailable(string identity, string address, bool available)
        {
            if (identity == null)
            {
                throw new ArgumentNullException("key");
            }
            if (identity.Length <= 0)
            {
                throw new ArgumentOutOfRangeException("key");
            }
            if (address == null || address.Length <= 0)
            {
                return false;
            }
            lock (this.syncobj)
            {
                Host host;
                if (!this.hosts.TryGetValue(identity, out host))
                {
                    return false;
                }
                HostEntry entry = host.Entry;
                var i = entry.Select(address);
                if (i == null)
                {
                    return false;
                }
                else
                {
                    i.Available = available;
                }
                return true;
            }
        }

        public bool IsAvailable(string identity)
        {
            if (identity == null || identity.Length <= 0)
            {
                return false;
            }
            lock (this.syncobj)
            {
                Host host;
                if (!this.hosts.TryGetValue(identity, out host))
                {
                    return false;
                }
                return host.Available;
            }
        }

        public bool ContainsIdentity(string identity)
        {
            if (identity == null || identity.Length <= 0)
            {
                return false;
            }
            lock (this.syncobj)
            {
                return this.hosts.ContainsKey(identity);
            }
        }

        public bool ContainsKey(string key)
        {
            if (key == null || key.Length <= 0)
            {
                return false;
            }
            lock (this.syncobj)
            {
                return this.entrys.ContainsKey(key);
            }
        }

        public Host GetHost(string identity)
        {
            lock (this.syncobj)
            {
                Host host;
                if (!this.hosts.TryGetValue(identity, out host))
                {
                    return null;
                }
                return host;
            }
        }

        public bool Register(string identity, HostEntry entry)
        {
            if (identity == null)
            {
                throw new ArgumentNullException("key");
            }
            if (identity.Length <= 0)
            {
                throw new ArgumentOutOfRangeException("key");
            }
            if (entry == null)
            {
                throw new ArgumentNullException("entry");
            }
            lock (this.syncobj)
            {
                if (!this.hosts.TryAdd(identity, new Host(identity, entry)))
                {
                    Host host;
                    if (this.hosts.TryGetValue(identity, out host))
                    {
                        if (host.Entry == entry)
                        {
                            return true;
                        }
                    }
                    return false;
                }
                else
                {
                    this.buckets.Add(identity);
                }
                return true;
            }
        }
    }
}
