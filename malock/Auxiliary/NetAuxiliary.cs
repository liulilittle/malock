namespace malock.Auxiliary
{
    using malock.Client;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Net;
    using System.Net.NetworkInformation;
    using System.Net.Sockets;
    using System.Threading;

    public class NetAuxiliary
    {
        private static readonly string[] EmptryStringArray = new string[0];

        public static IPAddress QueryActiveIPAddress(int timeout = 300, string hostNameOrAddress = "www.baidu.com")
        {
            if (string.IsNullOrEmpty(hostNameOrAddress))
            {
                return null;
            }
            using (AutoResetEvent events = new AutoResetEvent(false))
            {
                IPAddress address = null;
                Stopwatch stopwatch = new Stopwatch();
                bool istimedout = false;
                try
                {
                    Dns.BeginGetHostAddresses(hostNameOrAddress, (ar) =>
                    {
                        if (istimedout)
                        {
                            return;
                        }
                        try
                        {
                            IPAddress[] addresses = Dns.EndGetHostAddresses(ar);
                            if (addresses != null && addresses.Length > 0)
                            {
                                foreach (IPAddress i in addresses)
                                {
                                    if (i.AddressFamily == AddressFamily.InterNetwork)
                                    {
                                        address = i;
                                        break;
                                    }
                                }
                                events.Set();
                            }
                        }
                        catch (Exception)
                        {

                        }
                    }, null);
                }
                catch (Exception)
                {
                    return null;
                }
                do
                {
                    stopwatch.Start();
                    if (!events.WaitOne(timeout))
                    {
                        istimedout = true;
                        return null;
                    }
                    else if (address == null)
                    {
                        return null;
                    }
                    stopwatch.Stop();
                } while (false);
                timeout -= Convert.ToInt32(stopwatch.ElapsedMilliseconds);
                if (timeout <= 0)
                {
                    return null;
                }
                using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                {
                    try
                    {
                        socket.NoDelay = true;
                        socket.BeginConnect(new IPEndPoint(address, 443), (ar) =>
                        {
                            if (istimedout)
                            {
                                return;
                            }
                            try
                            {
                                socket.EndConnect(ar);
                                EndPoint localEP = socket.LocalEndPoint;
                                if (localEP != null)
                                {
                                    IPEndPoint ipep = (IPEndPoint)localEP;
                                    address = ipep.Address;
                                    events.Set();
                                }
                            }
                            catch (Exception)
                            {
                                address = null;
                            }
                        }, null);
                    }
                    catch (Exception)
                    {
                        return null;
                    }
                    do
                    {
                        if (!events.WaitOne(timeout))
                        {
                            istimedout = true;
                            return null;
                        }
                    } while (false);
                    MalockSocket.Close(socket);
                }
                return address;
            }
        }

        public static IPAddress GetActiveIPAddress()
        {
            IPAddress[] address = GetActiveIPAddresss();
            return address.FirstOrDefault(i => i != IPAddress.Any || i != IPAddress.Broadcast || i != IPAddress.Loopback || i != IPAddress.None); 
        }

        public static IPAddress[] GetActiveIPAddresss()
        {
            List<IPAddress> results = new List<IPAddress>();
            ISet<IPAddress> sets = new HashSet<IPAddress>();
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up)
                {
                    continue;
                }
                IPInterfaceProperties properties = ni.GetIPProperties();
                UnicastIPAddressInformationCollection addresses = properties.UnicastAddresses;
                foreach (UnicastIPAddressInformation address in addresses)
                {
                    IPAddress addr = address.Address;
                    if (addr.AddressFamily != AddressFamily.InterNetwork)
                    {
                        continue;
                    }
                    if (sets.Add(addr))
                    {
                        results.Add(addr);
                    }
                }
            }
            return results.ToArray();
        }

        public static string[] GetMacAddresss(string etherAddress)
        {
            return InternalQueryAddress(etherAddress, GetMacAddresss);
        }

        private static string[] InternalQueryAddress(string etherAddress, Func<IPAddress, string[]> queryValue)
        {
            if (queryValue == null)
            {
                throw new ArgumentNullException("queryValue");
            }
            IPAddress address;
            try
            {
                if (!IPAddress.TryParse(etherAddress, out address))
                {
                    return EmptryStringArray;
                }
                if (address.AddressFamily != AddressFamily.InterNetwork)
                {
                    return EmptryStringArray;
                }
            }
            catch (Exception)
            {
                return EmptryStringArray;
            }
            return queryValue(address);
        }

        public static string[] GetNetworkInstances(string etherAddress)
        {
            return InternalQueryAddress(etherAddress, GetNetworkInstances);
        }

        public static string[] GetNetworkInstances(IPAddress ether = default(IPAddress))
        {
            List<string> rs = new List<string>();
            ISet<string> ss = new HashSet<string>();
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ether == null || ni.GetIPProperties().UnicastAddresses.FirstOrDefault(i => i.Address == ether) != null)
                {
                    if (ss.Add(ni.Id))
                    {
                        rs.Add(ni.Id);
                    }
                    if (ether != null)
                    {
                        break;
                    }
                }
            }
            return rs.ToArray();
        }

        public static string[] GetMacAddresss(IPAddress ether = default(IPAddress))
        {
            List<string> rs = new List<string>();
            ISet<string> ss = new HashSet<string>();
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ether == null || ni.GetIPProperties().UnicastAddresses.FirstOrDefault(i => i.Address == ether) != null)
                {
                    string mac = BitConverter.ToString(ni.GetPhysicalAddress().GetAddressBytes());
                    if (ss.Add(mac))
                    {
                        rs.Add(mac);
                    }
                    if (ether != null)
                    {
                        break;
                    }
                }
            }
            return rs.ToArray();
        }
    }
}
