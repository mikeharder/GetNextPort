using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace GetNextPort
{
    class Program
    {
        private const ushort _maxPort = ushort.MaxValue;

        private static readonly int _threads = Environment.ProcessorCount;
        private static readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private static bool _debug;

        private static int _portsTested = 0;
        private static readonly ConcurrentBag<(int Thread, TimeSpan Time)>[] _assignedPorts = new ConcurrentBag<(int Thread, TimeSpan Time)>[_maxPort + 1];
        private static readonly ConcurrentBag<(int Thread, TimeSpan Time)>[] _bindingPorts = new ConcurrentBag<(int Thread, TimeSpan Time)>[_maxPort + 1];
        private static readonly ConcurrentBag<(int Thread, TimeSpan Time)>[] _boundPorts = new ConcurrentBag<(int Thread, TimeSpan Time)>[_maxPort + 1];
        private static readonly ConcurrentBag<(int Thread, TimeSpan Time)>[] _releasedPorts = new ConcurrentBag<(int Thread, TimeSpan Time)>[_maxPort + 1];

        static Program()
        {
            for (var i=0; i < _assignedPorts.Length; i++)
            {
                _assignedPorts[i] = new ConcurrentBag<(int Thread, TimeSpan Time)>();
                _bindingPorts[i] = new ConcurrentBag<(int Thread, TimeSpan Time)>();
                _boundPorts[i] = new ConcurrentBag<(int Thread, TimeSpan Time)>();
                _releasedPorts[i] = new ConcurrentBag<(int Thread, TimeSpan Time)>();
            }
        }

        static void Main(string[] args)
        {
            _debug = args.Length > 0 && args[0] == "--debug";

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

            if (_debug)
            {
                Console.WriteLine($"[{_stopwatch.Elapsed}] [{thread}] Assigned: {port}");
            }

            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                try
                {
                    _bindingPorts[port].Add((thread, _stopwatch.Elapsed));

                    if (_debug)
                    {
                        Console.WriteLine($"[{_stopwatch.Elapsed}] [{thread}] Binding: {port}");
                    }

                    socket.Bind(new IPEndPoint(IPAddress.Loopback, port));

                    _boundPorts[port].Add((thread, _stopwatch.Elapsed));

                    if (_debug)
                    {
                        Console.WriteLine($"[{_stopwatch.Elapsed}] [{thread}] Bound: {port}");
                    }
                }
                catch
                {
                    var sb = new StringBuilder();
                    sb.AppendLine($"Failed binding to port {port}");
                    sb.AppendLine();
                    sb.AppendLine("Assignment:");
                    sb.AppendLine(String.Join(Environment.NewLine, _assignedPorts[port].OrderBy(p => p.Time).Select(p => $"[{p.Thread}] {p.Time}")));
                    sb.AppendLine("Binding:");
                    sb.AppendLine(String.Join(Environment.NewLine, _bindingPorts[port].OrderBy(p => p.Time).Select(p => $"[{p.Thread}] {p.Time}")));
                    sb.AppendLine("Bound:");
                    sb.AppendLine(String.Join(Environment.NewLine, _boundPorts[port].OrderBy(p => p.Time).Select(p => $"[{p.Thread}] {p.Time}")));
                    sb.AppendLine("Released:");
                    sb.AppendLine(String.Join(Environment.NewLine, _releasedPorts[port].OrderBy(p => p.Time).Select(p => $"[{p.Thread}] {p.Time}")));
                    Console.WriteLine(sb.ToString());
                    Environment.Exit(-1);
                }
            }

            _releasedPorts[port].Add((thread, _stopwatch.Elapsed));

            if (_debug)
            {
                Console.WriteLine($"[{_stopwatch.Elapsed}] [{thread}] Released: {port}");
            }

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
