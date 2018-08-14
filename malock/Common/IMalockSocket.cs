namespace malock.Common
{
    public interface IMalockSocket
    {
        object Tag
        {
            get;
            set;
        }

        void Abort();

        bool Available
        {
            get;
        }

        bool Send(byte[] buffer, int ofs, int len);
    }
}
