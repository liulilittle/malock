namespace malock.Core
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Reflection;

    public class MixEvent<T> : IEnumerable<T>
    {
        private readonly Dictionary<MethodInfo, LinkedListNode<T>> tables = new Dictionary<MethodInfo, LinkedListNode<T>>();
        private readonly LinkedList<T> buckets = new LinkedList<T>();
        private readonly object syncobj = new object();

        public MixEvent()
        {

        }

        public bool Add(T d)
        {
            Delegate dd = d as Delegate;
            if (dd == null)
            {
                throw new ArgumentOutOfRangeException("d");
            }
            lock (this.syncobj)
            {
                var m = dd.Method;
                if (tables.ContainsKey(m))
                {
                    return false;
                }
                var n = buckets.AddLast(d);
                tables.Add(m, n);
            }
            return true;
        }

        public IEnumerator<T> GetEnumerator()
        {
            return buckets.GetEnumerator();
        }

        public bool Remove(T d)
        {
            Delegate dd = d as Delegate;
            if (dd == null)
            {
                throw new ArgumentOutOfRangeException("d");
            }
            LinkedListNode<T> n = null;
            lock (this.syncobj)
            {
                var m = dd.Method;
                if (!tables.TryGetValue(m, out n))
                {
                    return false;
                }
                else
                {
                    tables.Remove(m);
                    buckets.Remove(n);
                }
            }
            return true;
        }

        public void Invoke(Action<T> callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException("callback");
            }
            var current = buckets.First;
            do
            {
                T value;
                if (current == null)
                {
                    break;
                }
                value = current.Value;
                if (value == null)
                {
                    break;
                }
                else
                {
                    callback(value);
                }
                current = current.Next;
            } while (current != null);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}
