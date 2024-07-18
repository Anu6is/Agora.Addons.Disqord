using Emporia.Domain.Common;

namespace Extension.CustomAnnouncements.Domain;

public class Announcement : Entity<Guid>
{
    public ulong GuildId { get; set; }
    public string Message { get; set; }
    public AnnouncementType AnnouncementType { get; set; }

    public EmporiumId EmporiumId { get; set; }

    private Announcement(Guid id) : base(id) { }

    public static Announcement Create(ulong guildId, AnnouncementType type, string message)
    {
        return new Announcement(Guid.NewGuid()) { GuildId = guildId, AnnouncementType = type, Message = message, EmporiumId = new EmporiumId(guildId) };
    }
}

public enum AnnouncementType
{
    Default,
    Auction,
    Market,
    Giveaway,
    Trade
}
