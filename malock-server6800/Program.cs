namespace malock_server6800
{
    using System;
    using malock.Server;

    class Program
    {
        static void Main(string[] args)
        {
            MalockServer server = new MalockServer(6800, "127.0.0.1:6801");
            server.Run();

            while (true)
            {
                Console.ReadKey(false);
            }
        }
    }
}
