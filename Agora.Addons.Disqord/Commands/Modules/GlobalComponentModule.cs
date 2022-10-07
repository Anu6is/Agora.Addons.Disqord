﻿using Disqord;
using Disqord.Bot.Commands.Components;
using Disqord.Rest;

namespace Agora.Addons.Disqord.Commands.Modules
{
    public sealed class GlobalComponentModule : DiscordComponentModuleBase
    {
        [ButtonCommand("Close")]
        public async Task CloseMessage()
        {
            if (!Context.Interaction.Response().HasResponded) await Deferral();

            await Context.Interaction.Followup().DeleteAsync((Context.Interaction as IComponentInteraction).Message.Id);
        }
    }
}