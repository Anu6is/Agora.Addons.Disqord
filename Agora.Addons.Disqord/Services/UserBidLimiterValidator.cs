using Emporia.Application.Features.Commands;
using Emporia.Domain.Common;
using Emporia.Domain.Entities;
using FluentValidation;

namespace Agora.Addons.Disqord
{
    public class UserBidLimiterValidator : AbstractValidator<CreateBidCommand>
    {
        private readonly UserBidCacheService _bidCacheService;

        public UserBidLimiterValidator(UserBidCacheService bidCacheService)
        {
            _bidCacheService = bidCacheService;

            RuleFor(command => command.CurrentUser)
                .NotEmpty().WithMessage("User not found")
                .MustAsync(NotSpam).WithMessage("High bidding activity detected. Please wait a 1 second before your next bid...⌛");
        }

        private async Task<bool> NotSpam(CreateBidCommand command, EmporiumUser user, CancellationToken cancellationToken)
        {
            if (await _bidCacheService.GetLastBidAsync(user.ReferenceNumber.Value) is not null) return false;

            await _bidCacheService.AddBidAsync(new CachedBid(user, command.ListingId, command.Amount));

            return true;
        }
    }

    public record CachedBid(EmporiumUser User, ListingId ListingId, decimal Amount);
}
