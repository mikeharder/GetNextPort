using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace GetNextPort
{
    class Program
    {
        private static readonly int _threads = Environment.ProcessorCount;
        private static readonly Stopwatch _stopwatch = Stopwatch.StartNew();

        private const ushort _maxPort = ushort.MaxValue;

        private static readonly ConcurrentBag<(int Thread, TimeSpan Time)>[] _assignedPorts = new ConcurrentBag<(int Thread, TimeSpan Time)>[_maxPort];
        private static readonly ConcurrentBag<(int Thread, TimeSpan Time)>[] _bindingPorts = new ConcurrentBag<(int Thread, TimeSpan Time)>[_maxPort];
        private static readonly ConcurrentBag<(int Thread, TimeSpan Time)>[] _boundPorts = new ConcurrentBag<(int Thread, TimeSpan Time)>[_maxPort];
        private static readonly ConcurrentBag<(int Thread, TimeSpan Time)>[] _releasedPorts = new ConcurrentBag<(int Thread, TimeSpan Time)>[_maxPort];

        private static int _portsTested = 0;

        static Program()
        {
            for (var i=0; i < _maxPort; i++)
            {
                _assignedPorts[i] = new ConcurrentBag<(int Thread, TimeSpan Time)>();
                _bindingPorts[i] = new ConcurrentBag<(int Thread, TimeSpan Time)>();
                _boundPorts[i] = new ConcurrentBag<(int Thread, TimeSpan Time)>();
                _releasedPorts[i] = new ConcurrentBag<(int Thread, TimeSpan Time)>();
            }
        }

        static void Main(string[] args)
        {
            Console.WriteLine("Testing GetNextPort()...");

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

            using (var printStatusTimer = new Timer(s => Console.WriteLine($"[{_stopwatch.Elapsed}] Ports Tested: {_portsTested}"),
                null, TimeSpan.Zero, TimeSpan.FromSeconds(1)))
            {
                foreach (var thread in threads)
                {
                    thread.Join();
                }
            }
        }

        private static void TestPort(int thread)
        {
            var port = GetNextPort();
            _assignedPorts[port].Add((thread, _stopwatch.Elapsed));

            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                try
                {
                    _bindingPorts[port].Add((thread, _stopwatch.Elapsed));
                    socket.Bind(new IPEndPoint(IPAddress.Loopback, port));
                    _boundPorts[port].Add((thread, _stopwatch.Elapsed));
                }
                catch
                {
                    Console.WriteLine($"Failed binding to port {port}");
                    Console.WriteLine();
                    Console.WriteLine("Assignment:");
                    Console.WriteLine(String.Join(Environment.NewLine, _assignedPorts[port].OrderBy(p => p.Time).Select(p => $"[{p.Thread}] {p.Time}")));
                    Console.WriteLine("Binding:");
                    Console.WriteLine(String.Join(Environment.NewLine, _bindingPorts[port].OrderBy(p => p.Time).Select(p => $"[{p.Thread}] {p.Time}")));
                    Console.WriteLine("Bound:");
                    Console.WriteLine(String.Join(Environment.NewLine, _boundPorts[port].OrderBy(p => p.Time).Select(p => $"[{p.Thread}] {p.Time}")));
                    Console.WriteLine("Released:");
                    Console.WriteLine(String.Join(Environment.NewLine, _releasedPorts[port].OrderBy(p => p.Time).Select(p => $"[{p.Thread}] {p.Time}")));
                    Environment.Exit(-1);
                }
            }

            _releasedPorts[port].Add((thread, _stopwatch.Elapsed));

            Interlocked.Increment(ref _portsTested);
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
