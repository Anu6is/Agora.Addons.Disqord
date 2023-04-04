using Disqord.Bot.Commands.Application;

namespace Agora.Addons.Disqord
{
    public interface IAgoraContext
    {
        public IDiscordApplicationGuildCommandContext FromContext(IDiscordApplicationGuildCommandContext context);
    }
}
