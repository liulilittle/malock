namespace malock.Server
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using MalockTaskTable = System.Collections.Concurrent.ConcurrentDictionary<string, System.Collections.Generic.LinkedList<MalockTaskInfo>>;
    using Runnable = global::malock.Client.EventWaitHandle;

    public sealed class MalockTaskPoll
    {
        private readonly ConcurrentDictionary<MalockTaskType, MalockTaskTable> tables = null;
        private readonly MalockEngine engine = null;
        private readonly Thread workthread = null;

        public MalockTaskPoll(MalockEngine engine)
        {
            if (engine == null)
            {
                throw new ArgumentNullException("engine");
            }
            this.tables = new ConcurrentDictionary<MalockTaskType, MalockTaskTable>();
            this.engine = engine;
            this.workthread = Runnable.Run(()=> 
            {
                while (true)
                {
                    foreach (var kv in this.tables)
                    {
                        this.Handle(kv.Key, kv.Value);
                    }
                    Thread.Sleep(1);
                }
            });
        }

        private void Handle(MalockTaskType type, MalockTaskTable tables)
        {
            foreach (var kv in tables)
            {
                LinkedList<MalockTaskInfo> tasks = kv.Value;
                MalockTaskInfo info = null;
                LinkedListNode<MalockTaskInfo> node = null;
                lock (tasks)
                {
                    node = tasks.First;
                    if (node == null)
                    {
                        continue;
                    }
                    info = node.Value;
                }
                bool success = false;
                Stopwatch sw = info.Stopwatch;
                if (sw != null && info.Timeout != -1 && sw.ElapsedMilliseconds > info.Timeout)
                {
                    success = this.engine.Timeout(info);
                }
                else
                {
                    switch (type)
                    {
                        case MalockTaskType.kEnter:
                            success = this.engine.Enter(info);
                            break;
                        case MalockTaskType.kExit:
                            success = this.engine.Exit(info);
                            break;
                        case MalockTaskType.kAbort:
                            success = this.engine.Abort(info);
                            break;
                        case MalockTaskType.kGetAllInfo:
                            success = this.engine.GetAllInfo(info);
                            break;
                    }
                }
                if (success)
                {
                    lock (tasks)
                    {
                        if (node.List != null)
                        {
                            tasks.Remove(node);
                        }
                    }
                }
            }
        }

        public bool Remove(string identity)
        {
            lock (this.tables)
            {
                LinkedList<MalockTaskInfo> tasks;
                foreach (MalockTaskTable tables in this.tables.Values)
                {
                    tables.TryRemove(identity, out tasks);
                }
                return true;
            }
        }

        private MalockTaskTable GetTable(MalockTaskType type)
        {
            lock (this.tables)
            {
                ConcurrentDictionary<string, LinkedList<MalockTaskInfo>> table;
                if (!this.tables.TryGetValue(type, out table))
                {
                    table = new ConcurrentDictionary<string, LinkedList<MalockTaskInfo>>();
                    this.tables.TryAdd(type, table);
                }
                return table;
            }
        }

        private MalockTaskTable RemoveTable(MalockTaskType type)
        {
            lock (this.tables)
            {
                MalockTaskTable table;
                this.tables.TryRemove(type, out table);
                return table;
            }
        }

        public void Add(MalockTaskInfo info)
        {
            LinkedList<MalockTaskInfo> tasks;
            string identity = info.Identity;
            lock (this.tables)
            {
                MalockTaskTable tables = this.GetTable(info.Type);
                if (!tables.TryGetValue(identity, out tasks))
                {
                    tasks = new LinkedList<MalockTaskInfo>();
                    tables.TryAdd(identity, tasks);
                }
                lock (tasks)
                {
                    tasks.AddLast(info);
                }
            }
        }
    }
}
