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
        private static int _skipPort;

        private static int _portsTested = 0;
        private static readonly ConcurrentBag<(int Thread, TimeSpan Time)>[] _assignedPorts = new ConcurrentBag<(int Thread, TimeSpan Time)>[_maxPort + 1];
        private static readonly ConcurrentBag<(int Thread, TimeSpan Time)>[] _bindingPorts = new ConcurrentBag<(int Thread, TimeSpan Time)>[_maxPort + 1];
        private static readonly ConcurrentBag<(int Thread, TimeSpan Time)>[] _boundPorts = new ConcurrentBag<(int Thread, TimeSpan Time)>[_maxPort + 1];
        private static readonly ConcurrentBag<(int Thread, TimeSpan Time)>[] _releasedPorts = new ConcurrentBag<(int Thread, TimeSpan Time)>[_maxPort + 1];

        private static int _failedPort = -1;

        static Program()
        {
            for (var i = 0; i < _assignedPorts.Length; i++)
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

            if (args.Length > 0)
            {
                int.TryParse(args[0], out _skipPort);
            }

            Console.WriteLine("Testing GetNextPort()...");

            var threads = new Thread[_threads];
            for (var i = 0; i < _threads; i++)
            {
                var j = i;
                threads[i] = new Thread(() =>
                {
                    while (_failedPort == -1)
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

            var sb = new StringBuilder();
            sb.AppendLine($"Failed binding to port {_failedPort}");
            sb.AppendLine();
            sb.AppendLine("Assignment:");
            sb.AppendLine(String.Join(Environment.NewLine, _assignedPorts[_failedPort].OrderBy(p => p.Time).Select(p => $"[{p.Thread}] {p.Time}")));
            sb.AppendLine("Binding:");
            sb.AppendLine(String.Join(Environment.NewLine, _bindingPorts[_failedPort].OrderBy(p => p.Time).Select(p => $"[{p.Thread}] {p.Time}")));
            sb.AppendLine("Bound:");
            sb.AppendLine(String.Join(Environment.NewLine, _boundPorts[_failedPort].OrderBy(p => p.Time).Select(p => $"[{p.Thread}] {p.Time}")));
            sb.AppendLine("Released:");
            sb.AppendLine(String.Join(Environment.NewLine, _releasedPorts[_failedPort].OrderBy(p => p.Time).Select(p => $"[{p.Thread}] {p.Time}")));
            Console.WriteLine(sb.ToString());
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
                    _failedPort = port;
                    return;
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

                var port = ((IPEndPoint)socket.LocalEndPoint).Port;

                if (port == _skipPort)
                {
                    return GetNextPort();
                }
                else
                {
                    return port;
                }
            }
        }
    }
}
