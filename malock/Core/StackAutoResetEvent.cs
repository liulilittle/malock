namespace malock.Core
{
    using System;
    using System.Collections.Generic;
    using System.Threading;

    public class StackAutoResetEvent
    {
        private volatile int slidingsignal = 1; // 原子信号 
        private volatile LinkedList<Action> slidingevents = new LinkedList<Action>();

        public StackAutoResetEvent(bool initial)
        {
            this.slidingsignal = initial ? 1 : 0;
        }

        public void WaitOne(Action state)
        {
            if (state == null)
            {
                throw new ArgumentNullException("state");
            }
            lock (this.slidingevents)
            {
                if (this.CompareExchange(0, 1))
                {
                    state();
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

        public void Set()
        {
            Action state = null;
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
            if (state != null)
            {
                state();
            }
        }
    }
}
