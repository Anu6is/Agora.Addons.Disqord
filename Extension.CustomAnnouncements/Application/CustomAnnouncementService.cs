using Agora.Shared.Attributes;
using Agora.Shared.Services;
using Emporia.Application.Common;
using Emporia.Application.Specifications;
using Emporia.Domain.Services;
using Emporia.Persistence.DataAccess;
using Extension.CustomAnnouncements.Domain;
using Microsoft.Extensions.Logging;

namespace Extension.CustomAnnouncements.Application;

[AgoraService(AgoraServiceAttribute.ServiceLifetime.Transient)]
public class CustomAnnouncementService(IDataAccessor dataAccessor, ILogger<CustomAnnouncementService> logger) : AgoraService(logger)
{
    public async Task<IResult<string>> AddAnnouncementAsync(ulong guildId, AnnouncementType type, string message)
    {
        var feedback = string.Empty;
        var announcementSpecification = new EntitySpec<Announcement>(x => x.GuildId == guildId && x.AnnouncementType == type);
        var announcement = await dataAccessor.Transaction<GenericRepository<Announcement>>().FirstOrDefaultAsync(announcementSpecification);

        try
        {
            if (announcement != null)
            {
                announcement.Message = message;

                await dataAccessor.Transaction<GenericRepository<Announcement>>().UpdateAsync(announcement);

                feedback = "Announcment successfully updated!";
            }
            else
            {
                announcement = Announcement.Create(guildId, type, message);

                await dataAccessor.Transaction<GenericRepository<Announcement>>().AddAsync(announcement);

                feedback = "Announcement successfully created!";
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error adding announcement");

            return Result<string>.Failure("An error occurred while saving the announcmenet, try again later.");
        }

        return Result.Success(feedback);
    }

    public async Task<IResult<string>> GetAnnouncementAsync(ulong guildId, AnnouncementType type)
    {
        var announcementSpecification = new EntitySpec<Announcement>(x => x.GuildId == guildId && x.AnnouncementType == type);
        var announcement = await dataAccessor.Transaction<GenericRepository<Announcement>>().FirstOrDefaultAsync(announcementSpecification);

        return announcement is null ? Result<string>.Failure($"Error retrieving {type} announcement") : Result.Success(announcement.Message);
    }

    public async Task<IResult<string>> DeleteAnnouncementAsync(ulong guildId, AnnouncementType type)
    {
        var announcementSpecification = new EntitySpec<Announcement>(x => x.GuildId == guildId && x.AnnouncementType == type);
        var announcement = await dataAccessor.Transaction<GenericRepository<Announcement>>().FirstOrDefaultAsync(announcementSpecification);

        if (announcement is null) return Result<string>.Failure($"Unable to delete announcement. {type} announcement not found!");

        await dataAccessor.Transaction<GenericRepository<Announcement>>().DeleteAsync(announcement);

        return Result.Success("Announcement successfully deleted");
    }
}
