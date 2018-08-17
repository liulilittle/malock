namespace malock.Server
{
    using global::malock.Core;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using Monitor = global::System.Threading.Monitor;
    /// <summary>
    /// I used to cover the sky light with my hands Wandering the universe is too primitive 
    /// I used to be in heaven and earth and everything to say you're alright
    /// </summary>
    internal class MalockTable
    {
        public class LockerInfo
        {
            public AtomicBoolean Locker
            {
                get;
                private set;
            }

            public string Identity
            {
                get;
                set;
            }

            public object Tag
            {
                get;
                set;
            }

            public object State
            {
                get;
                set;
            }

            public bool Available
            {
                get
                {
                    return this.Identity == null;
                }
            }

            public string Key
            {
                get;
                private set;
            }

            public LockerInfo(string key)
            {
                this.Key = key;
                this.Locker = new AtomicBoolean(false);
            }
        }

        private IDictionary<string, LockerInfo> lockInfos = new ConcurrentDictionary<string, LockerInfo>();
        private IDictionary<string, ISet<string>> mapKeys = new ConcurrentDictionary<string, ISet<string>>();
        private readonly object syncobj = new object();

        private static readonly string[] EmptryKeyNames = new string[0];

        public object GetSynchronizationObject()
        {
            return this.syncobj;
        }

        protected virtual bool AllocEnterKey(string key)
        {
            if (key == null)
            {
                throw new ArgumentNullException("key");
            }
            if (key.Length <= 0)
            {
                throw new ArgumentOutOfRangeException("key");
            }
            lock (lockInfos)
            {
                if (lockInfos.ContainsKey(key))
                {
                    return true;
                }
                else
                {
                    lockInfos.Add(key, new LockerInfo(key));
                }
                return true;
            }
        }

        protected virtual bool FreeEnterKey(string key)
        {
            if (key == null)
            {
                throw new ArgumentNullException("key");
            }
            if (key.Length <= 0)
            {
                throw new ArgumentOutOfRangeException("key");
            }
            lock (lockInfos)
            {
                return lockInfos.Remove(key);
            }
        }

        public virtual bool AllocKeyCollection(string identity)
        {
            if (identity == null)
            {
                throw new ArgumentNullException("identity");
            }
            lock (mapKeys)
            {
                ISet<string> keys;
                if (!mapKeys.TryGetValue(identity, out keys))
                {
                    keys = new HashSet<string>();
                    mapKeys.Add(identity, keys);
                }
                return keys != null;
            }
        }

        public virtual bool FreeKeyCollection(string identity, out string[] keys)
        {
            if (identity == null)
            {
                throw new ArgumentNullException("identity");
            }
            return this.InternalExitByIdentity(identity, out keys, true);
        }

        private ISet<string> GetAllKeyByIdentity(string identity)
        {
            ISet<string> keys;
            mapKeys.TryGetValue(identity, out keys);
            return keys;
        }

        public IEnumerable<string> GetAllKey()
        {
            return lockInfos.Keys;
        }

        public IEnumerable<LockerInfo> GetAllLocker()
        {
            return lockInfos.Values;
        }

        public bool ContainsKey(string key)
        {
            return lockInfos.ContainsKey(key);
        }

        public virtual bool IsEnter(string key)
        {
            return this.InternalIsEnter(key, null, true);
        }

        public virtual bool IsEnter(string key, string identity)
        {
            return this.InternalIsEnter(key, identity, false);
        }

        private bool InternalIsEnter(string key, string identity, bool igrone)
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }
            lock (this.syncobj)
            {
                LockerInfo info = GetLockerInfo(key);
                if (info == null)
                {
                    return false;
                }
                lock (info)
                {
                    if (igrone)
                    {
                        return !info.Available;
                    }
                    return info.Identity == identity;
                }
            }
        }

        protected virtual LockerInfo GetLockerInfo(string key)
        {
            LockerInfo info;
            if (!lockInfos.TryGetValue(key, out info))
            {
                return null;
            }
            return info;
        }

        public bool Enter(string key, string identity)
        {
            string exitIdentity;
            return this.Enter(key, identity, out exitIdentity);
        }

        public bool Enter(string key, string enterIdentity, out string exitIdentity)
        {
            return this.Enter(key, enterIdentity, null, out exitIdentity);
        }

        public virtual bool Enter(string key, string enterIdentity, object state, out string exitIdentity)
        {
            if (enterIdentity == null)
            {
                throw new ArgumentNullException("enterIdentity");
            }
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
                exitIdentity = null;
                if (!this.AllocEnterKey(key))
                {
                    return false;
                }
                LockerInfo info;
                if (!lockInfos.TryGetValue(key, out info))
                {
                    return false;
                }
                AtomicBoolean locker = null;
                lock (info)
                {
                    locker = info.Locker;
                }
                bool localTaken = locker.CompareExchange(false, true);
                if (localTaken)
                {
                    ISet<string> keys = GetAllKeyByIdentity(enterIdentity);
                    do
                    {
                        bool addKeyToKeys = false;
                        if (keys == null)
                        {
                            addKeyToKeys = false;
                        }
                        else
                        {
                            lock (keys)
                            {
                                addKeyToKeys = keys.Add(key);
                            }
                        }
                        if (!addKeyToKeys)
                        {
                            localTaken = false;
                            locker.CompareExchange(true, false);
                        }
                        else
                        {
                            lock (info)
                            {
                                exitIdentity = info.Identity;
                                info.Identity = enterIdentity;
                                info.State = state;
                            }
                        }
                    } while (false);
                }
                return localTaken;
            }
        }

        public bool Exit(string key, string identity)
        {
            object state;
            return this.Exit(key, identity, out state);
        }

        public virtual bool Exit(string key, out object state)
        {
            return this.InternalExitEx(key, null, out state, true);
        }

        public virtual bool Exit(string key)
        {
            object state;
            return this.Exit(key, out state);
        }

        private bool InternalExitEx(string key, string identity, out object state, bool ignoreIdentity)
        {
            if (key == null)
            {
                throw new ArgumentNullException("key");
            }
            if (key.Length <= 0)
            {
                throw new ArgumentOutOfRangeException("key");
            }
            if (!ignoreIdentity)
            {
                if (identity == null)
                {
                    throw new ArgumentNullException("identity");
                }
                if (identity.Length <= 0)
                {
                    throw new ArgumentOutOfRangeException("identity");
                }
            }
            return this.InternalExitByKey(key, identity, out state, ignoreIdentity);
        }

        public virtual bool Exit(string key, string identity, out object state)
        {
            return this.InternalExitEx(key, identity, out state, false);
        }

        private bool InternalExitByKey(string key, string identity, out object state, bool ignoreIdentity)
        {
            lock (this.syncobj)
            {
                state = null;
                LockerInfo info;
                if (!lockInfos.TryGetValue(key, out info))
                {
                    return false;
                }
                lock (info)
                {
                    ISet<string> keys = string.IsNullOrEmpty(identity) ? null : GetAllKeyByIdentity(identity);
                    try
                    {
                        if (keys != null)
                        {
                            Monitor.Enter(keys);
                        }
                        AtomicBoolean locker = info.Locker;
                        if (!ignoreIdentity && info.Identity != identity)
                        {
                            return false;
                        }
                        else
                        {
                            if (keys != null)
                            {
                                keys.Remove(key);
                            }
                            state = info.State;
                            info.Identity = null;
                        }
                        locker.CompareExchange(true, false);
                    }
                    finally
                    {
                        if (keys != null)
                        {
                            Monitor.Exit(keys);
                        }
                    }
                }
            }
            return true;
        }

        public virtual bool Exit(string identity, out string[] keys)
        {
            if (identity == null)
            {
                throw new ArgumentNullException("identity");
            }
            return this.InternalExitByIdentity(identity, out keys, false);
        }

        private bool InternalExitByIdentity(string identity, out string[] keys, bool freeKeySets)
        {
            lock (this.syncobj)
            {
                keys = MalockTable.EmptryKeyNames;
                ISet<string> totkeys = this.GetAllKeyByIdentity(identity);
                if (totkeys == null)
                {
                    return false;
                }
                lock (totkeys)
                {
                    if (totkeys.Count <= 0)
                    {
                        return false;
                    }
                    keys = new string[totkeys.Count];
                    int index = 0;
                    while (totkeys.Count > 0)
                    {
                        if (index >= keys.Length)
                        {
                            break;
                        }
                        using (IEnumerator<string> enumerator = totkeys.GetEnumerator())
                        {
                            if (!enumerator.MoveNext())
                            {
                                break;
                            }
                            string keyid = enumerator.Current;
                            keys[index++] = keyid;
                            this.Exit(keyid, identity);
                        }
                    }
                    if (freeKeySets)
                    {
                        mapKeys.Remove(identity);
                    }
                }
                return true;
            }
        }
    }
}
