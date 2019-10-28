/*
 * (C) Copyright 2019 Maciej Izak
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using CommandLine;
using System.Text.Json;
using DnsMx;

namespace DnsMxCli
{
    class DnsMxCli
    {
        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
                .WithParsed(opt => new DnsMxCli(opt));
        }

        private readonly Options _options;
        private readonly DnsMxFactory _factory;

        /// <summary>
        /// Create new instance of Command Line Interface (CLI) for DnsMx library. DnsMxCli functionality might be
        /// usefully to refactor in the future version.
        /// </summary>
        /// <param name="options">Options received from command line converted to Options class instance</param>
        public DnsMxCli(Options options)
        {
            _options = options;
            var settings = new FactorySettings
            {
                DnsIp = _options.DnsIp, DnsPort = _options.DnsPort, Sort = _options.Sort, Async = true, Process = false
            };
            _factory = new DnsMxFactory(GetDomains(), GetDataNotifier(), settings);
            CreateQuitThread();
            _factory.Process();
            SaveResult();
        }

        /// <summary>
        /// Save result of application to file (only when CLI --output parameter was specified)
        /// </summary>
        private void SaveResult()
        {
            if (_options.Output == null) return;
            try
            {
                var outputContent = JsonSerializer.SerializeToUtf8Bytes(_factory.Result,
                    new JsonSerializerOptions
                    {
                        WriteIndented = _options.HumanReadable
                    });
                File.WriteAllBytes(_options.Output, outputContent);
            }
            catch (Exception e)
            {
                Console.WriteLine($"--output ERROR {e.Message}");
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// Get domains in the <c>IEnumerable<string></c> form. If the CLI --input is specified then other domains
        /// are ignored
        /// </summary>
        /// <returns></returns>
        private IEnumerable<string> GetDomains()
        {
            // --input option has priority
            if (_options.Input == null) return _options.Domains;
            var jsonArray = File.ReadAllBytes(_options.Input);
            return JsonSerializer.Deserialize<IEnumerable<string>>(new Span<byte>(jsonArray));
        }

        /// <summary>
        /// Provide delegate for printing processed domains on the screen. If CLI option --quiet is specified
        /// then null is returned (printing on screen for such case is improper)
        /// </summary>
        /// <returns></returns>
        private Action<DomainMx>? GetDataNotifier()
        {
            // handle writing data on screen (if --quiet mode is not specified)
            if (_options.Quiet) return null;
            return dmx =>
            {
                if (dmx.Error != null)
                    Console.WriteLine($"ERROR {dmx.Error} FOR {dmx.Domain}");
                else
                {
                    Console.WriteLine($"DONE {dmx.Domain}");
                    foreach (var mx in dmx.MxArray)
                        Console.WriteLine(mx.Error == null
                            ? $"\tOK\t{mx.Preference}\t{mx.ExchangeIp}\t{mx.Exchange}"
                            : $"ERROR {mx.Error} FOR ({mx.Preference}) {mx.Exchange}");
                }
            };
        }

        /// <summary>
        /// Creates lightweight background thread for terminating CLI on user request.
        /// The user can press 'Q' key at any moment
        /// </summary>
        private void CreateQuitThread()
        {
            var thread = new Thread(() =>
            {
                if (Console.ReadKey(true).KeyChar.ToString().ToUpperInvariant() == "Q")
                    _factory.Cancel();
            }) {IsBackground = true};
            thread.Start();
            if (!_options.Quiet) Console.WriteLine("Press 'Q' to terminate the application...\n");
        }
    }
}