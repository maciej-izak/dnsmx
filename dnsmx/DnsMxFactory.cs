/*
 * (C) Copyright 2019 Maciej Izak
 */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DnsClient;

namespace DnsMx
{
    using DnsQueryDictionary = ConcurrentDictionary<string, DomainMx>;
    using static TasksUtils;

    public sealed class DnsMxFactory : IDisposable
    {
        // special concurrent dictionary will allow to start processing ASAP
        private readonly DnsQueryDictionary _domainsDict =
            new DnsQueryDictionary(Environment.ProcessorCount * 2, 1024);

        private Task? _domainsTasks;
        private Task? _mainTask;
        private Task? _dataTask;
        private bool _disposed;
        private readonly LookupClient? _lookup;
        // processed domains are stored in special collection handled by dedicated thread, see dataTask in constructor
        private readonly BlockingCollection<DomainMx>? _dataStorage;
        private readonly ConcurrentBag<Task> _exchangeTasks = new ConcurrentBag<Task>();
        private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();

        private FactorySettings Settings { get; }

        private string? ErrorMessage { get; set; }

        /// <summary>
        /// Create factory which is able to produce DomainMx info. Factory is able to work in async mode
        /// (for <paramref name="settings"/>.Async true). The process is able to start during construction
        /// (depending on <paramref name="settings"/>.Process value). 
        /// </summary>
        /// <param name="domains">Domains to resolve their MX records</param>
        /// <param name="notifyDomainCompletion">Callback for reporting processed data in console or any other GUI.
        /// <paramref name="notifyDomainCompletion"/> is invoked from dedicated thread</param>
        /// <param name="settings">Settings for factory see <see cref="FactorySettings"/> for more details</param>
        public DnsMxFactory(IEnumerable<string> domains, Action<DomainMx>? notifyDomainCompletion = null,
            FactorySettings? settings = null)
        {
            settings ??= FactorySettings.DefaultSettings;
            Settings = settings;
            if (Settings.DnsIp != null)
            {
                try
                {
                    var endpoint = new IPEndPoint(IPAddress.Parse(Settings.DnsIp),
                        Settings.DnsPort ?? NameServer.DefaultPort);
                    _lookup = new LookupClient(endpoint);
                }
                catch (Exception e)
                {
                    ErrorMessage = e.Message;
                    return;
                }
            }
            else
                _lookup = new LookupClient();

            if (notifyDomainCompletion != null)
            {
                _dataStorage = new BlockingCollection<DomainMx>(new ConcurrentBag<DomainMx>());
                _dataTask = new Task(() =>
                {
                    foreach (var dmx in _dataStorage.GetConsumingEnumerable(_cancellation.Token))
                        notifyDomainCompletion(dmx);
                }, _cancellation.Token);
                _dataTask.Start();
            }

            // filter duplicates and process if needed (default)
            InitializeDomainsDict(domains);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (disposing)
            {
                _dataStorage?.Dispose();
                _cancellation.Dispose();
            }
            _disposed = true;
        }

        ~DnsMxFactory() => Dispose(false);

        /// <summary>
        /// Process domain MX query, start new tasks for each of received MX entry
        /// </summary>
        /// <param name="domainMx"></param>
        /// <returns></returns>
        private async Task ProcessDomainAsync(DomainMx domainMx)
        {
            if (_lookup == null) return;
            int mxCount = 0;
            var wasException = false;
            try
            {
                var result = await _lookup.QueryAsync(domainMx.Domain, QueryType.MX, QueryClass.IN, _cancellation.Token);
                domainMx.Error = result.HasError ? result.ErrorMessage : null;
                // result.Answers.Count may be different than real MX records count in the case of redirection
                var mxs = result.Answers.MxRecords();
                mxCount = mxs.Count();
                if (mxCount != result.Answers.Count)
                    domainMx.Error =
                        $@"Bad number ({mxCount}\{result.Answers.Count}) of received MX records. Probably redirects occurred.";
                domainMx.mxCount = mxCount;
                domainMx.MxArray = new DomainMx.MxEntry[mxCount];
                if (Settings.Sort)
                    mxs = mxs.OrderBy(mx => mx.Preference);
                var i = 0;
                foreach (var mx in mxs)
                {
                    var entry = new DomainMx.MxEntry(domainMx, mx.Exchange, mx.Preference);
                    ProcessExchange(entry);
                    domainMx.MxArray[i++] = entry;
                }
            }
            catch (Exception e)
            {
                domainMx.Error = e.Message;
                wasException = true;
            }
            finally
            {
                // log info about Domain even without MX (for reporting purposes)
                if (mxCount == 0 || wasException) _dataStorage?.Add(domainMx);   
            }
        }

