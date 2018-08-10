namespace malock.Server
{
    public interface IMalockSender
    {
        bool Send(byte[] buffer, int ofs, int len);
    }
}
