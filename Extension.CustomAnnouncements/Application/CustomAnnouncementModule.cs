using Agora.Addons.Disqord.Commands;
using Agora.Addons.Disqord.Commands.Checks;
using Disqord;
using Disqord.Bot.Commands;
using Disqord.Bot.Commands.Application;
using Emporia.Application.Specifications;
using Emporia.Persistence.DataAccess;
using Extension.CustomAnnouncements.Domain;
using Microsoft.Extensions.DependencyInjection;
using Qmmands;

namespace Extension.CustomAnnouncements.Application;

[SlashGroup("server")]
[RequireAuthorPermissions(Permissions.ManageGuild)]
public sealed class CustomAnnouncementModule(IServiceScopeFactory scopeFactory) : AgoraModuleBase
{
    [RequireSetup]
    [SlashCommand("announcements")]
    [Description("Customize result-log announcement messages")]
    public async Task<IResult> ManageAnnouncements()
    {
        var customAnnouncements = await Data.Transaction<GenericRepository<Announcement>>().ListAsync(new EntitySpec<Announcement>(x => x.GuildId == Guild.Id.RawValue));

        return View(new ManageAnnouncementsView(Guild.Id.RawValue, customAnnouncements, scopeFactory));
    }
}
