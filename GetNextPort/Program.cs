using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace GetNextPort
{
    class Program
    {
        private static readonly int _threads = Environment.ProcessorCount;
        private static readonly Stopwatch _stopwatch = Stopwatch.StartNew();

        static void Main(string[] args)
        {
            var threads = new Thread[_threads];
            for (var i=0; i < _threads; i++)
            {
                var j = i;
                threads[i] = new Thread(() =>
                {
                    while (true)
                    {
                        TestPort(j);
                    }
                });
                threads[i].Start();
            }

            foreach (var thread in threads)
            {
                thread.Join();
            }
        }

        private static void TestPort(int id)
        {
            var port = GetNextPort();

            Console.WriteLine($"[{_stopwatch.Elapsed}] [{id}] Selected Port: {port}");

            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                try
                {
                    Console.WriteLine($"[{_stopwatch.Elapsed}] [{id}] Binding to: {port}");
                    socket.Bind(new IPEndPoint(IPAddress.Loopback, port));
                    Console.WriteLine($"[{_stopwatch.Elapsed}] [{id}] Bound to: {port}");
                }
                catch
                {
                    Console.WriteLine($"[{_stopwatch.Elapsed}] [{id}] Failure Binding: {port}");
                    Environment.Exit(-1);
                }
            }

            Console.WriteLine($"[{_stopwatch.Elapsed}] [{id}] Released Port: {port}");
        }

        public static int GetNextPort()
        {
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                // Let the OS assign the next available port. Unless we cycle through all ports
                // on a test run, the OS will always increment the port number when making these calls.
                // This prevents races in parallel test runs where a test is already bound to
                // a given port, and a new test is able to bind to the same port due to port
                // reuse being enabled by default by the OS.
                socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
                return ((IPEndPoint)socket.LocalEndPoint).Port;
            }
        }
    }
}
