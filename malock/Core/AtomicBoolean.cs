namespace malock.Core
{
    using System.Threading;

    public class AtomicBoolean
    {
        private volatile int s = 0x00;

        public AtomicBoolean(bool initialValue)
        {
            this.s = initialValue ? 1 : 0;
        }

        public bool Get() => Interlocked.CompareExchange(ref s, 0, 0) != 0x00;

        public void Set(bool value) => Interlocked.Exchange(ref s, value ? 0x01 : 0x00);

        public bool CompareAndSet(bool expect, bool update) => Interlocked.CompareExchange(ref s, update ? 1 : 0, expect ? 1 : 0) != 0x00;

        public bool CompareExchange(bool comparand, bool value)
        {
            int compared = comparand ? 1 : 0;
            int newvalue = value ? 1 : 0;
            return Interlocked.CompareExchange(ref s, newvalue, compared) == compared;
        }
    }
}
