using Agora.Addons.Disqord.Parsers;
using Disqord;
using Disqord.Models;
using Disqord.Rest;
using Disqord.Serialization.Json.Default;
using Emporia.Application.Features.Commands;
using Emporia.Domain.Common;
using Emporia.Domain.Extension;
using Emporia.Extensions.Discord;
using FluentValidation;
using HumanTimeParser.Core.Parsing;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Qommon;

namespace Agora.Addons.Disqord
{
    public partial class PersistentInteractionService
    {
        private async Task<IBaseRequest> HandleModalInteraction(IModalSubmitInteraction modalInteraction)
        {
            var emporiumId = new EmporiumId(modalInteraction.GuildId.Value);
            var showroomId = new ShowroomId(modalInteraction.ChannelId);
            var keys = modalInteraction.CustomId.Split(':');

            switch (keys[0])
            {
                case string x when x.StartsWith("extend"):
                    return await ExtendListing(modalInteraction, emporiumId, showroomId, keys);
                case "editAuction":
                    return EditAuctionListing(modalInteraction, emporiumId, showroomId, keys);
                case "editMarket":
                    return EditMarketListing(modalInteraction, emporiumId, showroomId, keys);
                case "editTrade":
                    return EditTradeListing(modalInteraction, emporiumId, showroomId, keys);
                case "claim":
                    return ClaimListing(modalInteraction, emporiumId, showroomId, keys);
                default:
                    break;
            }

            throw new NotImplementedException();
        }

        private static Task HandleResponse(IModalSubmitInteraction interaction) => interaction.CustomId switch
        {
            { } when interaction.CustomId.StartsWith("extend")
                    => interaction.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithContent("Product listing extended!").WithIsEphemeral(true)),
            { } when interaction.CustomId.StartsWith("edit")
                    => interaction.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithContent("Product listing successfully updated!").WithIsEphemeral(true)),
            { } when interaction.CustomId.StartsWith("claim")
                    => interaction.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithContent("Congratulations on your purchase!").WithIsEphemeral(true)),
            _ => Task.CompletedTask
        };

        private async Task<IBaseRequest> ExtendListing(IModalSubmitInteraction modalInteraction, EmporiumId emporiumId, ShowroomId showroomId, string[] keys)
        {
            var rows = modalInteraction.Components.OfType<IRowComponent>();
            var selection = rows.First().Components.OfType<ISelectionComponent>().First() as ITransientEntity<ComponentJsonModel>;
            var option = selection.Model.Values.GetValueOrDefault()[0]; //["values"].ToType<DefaultJsonArray>()[0].ToString();
            var text = rows.Last().Components.OfType<ITextInputComponent>().First().Value;

            var emporium = await Client.Services.GetRequiredService<IEmporiaCacheService>().GetEmporiumAsync(emporiumId.Value);
            var settings = await Client.Services.GetRequiredService<IGuildSettingsService>().GetGuildSettingsAsync(emporiumId.Value);
            var result = Client.Services.GetRequiredService<EmporiumTimeParser>().WithOffset(emporium.TimeOffset).Parse(text);

            if (result is not ISuccessfulTimeParsingResult<DateTime> successfulResult) throw new ValidationException("Invalid extension format.");

            if (option == "duration")
            {
                var extension = successfulResult.Value - emporium.LocalTime.DateTime;

                return new ExtendListingCommand(emporiumId, showroomId, ReferenceNumber.Create(ulong.Parse(keys[1])), keys[0].Replace("extend", ""))
                {
                    Limit = settings.MaximumDuration,
                    Duration = extension
                };
            }
            else
            {
                return new ExtendListingCommand(emporiumId, showroomId, ReferenceNumber.Create(ulong.Parse(keys[1])), keys[0].Replace("extend", ""))
                {
                    Limit = settings.MaximumDuration,
                    ExpirationDate = successfulResult.Value
                };
            }
        }

        private static IBaseRequest EditAuctionListing(IModalSubmitInteraction modalInteraction, EmporiumId emporiumId, ShowroomId showroomId, string[] keys)
        {
            var rows = modalInteraction.Components
                .OfType<IRowComponent>()
                .Select(row => row.Components.OfType<ITextInputComponent>().First())
                .ToDictionary(key => key.CustomId, value => value.Value);

            _ = decimal.TryParse(rows["minIncrease"], out var minIncrease);
            _ = decimal.TryParse(rows["maxIncrease"], out var maxIncrease);

            return new UpdateAuctionItemCommand(emporiumId, showroomId, ReferenceNumber.Create(ulong.Parse(keys[1])))
            {
                ImageUrls = rows["image"].IsNull() ? null : new[] { rows["image"] },
                Message = rows["message"].IsNull() ? null : HiddenMessage.Create(rows["message"]),
                Description = rows["description"].IsNull() ? null : ProductDescription.Create(rows["description"]),
                MinBidIncrease = rows["minIncrease"].IsNull() ? null : minIncrease,
                MaxBidIncrease = rows["maxIncrease"].IsNull() ? null : maxIncrease,
            };
        }

        private static IBaseRequest EditMarketListing(IModalSubmitInteraction modalInteraction, EmporiumId emporiumId, ShowroomId showroomId, string[] keys)
        {
            var rows = modalInteraction.Components
                .OfType<IRowComponent>()
                .Select(row => row.Components.OfType<ITextInputComponent>().First())
                .Where(component => component is not null)
                .ToDictionary(key => key.CustomId, value => value.Value);

            return new UpdateMarketItemCommand(emporiumId, showroomId, ReferenceNumber.Create(ulong.Parse(keys[1])))
            {
                ImageUrls = rows["image"].IsNull() ? null : new[] { rows["image"] },
                Message = rows["message"].IsNull() ? null : HiddenMessage.Create(rows["message"]),
                Description = rows["description"].IsNull() ? null : ProductDescription.Create(rows["description"]),
            };
        }

        private static IBaseRequest EditTradeListing(IModalSubmitInteraction modalInteraction, EmporiumId emporiumId, ShowroomId showroomId, string[] keys)
        {
            var rows = modalInteraction.Components
                .OfType<IRowComponent>()
                .Select(row => row.Components.OfType<ITextInputComponent>().First())
                .Where(component => component is not null)
                .ToDictionary(key => key.CustomId, value => value.Value);

            return new UpdateTradeItemCommand(emporiumId, showroomId, ReferenceNumber.Create(ulong.Parse(keys[1])))
            {
                ImageUrls = rows["image"].IsNull() ? null : new[] { rows["image"] },
                Message = rows["message"].IsNull() ? null : HiddenMessage.Create(rows["message"]),
                Description = rows["description"].IsNull() ? null : ProductDescription.Create(rows["description"]),
            };
        }

        private static IBaseRequest ClaimListing(IModalSubmitInteraction modalInteraction, EmporiumId emporiumId, ShowroomId showroomId, string[] keys)
        {
            var input = modalInteraction.Components
                .OfType<IRowComponent>().First()
                .Components.OfType<ITextInputComponent>().First().Value;

            if (int.TryParse(input, out var items))
                return new CreatePaymentCommand(emporiumId, showroomId, ReferenceNumber.Create(ulong.Parse(keys[1]))) { ItemCount = items };
            else
                throw new ValidationException("Claim amount must be a number!");
        }
    }
}
