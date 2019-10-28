/*
 * (C) Copyright 2019 Maciej Izak
 */

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DnsMx
{
    public class DnsMxData
    {
        public string? Error { get; }
        public string Dns { get; }
        public IEnumerable<DomainMx> Domains { get; }

        public DnsMxData(string? error, string dns, IEnumerable<DomainMx> domains)
        {
            Error = error;
            Dns = dns;
            Domains = domains;
        }
    }

    public class DomainMx
    {
        internal int mxCount;
        internal int mxProcessed;
        
        [JsonIgnore]
        public int MxDestCount => mxCount;
        [JsonIgnore]
        public int MxProcessed => mxProcessed;
        public string? Error { get; internal set; } = "Unfinished task";
        public string Domain { get; }
        public MxEntry[] MxArray { get; internal set; } = { };

        public DomainMx(string domain) => Domain = domain;

        public class MxEntry
        {
            [JsonIgnore] public DomainMx Owner { get; }
            public string? Error { get; internal set; } = "Unfinished task";
            public string Exchange { get; }
            public string? ExchangeIp { get; internal set; }
            public ushort Preference { get; }

            public MxEntry(DomainMx owner, string exchange, ushort preference)
            {
                Owner = owner;
                Exchange = exchange;
                Preference = preference;
            }
        }
    }
}