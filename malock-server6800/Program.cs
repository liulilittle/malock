namespace malock_server6800
{
    using System;
    using malock.Server;

    class Program
    {
        static void Main(string[] args)
        {
            MalockConfiguration configuration = new MalockConfiguration("malock-server-node-001",
                   6800, "127.0.0.1:6801", "127.0.0.1:6900", "127.0.0.1:6901");

            MalockServer server = new MalockServer(configuration);
            server.Run();

            while (true)
            {
                Console.ReadKey(false);
            }
        }
    }
}
