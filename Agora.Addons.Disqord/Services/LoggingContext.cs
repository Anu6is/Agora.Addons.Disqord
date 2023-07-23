using Emporia.Application.Common.Interfaces;

namespace Agora.Addons.Disqord
{
    public class LoggingContext : ILoggingContext
    {
        public Dictionary<string, object> ContextInfo => new();
    }
}
