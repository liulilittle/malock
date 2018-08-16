namespace malock_server6801
{
    using System;
    using malock.Server;

    class Program
    {
        static void Main(string[] args)
        {
            MalockConfiguration configuration = new MalockConfiguration("malock-server-node-002",
                6801, "malock-server-node-nns001", "127.0.0.1:6800", "127.0.0.1:6900", "127.0.0.1:6901");

            MalockServer server = new MalockServer(configuration);
            server.Run();

            while (true)
            {
                Console.ReadKey(false);
            }
        }
    }
}
