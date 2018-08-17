namespace malock_nns_server6901
{
    using malock.NN;
    using System;

    class Program
    {
        static void Main(string[] args)
        {
            NnsServer server = new NnsServer("malock-nns-server-node-002", 6901, "127.0.0.1:6900");
            server.Run();

            Console.ReadKey(false);
        }
    }
}
