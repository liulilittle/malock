namespace malock.Core
{
    using System;
    using System.Collections.Generic;
    using System.Threading;

    public class AsyncAutoResetEvent
    {
        private volatile int slidingsignal = 1; // 原子信号 
        private volatile LinkedList<Action<bool>> slidingevents = new LinkedList<Action<bool>>();

        public AsyncAutoResetEvent(bool initial)
        {
            this.slidingsignal = initial ? 1 : 0;
        }

        public void WaitOne(Action<bool> state)
        {
            if (state == null)
            {
                throw new ArgumentNullException("state");
            }
            lock (this.slidingevents)
            {
                if (this.CompareExchange(0, 1))
                {
                    state(true);
                }
                else
                {
                    this.slidingevents.AddLast(state);
                }
            }
        }

        private bool CompareExchange(int value, int compared)
        {
            Thread.MemoryBarrier();
            return Interlocked.CompareExchange(ref this.slidingsignal, value, compared) == compared;
        }

        private bool SetEvent(out Action<bool> state)
        {
            state = null;
            lock (this.slidingevents)
            {
                var first = this.slidingevents.First;
                if (first != null)
                {
                    this.slidingevents.Remove(first);
                    state = first.Value;
                }
            }
            this.CompareExchange(1, 0);
            return state != null;
        }

        public int Set(bool state)
        {
            int count = 0;
            if (!state)
            {
                return count;
            }
            Action<bool> callback;
            while (this.SetEvent(out callback))
            {
                count++;
                if (callback != null)
                {
                    callback(false);
                }
            }
            return count;
        }
    }
}
