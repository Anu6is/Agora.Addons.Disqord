using Disqord;
using Disqord.Bot.Commands.Components;
using Disqord.Rest;

namespace Agora.Addons.Disqord.Commands
{
    public sealed class GlobalComponentModule : DiscordComponentModuleBase
    {
        [ButtonCommand("#Close")]
        public async Task CloseMessage()
        {
            if (Context.Interaction.Response().HasResponded)
                await Context.Interaction.Followup().DeleteAsync((Context.Interaction as IComponentInteraction).Message.Id);
            else
                try
                {
                    await (Context.Interaction as IComponentInteraction).Message.DeleteAsync();
                }
                catch (Exception) { }
        }
    }
}
