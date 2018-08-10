namespace malock.Client
{
    using System;

    public class MalockSocketException : Exception
    {
        public int ErrorCode
        {
            get;
            private set;
        }

        public MalockSocketException(int errorCode, string message) : base(message)
        {
            this.ErrorCode = errorCode;
        }
    }
}
