namespace malock.Server
{
    using System;
    using System.Diagnostics;

    internal sealed class MalockTaskInfo : EventArgs
    {
        public MalockTaskType Type { get; set; }

        public int Timeout { get; set; }

        public string Key { get; set; }

        public string Identity { get; set; }

        public int Sequence { get; set; }

        public Stopwatch Stopwatch { get; set; }

        public MalockSocket Socket { get; set; }

        internal MalockTaskInfo()
        {

        }
    }
}
