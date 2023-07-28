using Disqord;
using Disqord.Rest;

namespace Agora.Addons.Disqord.Extensions
{
    public static class InteractionExtensions
    {
        public static async Task SendMessageAsync(this IUserInteraction interaction, LocalInteractionMessageResponse response)
        {
            if (interaction is IComponentInteraction) return;

            if (interaction.Response().HasResponded)
                await interaction.Followup().SendAsync(response);
            else
                await interaction.Response().SendMessageAsync(response);
        }

        public static async Task ModifyMessageAsync(this IUserInteraction interaction, LocalInteractionMessageResponse response)
        {
            if (interaction is IComponentInteraction) return;

            if (interaction.Response().HasResponded)
                await interaction.Followup().ModifyResponseAsync(x =>
                {
                    x.Content = response.Content;
                    x.Embeds = response.Embeds.Value?.ToArray();
                    x.Components = response.Components.Value?.ToArray();
                });
            else
                await interaction.Response().ModifyMessageAsync(response);
        }
    }
}
