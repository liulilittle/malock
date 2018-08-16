namespace malock.NN
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;

    public class NnsTable
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

            internal Host(HostEntry entry)
            {
                if (entry == null)
                {
                    throw new ArgumentNullException("entry");
                }
                this.Entry = entry;
            }
        }

        public IEnumerable<Host> GetAllHosts()
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

        public bool BindEntry(string key, HostEntry entry)
        {
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
                Host host;
                if (!this.entrys.TryGetValue(key, out host))
                {
                    return false;
                }
                else
                {
                    if (entry == host.Entry)
                    {
                        return true;
                    }
                    this.entrys[key] = new Host(entry);
                }
                return true;
            }
        }

        public HostEntry GetEntry(string key)
        {
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
                if (!this.hosts.TryAdd(identity, new Host(entry)))
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
