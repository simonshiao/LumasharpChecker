using System;
using System.IO;
using System.Threading;

static class FaultHost
{
    static void Main()
    {
        Console.ReadLine();
        if (File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fault-mode.txt")) == "hang") Thread.Sleep(60000);
        else
        {
            Console.Error.Write(new string('x', 100000));
            Console.WriteLine("{\"requestId\":\"stale-request\",\"exit\":0}");
        }
    }
}
