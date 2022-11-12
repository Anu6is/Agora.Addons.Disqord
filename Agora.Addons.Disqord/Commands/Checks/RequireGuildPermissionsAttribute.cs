using Disqord;
using Disqord.Bot.Commands;
using Disqord.Gateway;
using Qmmands;

namespace Agora.Addons.Disqord.Commands.Checks
{
    internal class RequireGuildPermissionsAttribute : DiscordParameterCheckAttribute
    {
        private readonly Permissions _permissions;

        public RequireGuildPermissionsAttribute(Permissions permissions) => _permissions = permissions;

        public override bool CanCheck(IParameter parameter, object value) => value is Snowflake;

        public override ValueTask<IResult> CheckAsync(IDiscordCommandContext context, IParameter parameter, object argument)
        {
            var guildId = (Snowflake)argument;
            var currentMember = context.Bot.GetGuild(guildId).Client.GetCurrentMember(guildId);
            var currentPerms = currentMember.CalculateGuildPermissions();

            if (currentPerms.HasFlag(_permissions)) return Results.Success;
            
            var guild = currentMember.GetGuild();
            
            return Results.Failure($"The bot lacks permissions ({_permissions & ~currentPerms}) in {guild.Name}");
        }
    }
}
