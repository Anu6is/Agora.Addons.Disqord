﻿using Agora.Addons.Disqord.Parsers;
using Disqord;
using Disqord.Rest;
using Emporia.Application.Features.Commands;
using Emporia.Domain.Common;
using Emporia.Domain.Extension;
using Emporia.Domain.Services;
using Emporia.Extensions.Discord;
using FluentValidation;
using HumanTimeParser.Core.Parsing;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Agora.Addons.Disqord
{
    public partial class PersistentInteractionService
    {
        private async Task<IResult<IBaseRequest>> HandleModalInteraction(IModalSubmitInteraction modalInteraction, ulong roomId)
        {
            var emporiumId = new EmporiumId(modalInteraction.GuildId.Value);
            var showroomId = new ShowroomId(roomId);
            var keys = modalInteraction.CustomId.Split(':');

            switch (keys[0])
            {
                case string x when x.StartsWith("extend"):
                    return await ExtendListing(modalInteraction, emporiumId, showroomId, keys);
                case "editGiveaway":
                    return EditGiveawayListing(modalInteraction, emporiumId, showroomId, keys);
                case "editAuction":
                    return EditAuctionListing(modalInteraction, emporiumId, showroomId, keys);
                case "editMarket":
                    return EditMarketListing(modalInteraction, emporiumId, showroomId, keys);
                case "editTrade":
                    return EditTradeListing(modalInteraction, emporiumId, showroomId, keys);
                case "bestOffer":
                    return SubmitMarketOffer(modalInteraction, emporiumId, showroomId, keys);
                case "custombid":
                    return SubmitCustomBid(modalInteraction, emporiumId, showroomId, keys);
                case "barter":
                    return SubmitTradeOffer(modalInteraction, emporiumId, showroomId, keys);
                case "claim":
                    return ClaimListing(modalInteraction, emporiumId, showroomId, keys);
                default:
                    break;
            }

            throw new NotImplementedException();
        }

        private static Task HandleResponse(IModalSubmitInteraction interaction) => interaction.CustomId switch
        {
            { } when interaction.Response().HasResponded => Task.CompletedTask,
            { } when interaction.CustomId.StartsWith("extend")
                    => interaction.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithContent("Product listing extended!").WithIsEphemeral()),
            { } when interaction.CustomId.StartsWith("edit")
                    => interaction.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithContent("Product listing successfully updated!").WithIsEphemeral()),
            { } when interaction.CustomId.StartsWith("claim")
                    => interaction.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithContent("Congratulations on your purchase!").WithIsEphemeral()),
            { } when interaction.CustomId.StartsWith("barter")
                    => interaction.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithContent("Offer successfully submitted!").WithIsEphemeral()),
            { } when interaction.CustomId.StartsWith("bestOffer")
                    => interaction.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithContent("Offer successfully submitted!").WithIsEphemeral()),
            { } when interaction.CustomId.StartsWith("custombid")
                    => interaction.Response().SendMessageAsync(new LocalInteractionMessageResponse().WithContent("Bid successfully submitted!").WithIsEphemeral()),
            _ => Task.CompletedTask
        };

        private async Task<IResult<IBaseRequest>> ExtendListing(IModalSubmitInteraction modalInteraction, EmporiumId emporiumId, ShowroomId showroomId, string[] keys)
        {
            var rows = modalInteraction.Components.OfType<IRowComponent>().ToArray();
            var extendTo = rows[0].Components.OfType<ITextInputComponent>().First().Value;
            var extendBy = rows[1].Components.OfType<ITextInputComponent>().First().Value;

            var emporium = await Client.Services.GetRequiredService<IEmporiaCacheService>().GetEmporiumAsync(emporiumId.Value);
            var settings = await Client.Services.GetRequiredService<IGuildSettingsService>().GetGuildSettingsAsync(emporiumId.Value);

            if (extendBy.IsNotNull() && extendTo.IsNotNull()) return Result<IBaseRequest>.Failure("Invalid Input: Provide only one extension end option.");

            if (extendTo.IsNotNull())
            {
                var result = Client.Services.GetRequiredService<EmporiumTimeParser>().WithOffset(emporium.TimeOffset).Parse(extendTo);

                if (result is not ISuccessfulTimeParsingResult<DateTime> successfulResult) return Result<IBaseRequest>.Failure("Invalid extension format.");

                return Result.Success(new ExtendListingCommand(emporiumId, showroomId, ReferenceNumber.Create(ulong.Parse(keys[1])), keys[0].Replace("extend", ""))
                {
                    Limit = settings.MaximumDuration,
                    ExpirationDate = new DateTimeOffset(successfulResult.Value, emporium.TimeOffset).UtcDateTime,
                });
            }
            else if (extendBy.IsNotNull())
            {
                var result = Client.Services.GetRequiredService<EmporiumTimeParser>().WithOffset(emporium.TimeOffset).Parse(extendBy);

                if (result is not ISuccessfulTimeParsingResult<DateTime> successfulResult) return Result<IBaseRequest>.Failure("Invalid extension format.");

                var extension = successfulResult.Value - emporium.LocalTime.DateTime;

                return Result.Success(new ExtendListingCommand(emporiumId, showroomId, ReferenceNumber.Create(ulong.Parse(keys[1])), keys[0].Replace("extend", ""))
                {
                    Limit = settings.MaximumDuration,
                    Duration = extension
                });
            }

            return Result<IBaseRequest>.Failure("An extension value must be included");
        }

        private static IResult<IBaseRequest> EditAuctionListing(IModalSubmitInteraction modalInteraction, EmporiumId emporiumId, ShowroomId showroomId, string[] keys)
        {
            var rows = modalInteraction.Components
                .OfType<IRowComponent>()
                .Select(row => row.Components.OfType<ITextInputComponent>().First())
                .ToDictionary(key => key.CustomId, value => value.Value);

            _ = decimal.TryParse(rows["minIncrease"], out var minIncrease);
            _ = decimal.TryParse(rows["maxIncrease"], out var maxIncrease);

            return Result.Success(new UpdateAuctionItemCommand(emporiumId, showroomId, ReferenceNumber.Create(ulong.Parse(keys[1])))
            {
                ImageUrls = rows["image"].IsNull() ? null : new[] { rows["image"] },
                Message = rows["message"].IsNull() ? null : HiddenMessage.Create(rows["message"]),
                Description = rows["description"].IsNull() ? null : ProductDescription.Create(rows["description"]),
                MinBidIncrease = rows["minIncrease"].IsNull() ? null : minIncrease,
                MaxBidIncrease = rows["maxIncrease"].IsNull() ? null : maxIncrease,
            });
        }

        private static IResult<IBaseRequest> EditMarketListing(IModalSubmitInteraction modalInteraction, EmporiumId emporiumId, ShowroomId showroomId, string[] keys)
        {
            var rows = modalInteraction.Components
                .OfType<IRowComponent>()
                .Select(row => row.Components.OfType<ITextInputComponent>().First())
                .Where(component => component is not null)
                .ToDictionary(key => key.CustomId, value => value.Value);

            _ = decimal.TryParse(rows["price"], out var price);

            return Result.Success(new UpdateMarketItemCommand(emporiumId, showroomId, ReferenceNumber.Create(ulong.Parse(keys[1])))
            {
                Title = rows["title"].IsNull() ? null : ProductTitle.Create(rows["title"]),
                Price = rows["price"].IsNull() ? 0 : price,
                ImageUrls = rows["image"].IsNull() ? null : new[] { rows["image"] },
                Message = rows["message"].IsNull() ? null : HiddenMessage.Create(rows["message"]),
                Description = rows["description"].IsNull() ? null : ProductDescription.Create(rows["description"]),
            });
        }

        private static IResult<IBaseRequest> EditTradeListing(IModalSubmitInteraction modalInteraction, EmporiumId emporiumId, ShowroomId showroomId, string[] keys)
        {
            var rows = modalInteraction.Components
                .OfType<IRowComponent>()
                .Select(row => row.Components.OfType<ITextInputComponent>().First())
                .Where(component => component is not null)
                .ToDictionary(key => key.CustomId, value => value.Value);

            return Result.Success(new UpdateTradeItemCommand(emporiumId, showroomId, ReferenceNumber.Create(ulong.Parse(keys[1])))
            {
                ImageUrls = rows["image"].IsNull() ? null : new[] { rows["image"] },
                Message = rows["message"].IsNull() ? null : HiddenMessage.Create(rows["message"]),
                Description = rows["description"].IsNull() ? null : ProductDescription.Create(rows["description"]),
            });
        }

        private static IResult<IBaseRequest> EditGiveawayListing(IModalSubmitInteraction modalInteraction, EmporiumId emporiumId, ShowroomId showroomId, string[] keys)
        {
            var rows = modalInteraction.Components
                .OfType<IRowComponent>()
                .Select(row => row.Components.OfType<ITextInputComponent>().First())
                .Where(component => component is not null)
                .ToDictionary(key => key.CustomId, value => value.Value);

            return Result.Success(new UpdateGiveawayItemCommand(emporiumId, showroomId, ReferenceNumber.Create(ulong.Parse(keys[1])))
            {
                Title = rows["title"].IsNull() ? null : ProductTitle.Create(rows["title"]),
                ImageUrls = rows["image"].IsNull() ? null : new[] { rows["image"] },
                Message = rows["message"].IsNull() ? null : HiddenMessage.Create(rows["message"]),
                Description = rows["description"].IsNull() ? null : ProductDescription.Create(rows["description"]),
            });
        }

        private static IResult<IBaseRequest> SubmitMarketOffer(IModalSubmitInteraction modalInteraction, EmporiumId emporiumId, ShowroomId showroomId, string[] keys)
        {
            var input = modalInteraction.Components
                .OfType<IRowComponent>().First()
                .Components.OfType<ITextInputComponent>().First().Value;

            if (!decimal.TryParse(input, out var offer)) return Result<IBaseRequest>.Failure("Offer amount must be a number!");

            return Result.Success(new CreatePaymentCommand(emporiumId, showroomId, ReferenceNumber.Create(ulong.Parse(keys[1]))) { Offer = offer });
        }

        private static IResult<IBaseRequest> SubmitCustomBid(IModalSubmitInteraction modalInteraction, EmporiumId emporiumId, ShowroomId showroomId, string[] keys)
        {
            var input = modalInteraction.Components
                .OfType<IRowComponent>().First()
                .Components.OfType<ITextInputComponent>().First().Value;

            if (!decimal.TryParse(input, out var amount)) return Result<IBaseRequest>.Failure("Bid amount must be a number!");

            if (amount <= 0) return Result<IBaseRequest>.Failure("Bid amount must be greater than 0");

            return Result.Success(new CreateBidCommand(emporiumId, showroomId, ReferenceNumber.Create(ulong.Parse(keys[1])), amount));
        }

        private static IResult<IBaseRequest> SubmitTradeOffer(IModalSubmitInteraction modalInteraction, EmporiumId emporiumId, ShowroomId showroomId, string[] keys)
        {
            var rows = modalInteraction.Components
                .OfType<IRowComponent>()
                .Select(row => row.Components.OfType<ITextInputComponent>().First())
                .Where(component => component is not null)
                .ToDictionary(key => key.CustomId, value => value.Value);

            return Result.Success(new CreateDealCommand(emporiumId, showroomId, ReferenceNumber.Create(ulong.Parse(keys[1])), rows["offer"])
            {
                Details = rows["details"]
            });
        }

        private static IResult<IBaseRequest> ClaimListing(IModalSubmitInteraction modalInteraction, EmporiumId emporiumId, ShowroomId showroomId, string[] keys)
        {
            var input = modalInteraction.Components
                .OfType<IRowComponent>().First()
                .Components.OfType<ITextInputComponent>().First().Value;

            if (!int.TryParse(input, out var items)) return Result<IBaseRequest>.Failure("Claim amount must be a number!");

            return Result.Success(new CreatePaymentCommand(emporiumId, showroomId, ReferenceNumber.Create(ulong.Parse(keys[1]))) { ItemCount = items });
        }
    }
}
