﻿using Disqord;
using Disqord.Rest;

namespace Agora.Addons.Disqord.Extensions
{
    public static class InteractionExtensions
    {
        public static async Task SendMessageAsync(this IUserInteraction interaction, LocalInteractionMessageResponse response)
        {
            try
            {
                if (interaction.Response().HasResponded)
                    await interaction.Followup().SendAsync(response);
                else
                    await interaction.Response().SendMessageAsync(response);
            }
            catch (Exception)
            {
                //unable to respond to interaction
            }
        }

        public static async Task ModifyMessageAsync(this IUserInteraction interaction, LocalInteractionMessageResponse response)
        {
            try
            {
                if (interaction.Response().HasResponded)
                    await interaction.Followup().ModifyResponseAsync(x =>
                    {
                        x.Content = response.Content;
                        x.Embeds = response.Embeds.HasValue ? response.Embeds.Value.ToArray() : null;
                        x.Components = response.Components.HasValue ? response.Components.Value.ToArray() : null;
                    });
                else
                    await interaction.Response().ModifyMessageAsync(response);
            }
            catch (Exception)
            {
                //unable to respond to interaction
            }

        }
    }
}