        /// <summary>
        /// Get server IP for each MX entry. The last processed MX entry will add to _dataStorage fully processed
        /// DomainMx instance 
        /// </summary>
        /// <param name="entry"></param>
        /// <returns></returns>
        private async Task ProcessExchangeAsync(DomainMx.MxEntry entry)
        {
            if (_lookup == null) return;
            // find IP of each exchange server
            try
            {
                var result = await _lookup.QueryAsync(entry.Exchange, QueryType.A, QueryClass.IN, _cancellation.Token);
                entry.Error = result.HasError ? result.ErrorMessage : null;
                if (entry.Error != null)
                    return;
                var resp = result.Answers.ARecords().First();
                entry.ExchangeIp = resp.Address.ToString();
            }
            finally
            {
                if (Interlocked.Increment(ref entry.Owner.mxProcessed) == entry.Owner.mxCount) 
                    _dataStorage?.Add(entry.Owner);
            }
        }

        private void ProcessExchange(DomainMx.MxEntry entry) =>
            _exchangeTasks.Add(ProcessExchangeAsync(entry));

        /// <summary>
        /// Initialize internal _domainsDict dictionary with all domains. Depending on configuration this method can
        /// start tasks for processing domains immediately or process can be executed on request.
        /// </summary>
        /// <param name="domains">domains to be processed</param>
        /// <returns>unique domains number</returns>
        private int InitializeEachDomain(IEnumerable<string> domains)
        {
            var count = 0;
            var tasks = new List<Task>();
            Func<string, DomainMx> domainsValFactory;
            if (Settings.Process)
                domainsValFactory = k =>
                {
                    count++;
                    var dmx = new DomainMx(k);
                    tasks.Add(ProcessDomainAsync(dmx));
                    return dmx;
                };
            else
                domainsValFactory = k =>
                {
                    count++;
                    return new DomainMx(k);
                };

            foreach (var d in domains)
                // filter duplicates
                _domainsDict.GetOrAdd(d.ToLower(), domainsValFactory);
            if (Settings.Process)
                _domainsTasks = WhenAllOrError(tasks);
            return count;
        }

        /// <summary>
        /// Perform complete operation. In this method all threads/tasks are synchronized including final
        /// data reporting (via _dataStorage) 
        /// </summary>
        /// <returns></returns>
        private async Task InternalProcessAsync()
        {
            if (_domainsTasks == null) return;
            try
            {
                await _domainsTasks;
                var allIpResolve = WhenAllOrError(_exchangeTasks);
                if (_cancellation.IsCancellationRequested)
                    return;
                await allIpResolve;
                _dataStorage?.CompleteAdding();
                _dataTask?.Wait();
            }
            catch (Exception e)
            {
                // after first exception on this level whole process is terminated, we need only first exception message
                // so no need for AggregateException (InnerExceptions)
                ErrorMessage = e.Message;
            }
        }

        /// <summary>
        /// Helper method used in constructor for start processing (depending on settings) and for data initialization
        /// </summary>
        /// <param name="domains">domains to be processed</param>
        /// <returns>unique domains number</returns>
        private int InitializeDomainsDict(IEnumerable<string> domains)
        {
            var count = InitializeEachDomain(domains);
            if (!Settings.Process)
                return count;
            _mainTask = InternalProcessAsync();
            if (!Settings.Async)
                _mainTask.Wait();
            return count;
        }

        /// <summary>
        /// Final or partially processed domains data
        /// </summary>
        public DnsMxData Result => new DnsMxData(ErrorMessage, Settings.DnsAddress, _domainsDict.Values);

        public void Cancel() => _cancellation.Cancel();

        public async Task<DnsMxData> ProcessAsync()
        {
            // check for DNS configuration error (for example DNS address can be invalid)
            if (ErrorMessage != null) return Result;
            if (_mainTask == null)
            {
                var tasks = _domainsDict.Values.Select(ProcessDomainAsync);
                _domainsTasks = WhenAllOrError(tasks);
                _mainTask = InternalProcessAsync();
            }

            await _mainTask;
            return Result;
        }

        public DnsMxData Process()
        {
            var task = ProcessAsync();
            task.Wait();
            return task.Result;
        }
    }

    public class FactorySettings
    {
        public bool Process { get; set; }
        public bool Async { get; set; }
        public bool Sort { get; set; }

        public string? DnsIp { get; set; }
        public int? DnsPort { get; set; }

        public string DnsAddress => DnsIp != null ? $"{DnsIp}:{DnsPort ?? NameServer.DefaultPort}" : "(default)";

        public static FactorySettings DefaultSettings => new FactorySettings();

        public FactorySettings() => Process = true;
    }
}