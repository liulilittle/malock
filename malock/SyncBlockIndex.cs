namespace malock
{
    using global::malock.Client;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;

    public abstract class SyncBlockIndex : EventArgs, IEventWaitHandle
    {
        private static readonly ConcurrentDictionary<string, SyncBlockIndex> g_blocks = 
            new ConcurrentDictionary<string, SyncBlockIndex>();
        private static readonly object g_syncobj = new object();

        internal SyncBlockIndex(string key, MalockClient malock)
        {
            Exception exception = null;
            lock (g_syncobj)
            {
                if (g_blocks.ContainsKey(key))
                {
                    exception = new InvalidOperationException("Do not allow duplicate instances of locks of the same key");
                }
            }
            if (exception != null)
            {
                throw exception;
            }
            this.Handle = this.NewWaitHandle(key, malock);
        }

        public virtual string Key
        {
            get
            {
                return this.Handle.Key;
            }
        }

        public static TSyncBlockIndex Get<TSyncBlockIndex>(string key)
            where TSyncBlockIndex : SyncBlockIndex
        {
            return Get(key) as TSyncBlockIndex;
        }

        public static SyncBlockIndex Get(string key)
        {
            if (key == null)
            {
                throw new ArgumentNullException("key");
            }
            if (key.Length <= 0)
            {
                throw new ArgumentOutOfRangeException("key");
            }
            lock (g_syncobj)
            {
                SyncBlockIndex sync;
                if (!g_blocks.TryGetValue(key, out sync))
                {
                    sync = null;
                }
                return sync;
            }
        }

        public static bool IsUseLock(string key)
        {
            return Get(key) != null;
        }

        protected static TSyncBlockIndex NewOrGet<TSyncBlockIndex>(string key, MalockClient malock, 
            Func<TSyncBlockIndex> constructor)
            where TSyncBlockIndex : SyncBlockIndex
        {
            if (constructor == null)
            {
                throw new ArgumentNullException("constructor");
            }
            if (malock == null)
            {
                throw new ArgumentNullException("malock");
            }
            if (key == null)
            {
                throw new ArgumentNullException("key");
            }
            if (key.Length <= 0)
            {
                throw new ArgumentOutOfRangeException("key");
            }
            Exception exception = null;
            SyncBlockIndex block = null;
            lock (g_syncobj)
            {
                if (!g_blocks.TryGetValue(key, out block))
                {
                    try
                    {
                        block = constructor(); // BUG
                        if (block == null)
                        {
                            exception = new ArgumentOutOfRangeException("The calling type constructor returns a null value that is not allowed");
                        }
                        else
                        {
                            g_blocks.TryAdd(key, block);
                        }
                    }
                    catch (Exception e)
                    {
                        exception = e;
                    }
                }
            }
            if (exception != null)
            {
                throw exception;
            }
            return block as TSyncBlockIndex;
        }

        public static IEnumerable<SyncBlockIndex> GetAllUseLock()
        {
            return g_blocks.Values;
        }

        protected abstract EventWaitHandle NewWaitHandle(string key, MalockClient malock);

        public virtual EventWaitHandle Handle
        {
            get;
            private set;
        }
    }
}
