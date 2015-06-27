namespace Sawara.Pinger
{
    using log4net;
    using NDesk.Options;
    using Sawara.PubSub;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.NetworkInformation;
    using System.Threading;

    public class Ping
    {
        public class Options
        {
            public int TTL { get; set; } = 256;
            public TimeSpan TimeoutLength { get; set; } = TimeSpan.FromMilliseconds(5000);
            public int BufferSize { get; set; } = 32;
            public TimeSpan Interval { get; set; } = TimeSpan.FromMilliseconds(1000);
            public bool Help { get; set; } = false;
            public bool Version { get; set; } = false;
            public List<string> Args { get; } = new List<string>();
        }

        public static Options ParseArguments(string[] args)
        {
            var options = new Options();
            var argParser = new OptionSet() {
                { "i=", "Time to Live.", (int arg) => { options.TTL = arg; } },
                { "l=", "Send buffer size.", (int arg) => { options.BufferSize = arg; } },
                { "w=", "Timeout in milliseconds to wait for each reply.", (int arg) => { options.TimeoutLength= TimeSpan.FromMilliseconds(arg); } },
                { "h|help", "Show this message.", arg => { options.Help = (arg != null);  } },
                { "V|version", "Show version info.", arg => { options.Version = (arg != null); } }
            };
            try
            {
                options.Args.AddRange(argParser.Parse(args));
            }
            catch (OptionException e)
            {
                Console.Error.WriteLine(e.Message);
                WriteUsage(Console.Error);
                argParser.WriteOptionDescriptions(Console.Error);
                throw new ArgumentException();
            }
            if (options.Version)
            {
                WriteVersionInfo(Console.Error);
                throw new ArgumentException();
            }
            if (options.Args.Count < 1)
            {
                WriteUsage(Console.Error);
                argParser.WriteOptionDescriptions(Console.Error);
                throw new ArgumentException();
            }
            return options;
        }

        public Ping(Options options)
        {
            if (options == null)
            {
                throw new ArgumentNullException();
            }
            this.options = options;
        }

        public Ping Subscribe(Subscriber<PingReply> subscriber)
        {
            this.publisher.Subscribe(subscriber);
            return this;
        }
        public Ping Unsubscribe(Subscriber<PingReply> subscriber)
        {
            this.publisher.Unsubscribe(subscriber);
            return this;
        }

        public int Execute()
        {
            var hostNameOrAddress = this.options.Args.First();
            var buffer = new byte[this.options.BufferSize];
            buffer.Initialize();

            logger.InfoFormat("Initialized with ping target is %s, data size is %d", hostNameOrAddress, buffer.Length);

            using (var ping = new System.Net.NetworkInformation.Ping())
            {
                while (true)
                {
                    var reply = ping.Send(hostNameOrAddress, (int)this.options.TimeoutLength.TotalMilliseconds, buffer);
                    var rtt = reply.RoundtripTime == 0 ? "<1ms" : string.Format("={0}ms", reply.RoundtripTime);
                    Console.WriteLine("Reply from {0}: bytes={1} time{2} TTL={3}", reply.Address, reply.Buffer.Count(), rtt, reply.Options.Ttl);

                    logger.InfoFormat("Got reply %s and publishing it.", reply);
                    this.publisher.Publish(reply);
                    logger.Info("Published.");

                    Thread.Sleep(this.options.Interval);
                }
            }
        }

        static void WriteUsage(TextWriter writer)
        {
            var asm = typeof(Ping).Assembly.GetName();

            writer.WriteLine("{0} [options] {{hostname or address}}", asm.Name);
        }

        static void WriteVersionInfo(TextWriter writer)
        {
            var asm = typeof(Ping).Assembly.GetName();

            writer.WriteLine("{0} - v{1}", asm.Name, asm.Version);
        }

        private static ILog logger = LogManager.GetLogger("Sawara.Pinger");

        private Options options;

        private Publisher<PingReply> publisher = new Publisher<PingReply>();
    }
}
