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
        static void UT_NnsTable()
        {
            NnsTable nnsTable = new NnsTable();
            HostEntry host1 = new HostEntry();
            host1.Primary.Available = true;
            host1.Primary.Address = "127.0.0.1:6800";
            host1.Standby.Available = true;
            host1.Standby.Address = "127.0.0.1:6801";

            HostEntry host2 = new HostEntry();
            host2.Primary.Available = true;
            host2.Primary.Address = "127.0.0.1:7800";
            host2.Standby.Available = true;
            host2.Standby.Address = "127.0.0.1:7801";

            nnsTable.Register("0", host1);
            nnsTable.Register("1", host2);
            // nnsTable.Unregister("0", out host);
            HostEntry entry1 = nnsTable.GetEntry("1");
            HostEntry entry2 = nnsTable.GetEntry("2");
        }

        static void Main(string[] args)
        {
            UT_NnsTable();
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
