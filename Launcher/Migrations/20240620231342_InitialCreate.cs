using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Launcher.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Emporium",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false),
                    UtcOffset = table.Column<TimeSpan>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Emporium", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuctionTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EmporiumId = table.Column<long>(type: "INTEGER", nullable: true),
                    AuthorId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    Type = table.Column<string>(type: "TEXT", nullable: true),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    StartingPrice = table.Column<double>(type: "REAL", nullable: false),
                    ReservePrice = table.Column<double>(type: "REAL", nullable: false),
                    BuyNowPrice = table.Column<double>(type: "REAL", nullable: false),
                    Currency = table.Column<string>(type: "TEXT", nullable: true),
                    MinBidIncrease = table.Column<double>(type: "REAL", nullable: false),
                    MaxBidIncrease = table.Column<double>(type: "REAL", nullable: false),
                    MaxParticipants = table.Column<int>(type: "INTEGER", nullable: false),
                    Category = table.Column<string>(type: "TEXT", nullable: true),
                    Subcategory = table.Column<string>(type: "TEXT", nullable: true),
                    Duration = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    Timeout = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    Message = table.Column<string>(type: "TEXT", nullable: true),
                    Image = table.Column<string>(type: "TEXT", nullable: true),
                    Owner = table.Column<ulong>(type: "INTEGER", nullable: false),
                    Anonymous = table.Column<bool>(type: "INTEGER", nullable: false),
                    ReverseBidding = table.Column<bool>(type: "INTEGER", nullable: false),
                    Reschedule = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuctionTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuctionTemplates_Emporium_EmporiumId",
                        column: x => x.EmporiumId,
                        principalTable: "Emporium",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Category",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EmporiumId = table.Column<long>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Category", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Category_Emporium_EmporiumId",
                        column: x => x.EmporiumId,
                        principalTable: "Emporium",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Currency",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Code = table.Column<string>(type: "TEXT", nullable: true),
                    Symbol = table.Column<string>(type: "TEXT", nullable: true),
                    DecimalDigits = table.Column<int>(type: "INTEGER", nullable: false),
                    Format = table.Column<int>(type: "INTEGER", nullable: false),
                    EmporiumId = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Currency", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Currency_Emporium_EmporiumId",
                        column: x => x.EmporiumId,
                        principalTable: "Emporium",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Economy",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EmporiumId = table.Column<long>(type: "INTEGER", nullable: true),
                    UserReference = table.Column<long>(type: "INTEGER", nullable: true),
                    Balance = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Economy", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_Economy_Emporium_EmporiumId",
                        column: x => x.EmporiumId,
                        principalTable: "Emporium",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmporiumUser",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EmporiumId = table.Column<long>(type: "INTEGER", nullable: false),
                    LastActive = table.Column<long>(type: "INTEGER", nullable: false),
                    ReferenceNumber = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmporiumUser", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmporiumUser_Emporium_EmporiumId",
                        column: x => x.EmporiumId,
                        principalTable: "Emporium",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GuildSettings",
                columns: table => new
                {
                    GuildId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    Listings = table.Column<ulong>(type: "INTEGER", nullable: false),
                    AdminRole = table.Column<ulong>(type: "INTEGER", nullable: false),
                    BuyerRole = table.Column<ulong>(type: "INTEGER", nullable: false),
                    BrokerRole = table.Column<ulong>(type: "INTEGER", nullable: false),
                    MerchantRole = table.Column<ulong>(type: "INTEGER", nullable: false),
                    AuditLogChannelId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    ResultLogChannelId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    Flags = table.Column<ulong>(type: "INTEGER", nullable: true),
                    UtcOffset = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    EconomyType = table.Column<string>(type: "TEXT", nullable: true),
                    DefaultBalance = table.Column<double>(type: "REAL", nullable: false),
                    SnipeRange = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    SnipeExtension = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    MinimumDuration = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    MaximumDuration = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    DefaultDuration = table.Column<TimeSpan>(type: "TEXT", nullable: false),
                    MinimumDurationDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    BiddingRecallLimit = table.Column<TimeSpan>(type: "TEXT", nullable: false, defaultValue: new TimeSpan(0, 0, 0, 30, 0)),
                    MaxListingsLimit = table.Column<int>(type: "INTEGER", nullable: false),
                    MinBidIncrease_Amount = table.Column<decimal>(type: "TEXT", nullable: true),
                    MinBidIncrease_DeltaType = table.Column<int>(type: "INTEGER", nullable: true),
                    DefaultCurrency = table.Column<string>(type: "TEXT", nullable: true),
                    AllowedListings = table.Column<string>(type: "TEXT", nullable: false),
                    AvailableRooms = table.Column<string>(type: "TEXT", nullable: false),
                    ExternalApiKeys = table.Column<string>(type: "TEXT", nullable: true),
                    EmporiumId = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildSettings", x => x.GuildId);
                    table.ForeignKey(
                        name: "FK_GuildSettings_Emporium_EmporiumId",
                        column: x => x.EmporiumId,
                        principalTable: "Emporium",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ListingRequirements",
                columns: table => new
                {
                    GuildId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    ListingType = table.Column<int>(type: "INTEGER", nullable: false),
                    Image = table.Column<bool>(type: "INTEGER", nullable: false),
                    Description = table.Column<bool>(type: "INTEGER", nullable: false),
                    Category = table.Column<bool>(type: "INTEGER", nullable: false),
                    Subcategory = table.Column<bool>(type: "INTEGER", nullable: false),
                    Message = table.Column<bool>(type: "INTEGER", nullable: false),
                    MaxBidIncrease = table.Column<bool>(type: "INTEGER", nullable: false),
                    EmporiumId = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ListingRequirements", x => new { x.GuildId, x.ListingType });
                    table.ForeignKey(
                        name: "FK_ListingRequirements_Emporium_EmporiumId",
                        column: x => x.EmporiumId,
                        principalTable: "Emporium",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Showroom",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false),
                    EmporiumId = table.Column<long>(type: "INTEGER", nullable: false),
                    ListingType = table.Column<string>(type: "TEXT", nullable: false, collation: "NOCASE"),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    ActiveHours_OpensAt = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    ActiveHours_ClosesAt = table.Column<TimeSpan>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Showroom", x => new { x.EmporiumId, x.Id, x.ListingType });
                    table.ForeignKey(
                        name: "FK_Showroom_Emporium_EmporiumId",
                        column: x => x.EmporiumId,
                        principalTable: "Emporium",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserProfile",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EmporiumId = table.Column<long>(type: "INTEGER", nullable: true),
                    UserReference = table.Column<long>(type: "INTEGER", nullable: true),
                    OutbidAlerts = table.Column<bool>(type: "INTEGER", nullable: false),
                    TradeDealAlerts = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    Reviews = table.Column<ulong>(type: "INTEGER", nullable: false),
                    Rating = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserProfile", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserProfile_Emporium_EmporiumId",
                        column: x => x.EmporiumId,
                        principalTable: "Emporium",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubCategory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CategoryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: true, collation: "NOCASE")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubCategory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubCategory_Category_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Category",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserReview",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EntityId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Rating = table.Column<int>(type: "INTEGER", nullable: false),
                    Comment = table.Column<string>(type: "TEXT", nullable: true),
                    SubmittedBy = table.Column<Guid>(type: "TEXT", nullable: false),
                    SubmittedOn = table.Column<long>(type: "INTEGER", nullable: false),
                    ReferenceNumber = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserReview", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserReview_EmporiumUser_EntityId",
                        column: x => x.EntityId,
                        principalTable: "EmporiumUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductListing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ShowroomId = table.Column<long>(type: "INTEGER", nullable: false),
                    Anonymous = table.Column<bool>(type: "INTEGER", nullable: false),
                    OwnerId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ScheduledStart = table.Column<long>(type: "INTEGER", nullable: false),
                    ScheduledEnd = table.Column<long>(type: "INTEGER", nullable: false),
                    ReferenceCode = table.Column<string>(type: "TEXT", nullable: true),
                    HiddenMessage = table.Column<string>(type: "TEXT", nullable: true),
                    ExpirationDate = table.Column<long>(type: "INTEGER", nullable: false),
                    ReschedulingChoice = table.Column<int>(type: "INTEGER", nullable: false),
                    AccessRoles = table.Column<string>(type: "TEXT", nullable: true),
                    ValueTag = table.Column<string>(type: "TEXT", nullable: true),
                    EmporiumId = table.Column<long>(type: "INTEGER", nullable: false),
                    Listing = table.Column<string>(type: "TEXT", nullable: false),
                    ListingType = table.Column<string>(type: "TEXT", nullable: false),
                    Discount = table.Column<int>(type: "INTEGER", nullable: true),
                    DiscountValue = table.Column<double>(type: "REAL", nullable: true),
                    DiscountEndDate = table.Column<long>(type: "INTEGER", nullable: true),
                    Timeout = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    BuyNowPrice = table.Column<string>(type: "TEXT", nullable: true),
                    CostPerItem = table.Column<string>(type: "TEXT", nullable: true),
                    CostPerBundle = table.Column<string>(type: "TEXT", nullable: true),
                    AmountPerBundle = table.Column<int>(type: "INTEGER", nullable: true),
                    MultiItemMarket_CostPerItem = table.Column<string>(type: "TEXT", nullable: true),
                    MultiItemMarket_CostPerBundle = table.Column<string>(type: "TEXT", nullable: true),
                    MultiItemMarket_AmountPerBundle = table.Column<int>(type: "INTEGER", nullable: true),
                    MaxTicketsPerUser = table.Column<uint>(type: "INTEGER", nullable: true),
                    StandardAuction_BuyNowPrice = table.Column<string>(type: "TEXT", nullable: true),
                    AllowOffers = table.Column<bool>(type: "INTEGER", nullable: true),
                    StandardMarket_Discount = table.Column<int>(type: "INTEGER", nullable: true),
                    StandardMarket_DiscountValue = table.Column<double>(type: "REAL", nullable: true),
                    SelectedOffer = table.Column<string>(type: "TEXT", nullable: true),
                    StandardTrade_AllowOffers = table.Column<bool>(type: "INTEGER", nullable: true),
                    MaxParticipants = table.Column<uint>(type: "INTEGER", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedOn = table.Column<long>(type: "INTEGER", nullable: false),
                    LastModifiedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    LastModifiedOn = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductListing", x => x.Id);
                    table.UniqueConstraint("AK_ProductListing_ProductId", x => x.ProductId);
                    table.ForeignKey(
                        name: "FK_ProductListing_EmporiumUser_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "EmporiumUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductListing_Showroom_EmporiumId_ShowroomId_ListingType",
                        columns: x => new { x.EmporiumId, x.ShowroomId, x.ListingType },
                        principalTable: "Showroom",
                        principalColumns: new[] { "EmporiumId", "Id", "ListingType" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AcceptedOffer",
                columns: table => new
                {
                    ListingId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SubmittedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Submission = table.Column<string>(type: "TEXT", nullable: true),
                    SubmittedOn = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UserReference = table.Column<long>(type: "INTEGER", nullable: true),
                    Id = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcceptedOffer", x => x.ListingId);
                    table.ForeignKey(
                        name: "FK_AcceptedOffer_EmporiumUser_SubmittedBy",
                        column: x => x.SubmittedBy,
                        principalTable: "EmporiumUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AcceptedOffer_ProductListing_ListingId",
                        column: x => x.ListingId,
                        principalTable: "ProductListing",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Product",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    ReferenceNumber = table.Column<long>(type: "INTEGER", nullable: true),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: true),
                    SubCategoryId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Product", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Product_ProductListing_Id",
                        column: x => x.Id,
                        principalTable: "ProductListing",
                        principalColumn: "ProductId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AuctionItem",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CurrentPrice = table.Column<string>(type: "TEXT", nullable: true),
                    StartingPrice = table.Column<string>(type: "TEXT", nullable: true),
                    ReservePrice = table.Column<string>(type: "TEXT", nullable: true),
                    IsReversed = table.Column<bool>(type: "INTEGER", nullable: false),
                    BidIncrement = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuctionItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuctionItem_Product_Id",
                        column: x => x.Id,
                        principalTable: "Product",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GiveawayItem",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TicketPrice = table.Column<string>(type: "TEXT", nullable: true),
                    MaxParticipants = table.Column<uint>(type: "INTEGER", nullable: false),
                    TotalWinners = table.Column<uint>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GiveawayItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GiveawayItem_Product_Id",
                        column: x => x.Id,
                        principalTable: "Product",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Image",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Url = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Image", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Image_Product_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Product",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MarketItem",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Price = table.Column<string>(type: "TEXT", nullable: true),
                    CurrentPrice = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarketItem_Product_Id",
                        column: x => x.Id,
                        principalTable: "Product",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TradeItem",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SuggestedOffer = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradeItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TradeItem_Product_Id",
                        column: x => x.Id,
                        principalTable: "Product",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Bid",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Amount = table.Column<string>(type: "TEXT", nullable: true),
                    Submission = table.Column<string>(type: "TEXT", nullable: true),
                    SubmittedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SubmittedOn = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UserReference = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bid", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bid_AuctionItem_ProductId",
                        column: x => x.ProductId,
                        principalTable: "AuctionItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Bid_EmporiumUser_SubmittedBy",
                        column: x => x.SubmittedBy,
                        principalTable: "EmporiumUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Ticket",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Number = table.Column<string>(type: "TEXT", nullable: true),
                    Submission = table.Column<string>(type: "TEXT", nullable: true),
                    SubmittedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SubmittedOn = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UserReference = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ticket", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ticket_EmporiumUser_SubmittedBy",
                        column: x => x.SubmittedBy,
                        principalTable: "EmporiumUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Ticket_GiveawayItem_ProductId",
                        column: x => x.ProductId,
                        principalTable: "GiveawayItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Payment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Amount = table.Column<string>(type: "TEXT", nullable: true),
                    ItemCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Submission = table.Column<string>(type: "TEXT", nullable: true),
                    SubmittedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SubmittedOn = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UserReference = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payment_EmporiumUser_SubmittedBy",
                        column: x => x.SubmittedBy,
                        principalTable: "EmporiumUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Payment_MarketItem_ProductId",
                        column: x => x.ProductId,
                        principalTable: "MarketItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TradeItem_Offers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Details = table.Column<string>(type: "TEXT", nullable: true),
                    Submission = table.Column<string>(type: "TEXT", nullable: true),
                    SubmittedBy = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProductId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SubmittedOn = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UserReference = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradeItem_Offers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TradeItem_Offers_EmporiumUser_SubmittedBy",
                        column: x => x.SubmittedBy,
                        principalTable: "EmporiumUser",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TradeItem_Offers_TradeItem_ProductId",
                        column: x => x.ProductId,
                        principalTable: "TradeItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AcceptedOffer_SubmittedBy",
                table: "AcceptedOffer",
                column: "SubmittedBy");

            migrationBuilder.CreateIndex(
                name: "IX_AuctionTemplates_EmporiumId",
                table: "AuctionTemplates",
                column: "EmporiumId");

            migrationBuilder.CreateIndex(
                name: "IX_Bid_ProductId",
                table: "Bid",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Bid_SubmittedBy",
                table: "Bid",
                column: "SubmittedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Category_EmporiumId",
                table: "Category",
                column: "EmporiumId");

            migrationBuilder.CreateIndex(
                name: "IX_Currency_EmporiumId",
                table: "Currency",
                column: "EmporiumId");

            migrationBuilder.CreateIndex(
                name: "IX_Economy_EmporiumId",
                table: "Economy",
                column: "EmporiumId");

            migrationBuilder.CreateIndex(
                name: "IX_Economy_UserReference",
                table: "Economy",
                column: "UserReference");

            migrationBuilder.CreateIndex(
                name: "IX_EmporiumUser_EmporiumId_ReferenceNumber",
                table: "EmporiumUser",
                columns: new[] { "EmporiumId", "ReferenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuildSettings_EmporiumId",
                table: "GuildSettings",
                column: "EmporiumId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Image_ProductId",
                table: "Image",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ListingRequirements_EmporiumId",
                table: "ListingRequirements",
                column: "EmporiumId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payment_ProductId",
                table: "Payment",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Payment_SubmittedBy",
                table: "Payment",
                column: "SubmittedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Product_ReferenceNumber",
                table: "Product",
                column: "ReferenceNumber");

            migrationBuilder.CreateIndex(
                name: "IX_ProductListing_EmporiumId_ShowroomId_ListingType",
                table: "ProductListing",
                columns: new[] { "EmporiumId", "ShowroomId", "ListingType" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductListing_OwnerId",
                table: "ProductListing",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductListing_ReferenceCode",
                table: "ProductListing",
                column: "ReferenceCode");

            migrationBuilder.CreateIndex(
                name: "IX_SubCategory_CategoryId",
                table: "SubCategory",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Ticket_ProductId",
                table: "Ticket",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Ticket_SubmittedBy",
                table: "Ticket",
                column: "SubmittedBy");

            migrationBuilder.CreateIndex(
                name: "IX_TradeItem_Offers_ProductId",
                table: "TradeItem_Offers",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_TradeItem_Offers_SubmittedBy",
                table: "TradeItem_Offers",
                column: "SubmittedBy");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfile_EmporiumId",
                table: "UserProfile",
                column: "EmporiumId");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfile_UserReference",
                table: "UserProfile",
                column: "UserReference");

            migrationBuilder.CreateIndex(
                name: "IX_UserReview_EntityId",
                table: "UserReview",
                column: "EntityId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AcceptedOffer");

            migrationBuilder.DropTable(
                name: "AuctionTemplates");

            migrationBuilder.DropTable(
                name: "Bid");

            migrationBuilder.DropTable(
                name: "Currency");

            migrationBuilder.DropTable(
                name: "Economy");

            migrationBuilder.DropTable(
                name: "GuildSettings");

            migrationBuilder.DropTable(
                name: "Image");

            migrationBuilder.DropTable(
                name: "ListingRequirements");

            migrationBuilder.DropTable(
                name: "Payment");

            migrationBuilder.DropTable(
                name: "SubCategory");

            migrationBuilder.DropTable(
                name: "Ticket");

            migrationBuilder.DropTable(
                name: "TradeItem_Offers");

            migrationBuilder.DropTable(
                name: "UserProfile");

            migrationBuilder.DropTable(
                name: "UserReview");

            migrationBuilder.DropTable(
                name: "AuctionItem");

            migrationBuilder.DropTable(
                name: "MarketItem");

            migrationBuilder.DropTable(
                name: "Category");

            migrationBuilder.DropTable(
                name: "GiveawayItem");

            migrationBuilder.DropTable(
                name: "TradeItem");

            migrationBuilder.DropTable(
                name: "Product");

            migrationBuilder.DropTable(
                name: "ProductListing");

            migrationBuilder.DropTable(
                name: "EmporiumUser");

            migrationBuilder.DropTable(
                name: "Showroom");

            migrationBuilder.DropTable(
                name: "Emporium");
        }
    }
}
