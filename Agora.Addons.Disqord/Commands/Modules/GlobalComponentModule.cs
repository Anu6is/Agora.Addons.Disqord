using Disqord;
using Disqord.Bot.Commands.Components;
using Disqord.Rest;

namespace Agora.Addons.Disqord.Commands.Modules
{
    public sealed class GlobalComponentModule : DiscordComponentModuleBase
    {
        [ButtonCommand("Close")]
        public async Task CloseMessage() 
            => await Context.Bot.DeleteMessageAsync(Context.ChannelId, (Context.Interaction as IComponentInteraction).Message.Id);
    }
}
