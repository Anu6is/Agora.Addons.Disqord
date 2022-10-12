using Disqord;
using Disqord.Bot.Commands.Components;
using Disqord.Rest;
using Emporia.Application.Features.Commands;
using Emporia.Domain.Common;
using MediatR;
using Qmmands;

namespace Agora.Addons.Disqord.Commands
{
    public sealed class EmporiumUserComponentModule : DiscordComponentGuildModuleBase
    {
        private readonly IMediator _mediator;

        public EmporiumUserComponentModule(IMediator mediator)
        {
            _mediator = mediator;
        }

        [SelectionCommand("rate-owner:*:*:*")]
        public async Task<IResult> RateOwner(Snowflake owner, Snowflake buyer, Snowflake message, string[] selectedValue)
        {
            if (Context.AuthorId != buyer) 
                return Response(
                        new LocalInteractionMessageResponse()
                            .WithIsEphemeral()
                            .WithContent($"Only {Mention.User(buyer)} can rate this transaction"));


            if (await Context.Bot.FetchMessageAsync(Context.ChannelId, message) is not IUserMessage userMessage)
                return Response(
                    new LocalInteractionMessageResponse()
                            .WithIsEphemeral()
                            .WithContent("Unable to retrieve transaction results. Please try again"));

            var rating = int.Parse(selectedValue[0]);

            await Context.Interaction.Response().ModifyMessageAsync(new LocalInteractionMessageResponse().WithComponents());
            
            await _mediator.Send(new CreateUserReviewCommand(new EmporiumId(Context.GuildId), ReferenceNumber.Create(owner), rating));
            
            await userMessage.ModifyAsync(x => x.Embeds = new[] { LocalEmbed.CreateFrom(userMessage.Embeds[0]).WithFooter("✅") });

            return Results.Success;
        }
    }
}
