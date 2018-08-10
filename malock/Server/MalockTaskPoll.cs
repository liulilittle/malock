namespace malock.Server
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using Runnable = global::malock.Client.EventWaitHandle;

    public class MalockTaskPoll
    {
        private readonly ConcurrentDictionary<string, IList<MalockTaskInfo>> malockTasks = null;
        private readonly MalockEngine malockEngine = null;
        private readonly Thread malockWorkThread = null;

        public MalockTaskPoll(MalockEngine engine)
        {
            if (engine == null)
            {
                throw new ArgumentNullException("engine");
            }
            this.malockTasks = new ConcurrentDictionary<string, IList<MalockTaskInfo>>();
            this.malockEngine = engine;
            this.malockWorkThread = Runnable.Run(()=> 
            {
                while (true)
                {
                    this.Handle();
                    Thread.Sleep(1);
                }
            });
        }

        private void Handle()
        {
            foreach (var kv in this.malockTasks)
            {
                IList<MalockTaskInfo> tasks = kv.Value;
                MalockTaskInfo info = null;
                lock (tasks)
                {
                    if (tasks.Count <= 0)
                    {
                        continue;
                    }
                    else
                    {
                        info = tasks[0];
                    }
                }
                bool success = false;
                Stopwatch sw = info.Stopwatch;
                if (sw != null && info.Timeout != -1 && sw.ElapsedMilliseconds > info.Timeout)
                {
                    success = this.malockEngine.Timeout(info);
                }
                else
                {
                    switch (info.Type)
                    {
                        case MalockTaskType.kEnter:
                            success = this.malockEngine.Enter(info);
                            break;
                        case MalockTaskType.kExit:
                            success = this.malockEngine.Exit(info);
                            break;
                        case MalockTaskType.kAbort:
                            success = this.malockEngine.Abort(info);
                            break;
                    }
                }
                if (success)
                {
                    lock (tasks)
                    {
                        if (tasks.Count > 0)
                        {
                            tasks.RemoveAt(0);
                        }
                    }
                }
            }
        }

        public bool Remove(string identity)
        {
            lock (this.malockTasks)
            {
                IList<MalockTaskInfo> tasks;
                return this.malockTasks.TryRemove(identity, out tasks);
            }
        }

        public void Add(string identity, MalockTaskInfo info)
        {
            this.Insert(-1, identity, info);
        }

        public void Insert(int position, string identity, MalockTaskInfo info)
        {
            lock (this.malockTasks)
            {
                IList<MalockTaskInfo> tasks;
                if (!this.malockTasks.TryGetValue(identity, out tasks))
                {
                    tasks = new List<MalockTaskInfo>();
                    this.malockTasks.TryAdd(identity, tasks);
                }
                lock (tasks)
                {
                    if (position < 0)
                    {
                        tasks.Add(info);
                    }
                    else
                    {
                        tasks.Insert(position, info);
                    }
                }
            }
        }
    }
}
