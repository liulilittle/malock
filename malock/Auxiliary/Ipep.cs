namespace malock.Auxiliary
{
    using System;
    using System.Net;

    public static class Ipep
    {
        public static IPEndPoint ToIpep(string address)
        {
            if (address == null)
            {
                throw new ArgumentNullException("address");
            }
            if (address.Length <= 0)
            {
                throw new ArgumentException("address");
            }
            int index = address.IndexOf(':');
            if (!(index > -1))
            {
                throw new ArgumentOutOfRangeException("address");
            }
            string host = address.Substring(0, index++);
            int port = 0;
            if (!int.TryParse(address.Substring(index), out port))
            {
                throw new ArgumentOutOfRangeException("address");
            }
            return new IPEndPoint(IPAddress.Parse(host), port);
        }

        public static string ToIpepString(string host, int port)
        {
            return string.Format("{0}:{1}", host, port);
        }

        public static string GetHostName(string address)
        {
            if (address == null)
            {
                throw new ArgumentNullException("address");
            }
            if (address.Length <= 0)
            {
                throw new ArgumentException("address");
            }
            int index = address.IndexOf(':');
            if (!(index > -1))
            {
                throw new ArgumentOutOfRangeException("address");
            }
            return address.Substring(0, index++);
        }

        public static int GetPort(string address)
        {
            if (address == null)
            {
                throw new ArgumentNullException("address");
            }
            if (address.Length <= 0)
            {
                throw new ArgumentException("address");
            }
            int index = address.IndexOf(':');
            if (!(index > -1))
            {
                throw new ArgumentOutOfRangeException("address");
            }
            int port = 0;
            if (!int.TryParse(address.Substring(++index), out port))
            {
                throw new ArgumentOutOfRangeException("address");
            }
            return port;
        }
    }
}
