using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Hestia.Logging.Abstractions
{
    public abstract class BatchingLoggerProvider : ILoggerProvider, ISupportExternalScope
    {
        public const string Prefix = "Logging";
        public abstract string Name { get; }

        protected readonly IConfiguration configuration;
        private readonly CancellationTokenSource cts;
        private readonly BlockingCollection<Log> queue;
        private readonly Task worker;
        private int failed;
        protected bool IsDisposed = false;
        private IExternalScopeProvider sp;
        protected internal IExternalScopeProvider ScopeProvider
        {
            get
            {
                var scope = configuration.GetValue($"{Prefix}:{Name}:IncludeScopes", false);
                return scope ? sp : null;
            }
        }

        protected abstract Task WriteMessagesAsync(IEnumerable<Log> logs, CancellationToken token);
        public ILogger CreateLogger(string category) => new BatchingLogger(this,category);

        protected BatchingLoggerProvider(IServiceProvider services)
        {
            configuration = services.GetRequiredService<IConfiguration>();
            
            int? size = configuration.GetValue<int?>($"{Prefix}:{Name}:Size", null);
            queue = size.HasValue ? new BlockingCollection<Log>(new ConcurrentQueue<Log>(),size.Value) : new BlockingCollection<Log>(new ConcurrentQueue<Log>());
            cts = new CancellationTokenSource();            
            worker = Task.Run(ProcessLogQueue);
        }

        private async Task ProcessLogQueue()
        {
            while (!cts.IsCancellationRequested)
            {
                var batch = configuration.GetValue("batch",int.MaxValue);
                List<Log> logs = new();
                while (batch > 0 && queue.TryTake(out var log))
                {
                    logs.Add(log);
                    batch--;
                }

                var dropped = Interlocked.Exchange(ref failed, 0);
                if (dropped != 0)
                {
                    logs.Add(new Log($"Hestia.Logging.{Name}", LogLevel.Warning, new EventId(0), $"{dropped} message(s) dropped because of queue size limit"));
                }

                if (logs.Count > 0)
                {
                    try
                    {
                        await WriteMessagesAsync(logs, cts.Token).ConfigureAwait(false);
                    }
                    catch
                    {

                    }
                }
                else
                {
                    await IntervalAsync(cts.Token).ConfigureAwait(false);
                }
            }
        }

        protected virtual Task IntervalAsync(CancellationToken token)
        {
            return Task.Delay(TimeSpan.FromMilliseconds(configuration.GetValue("interval", 5000)), token);
        }        

        public void Dispose()
        {
            if (!IsDisposed)
            {
                try
                {
                    Dispose(true);
                }
                catch
                {
                }
                IsDisposed = true;
                GC.SuppressFinalize(this);  // instructs GC not bother to call the destructor   
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            cts.Cancel();
            queue.CompleteAdding();
            try
            {
                worker.Wait(configuration.GetValue("timeout", 5000));
            }
            catch (TaskCanceledException)
            {
            }
            catch (AggregateException ex) when (ex.InnerExceptions.All(x => x is TaskCanceledException))
            {
            }
        }

        internal void AddLog(Log log)
        {
            if (queue.IsAddingCompleted) { return; }

            try
            {
                if (!queue.TryAdd(log, 0, cts.Token))
                {
                    Interlocked.Increment(ref failed);
                }
            }
            catch
            {
                //cancellation token canceled or CompleteAdding called
            }
        }

        internal bool IsEnabled(LogLevel level,string category)
        {
            var categories = category.Split('.');
            var sections = new IConfigurationSection[] {
                configuration.GetSection($"{Prefix}:{Name}:LogLevel"),
                configuration.GetSection($"{Prefix}:LogLevel")
            };
            int index = categories.Length;
            LogLevel? target = null;
            foreach(var section in sections)
            {
                if (!section.Exists()) { continue; }
                while (true)
                {                    
                    string key = index == 0 ? "Default" : string.Join('.', categories[0..index]);
                    target = section.GetValue<LogLevel?>(key, null);
                    if (target.HasValue || index == 0) { break; }
                    index -= 1;
                }
                if (target.HasValue) { break; }
            }
            return level >= (target ?? LogLevel.Information);
        }

        public void SetScopeProvider(IExternalScopeProvider provider)
        {
            sp = provider;
        }        
    }
}