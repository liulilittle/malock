namespace malock_client
{
    using malock;
    using malock.Client;
    using malock.NN;
    using System;
    using System.Diagnostics;
    using Interlocked = System.Threading.Interlocked;

    class Program
    {
        static void NN_Test()
        {
            NnsClient nns = new NnsClient("malock-client-node-001", "127.0.0.1:6900", "127.0.0.1:6901").Run();
            nns.Ready += delegate
            {
                int count = 0;
                for (int i = 0; i < 4; i++)
                {
                    EventWaitHandle.Run(() =>
                    {
                        for (int j = 0; j < 1000; j++)
                        {
                            nns.QueryHostEntryAsync("test013", (error, entry) =>
                            {
                                Console.WriteLine("count {0}, {1}", Interlocked.Increment(ref count), entry);
                            });
                        }
                    });
                }
            };
        }

        static void Main(string[] args)
        {
            NN_Test();

            Console.ReadKey(false);

            MalockClient malock = Malock.NewClient("test013", "127.0.0.1:6800", "127.0.0.1:6801").Run();
            malock.Ready += delegate
            {
                Monitor m = new Monitor("OMFG", malock);
                int num = 0;
                for (int i = 0; i < 5; i++)
                {
                    EventWaitHandle.Run(() =>
                    {
                        for (int k = 0; k < 2000; k++)
                        {
                            try
                            {
                                Stopwatch sw = new Stopwatch();
                                sw.Start();
                                if (m.TryEnter())
                                {
                                    sw.Stop();
                                    Console.WriteLine("n: {0}, time: {1}ms", Interlocked.Increment(ref num), sw.ElapsedMilliseconds);
                                    m.Exit();
                                }
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine(e.Message);
                            }
                        }
                    });
                }
            };
            Console.ReadKey(false);
        }
    }
}
