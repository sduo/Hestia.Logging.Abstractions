using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace Hestia.Logging.Abstractions
{
    public record Log
    {
        public string Category { get; init; }

        public LogLevel Level { get; init ; }

        public EventId Event { get; init; }

        public DateTimeOffset Timestamp { get; init; }

        public string Message { get; init; }

        public Exception Exception { get; init; } 

        public List<object> Scopes { get; init; }

        public Log(string category,  LogLevel level, EventId @event, string message, Exception exception = null) : this(category, DateTimeOffset.Now,level, @event, message, exception) { }

        public Log(string category, DateTimeOffset timestamp,  LogLevel level, EventId @event, string message,Exception exception = null)
        {
            Category = category;
            Timestamp = timestamp;
            Level = level;
            Event = @event;
            Message = message;
            Exception = exception;
            Scopes= new List<object>();
        }
    }
}
