namespace malock_nns_server6900
{
    using malock.NN;
    using System;

    class Program
    {
        static void Main(string[] args)
        {
            NnsServer server = new NnsServer(6900, "127.0.0.1:6901");
            server.Run();

            Console.ReadKey(false);
        }
    }
}
