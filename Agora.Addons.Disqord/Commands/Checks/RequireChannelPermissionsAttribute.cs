using Disqord;
using Disqord.Bot.Commands;
using Disqord.Gateway;
using Qmmands;
using Qommon;
using System.Runtime.CompilerServices;

namespace Agora.Addons.Disqord.Commands.Checks
{
    internal class RequireChannelPermissionsAttribute : DiscordParameterCheckAttribute
    {
        public Permissions Permissions { get; }

        public RequireChannelPermissionsAttribute(Permissions permissions) =>  Permissions = permissions;

        public override bool CanCheck(IParameter parameter, object value) => value is IChannel;

        public override ValueTask<IResult> CheckAsync(IDiscordCommandContext context, IParameter parameter, object argument)
        {
            if (context is not IDiscordGuildCommandContext discordGuildCommandContext) return Results.Success;

            Permissions permissions;
            Snowflake channelId = (argument as IChannel).Id;
            IGuildChannel channel = discordGuildCommandContext.Bot.GetChannel(discordGuildCommandContext.GuildId, channelId);
                
            if (channel == null) Throw.InvalidOperationException("Unable to locate the specified channel.");

            CachedMember currentMember = discordGuildCommandContext.Bot.GetCurrentMember(discordGuildCommandContext.GuildId);
                
            if (currentMember == null) Throw.InvalidOperationException("RequireBotPermissionsAttribute requires the current member cached.");

            permissions = currentMember.CalculateChannelPermissions(channel);

            if (permissions.HasFlag(Permissions)) return Results.Success;

            DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new(59, 1);
            defaultInterpolatedStringHandler.AppendLiteral("The bot lacks the necessary permissions (");
            defaultInterpolatedStringHandler.AppendFormatted(Permissions & ~permissions);
            defaultInterpolatedStringHandler.AppendLiteral(") to use this channel.");

            return Results.Failure(defaultInterpolatedStringHandler.ToStringAndClear());
        }
    }
}
