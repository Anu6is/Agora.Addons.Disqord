using Agora.Addons.Disqord.Commands.Checks;
using Disqord;
using Disqord.Bot.Commands.Application;
using Emporia.Extensions.Discord.Features.Queries;
using Microsoft.Extensions.DependencyInjection;
using Qmmands;

namespace Agora.Addons.Disqord.Commands
{
    public sealed partial class TemplateModule : AgoraModuleBase
    {
        [RequireManager]
        [SlashGroup("list")]
        [Description("View existing listing templates")]
        public sealed class ListTemplatesModule : AgoraModuleBase
        {
            public enum AuctionType { Standard, Sealed, Live }

            [SlashCommand("auction")]
            [Description("View and modify existing listing templates")]
            public async Task<IResult> ListAuctionTemplates()
            {
                var templates = await Base.ExecuteAsync(new GetAuctionTemplateListQuery(Context.GuildId));

                if (!templates.Data.Any()) return OkResponse(isEphimeral: true, embeds: new[] { new LocalEmbed().WithDescription("No templates have been created for this server.") });

                var emporium = await Cache.GetEmporiumAsync(Context.GuildId);
                var provider = Context.Services.CreateScope().ServiceProvider;

                return View(new AuctionTemplateListView(emporium, templates.Data, Context.GuildLocale, provider));
            }
        }
    }
}
