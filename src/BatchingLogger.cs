using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hestia.Logging.Abstractions
{
    public sealed class BatchingLogger : ILogger
    {
        private readonly BatchingLoggerProvider provider;
        private readonly string category;

        public BatchingLogger(BatchingLoggerProvider provider,string category)
        {
            this.provider = provider;
            this.category = category;
        }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            return provider.ScopeProvider?.Push(state) ?? NullScope.Instance; 
        }

        public bool IsEnabled(LogLevel level)
        {
            return level != LogLevel.None && provider.IsEnabled(level,category);
        }

        private void SetScope(object scope,Log log)
        {
            log.Scopes.Add(scope);
        }

        public void Log<TState>(LogLevel level, EventId @event, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(level)) { return; }

            var message = formatter?.Invoke(state, exception);

            var log = new Log(category, level, @event, message,exception);

            if(provider.ScopeProvider != null)
            {
                provider.ScopeProvider.ForEachScope(SetScope, log);
            }

            provider.AddLog(log);

        }
    }
}
