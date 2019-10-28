/*
 * (C) Copyright 2019 Maciej Izak
 */

using System.Collections.Generic;
using CommandLine;

namespace DnsMxCli
{
    public class Options
    {
        public Options(bool quiet, bool sort, string dnsIp, int dnsPort,
            string? input, string? output, bool humanReadable, IEnumerable<string> domains)
        {
            Quiet = quiet;
            Sort = sort;
            DnsIp = dnsIp;
            DnsPort = dnsPort;
            Domains = domains;
            Input = input;
            Output = output;
            HumanReadable = humanReadable;
        }

        [Option(HelpText = "Do not print any info into std output")]
        public bool Quiet { get; }

        [Option(HelpText = "Sort all MX records for each domain via MX preference info")]
        public bool Sort { get; }

        [Option(HelpText = "Custom DNS IP address")]
        public string DnsIp { get; }

        [Option(HelpText = "Custom DNS port")]
        public int DnsPort { get; }
        
        [Option(HelpText = "Specify file which contains JSON string array with domains")]
        public string? Input { get; }
        
        [Option(HelpText = "Final file location with result data (in JSON format)")]
        public string? Output { get; }

        [Option(HelpText = "Can be combined with --output option, the final data will be stored in \"human readable\" form ")]
        public bool HumanReadable { get; }
        
        [Value(0, HelpText = "Input domains to process. Cannot be combined with --input option (otherwise will be simple ignored)",
            MetaName = "Domains")]
        public IEnumerable<string> Domains { get; }
    }
}