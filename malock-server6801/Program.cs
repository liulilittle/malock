namespace malock_server6801
{
    using System;
    using malock.Server;

    class Program
    {
        static void Main(string[] args)
        {
            MalockServer server = new MalockServer(6801, "127.0.0.1:6800");
            server.Run();

            while (true)
            {
                Console.ReadKey(false);
            }
        }
    }
}
