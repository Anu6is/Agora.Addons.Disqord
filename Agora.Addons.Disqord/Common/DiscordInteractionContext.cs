using Disqord;
using Disqord.Bot;
using Disqord.Gateway;

namespace Agora.Addons.Disqord
{
    public class DiscordInteractionContext
    {
        public IMember Author { get; }
        public DiscordBotBase Bot { get; }
        public IUserInteraction Interaction { get; }
        public Snowflake? GuildId => Interaction.GuildId;
        public Snowflake ChannelId => Interaction.ChannelId;

        public DiscordInteractionContext(InteractionReceivedEventArgs args) : this(args.Interaction, args.Member) { }

        public DiscordInteractionContext(IUserInteraction interaction, IMember member)
        {
            Bot = interaction.Client as DiscordBotBase;
            Interaction = interaction;
            Author = member;
        }
    }
}
