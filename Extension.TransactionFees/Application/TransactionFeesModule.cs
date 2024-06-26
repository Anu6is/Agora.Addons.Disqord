using Agora.Addons.Disqord.Commands;
using Agora.Addons.Disqord.Commands.Checks;
using Disqord;
using Disqord.Bot.Commands;
using Disqord.Bot.Commands.Application;
using Emporia.Persistence.DataAccess;
using Extension.TransactionFees.Domain;
using Microsoft.Extensions.DependencyInjection;
using Qmmands;

namespace Extension.TransactionFees.Application;

[SlashGroup("server")]
[Description("Configure various server fees")]
[RequireAuthorPermissions(Permissions.ManageGuild)]
public sealed class TransactionFeesModule(IServiceScopeFactory serviceScope) : AgoraModuleBase
{
    [RequireSetup]
    [SlashCommand("fees")]
    [Description("Review and/or modify the current server transaction fees.")]
    public async Task<IResult> ServerFeeSettings()
    {
        var settings = await Data.Transaction<GenericRepository<TransactionFeeSettings>>().GetByIdAsync(EmporiumId)
                       ?? await Data.Transaction<GenericRepository<TransactionFeeSettings>>().AddAsync(TransactionFeeSettings.Create(Guild.Id));

        return View(new TransactionFeesView(settings, serviceScope));
    }
}
