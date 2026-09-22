using System;
using ArtCommission.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace ArtCommission.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(AppDbContext))]
    partial class AppDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.8")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

            modelBuilder.Entity("ArtCommission.Domain.Entities.Ai.AiConversation", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid?>("ContextId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("ContextType")
                        .IsRequired()
                        .ValueGeneratedOnAdd()
                        .HasMaxLength(20)
                        .HasColumnType("nvarchar(20)")
                        .HasDefaultValue("General");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<bool>("IsArchived")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bit")
                        .HasDefaultValue(false);

                    b.Property<bool>("IsDeleted")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bit")
                        .HasDefaultValue(false);

                    b.Property<DateTimeOffset?>("LastMessageAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<int>("MessageCount")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .HasDefaultValue(0);

                    b.Property<string>("Topic")
                        .IsRequired()
                        .HasMaxLength(300)
                        .HasColumnType("nvarchar(300)");

                    b.Property<DateTimeOffset?>("UpdatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    b.HasIndex("ContextType", "ContextId")
                        .HasDatabaseName("IX_AiConversations_Context");

                    b.HasIndex("UserId", "LastMessageAt")
                        .HasDatabaseName("IX_AiConversations_User_LastMessageAt");

                    b.ToTable("AiConversations", (string)null);
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Ai.AiMessage", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Content")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<Guid>("ConversationId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<DateTimeOffset?>("FeedbackAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<bool?>("FeedbackHelpful")
                        .HasColumnType("bit");

                    b.Property<string>("FeedbackNote")
                        .HasMaxLength(1000)
                        .HasColumnType("nvarchar(1000)");

                    b.Property<bool>("IsDeleted")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bit")
                        .HasDefaultValue(false);

                    b.Property<string>("ModelVersion")
                        .HasMaxLength(80)
                        .HasColumnType("nvarchar(80)");

                    b.Property<string>("Role")
                        .IsRequired()
                        .ValueGeneratedOnAdd()
                        .HasMaxLength(20)
                        .HasColumnType("nvarchar(20)")
                        .HasDefaultValue("User");

                    b.Property<int?>("TokenCount")
                        .HasColumnType("int");

                    b.Property<DateTimeOffset?>("UpdatedAt")
                        .HasColumnType("datetimeoffset");

                    b.HasKey("Id");

                    b.HasIndex("FeedbackHelpful")
                        .HasDatabaseName("IX_AiMessages_FeedbackHelpful");

                    b.HasIndex("ConversationId", "CreatedAt")
                        .HasDatabaseName("IX_AiMessages_Conversation_CreatedAt");

                    b.ToTable("AiMessages", (string)null);
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Ai.DeadlineReminder", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Channel")
                        .IsRequired()
                        .ValueGeneratedOnAdd()
                        .HasMaxLength(20)
                        .HasColumnType("nvarchar(20)")
                        .HasDefaultValue("InApp");

                    b.Property<Guid>("CommissionId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("FailureReason")
                        .HasMaxLength(500)
                        .HasColumnType("nvarchar(500)");

                    b.Property<bool>("IsDeleted")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bit")
                        .HasDefaultValue(false);

                    b.Property<bool>("IsManual")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bit")
                        .HasDefaultValue(false);

                    b.Property<Guid?>("MilestoneId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("ModelVersion")
                        .HasMaxLength(80)
                        .HasColumnType("nvarchar(80)");

                    b.Property<DateTimeOffset?>("PredictedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<DateTimeOffset>("RemindAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<decimal>("RiskScore")
                        .HasPrecision(5, 2)
                        .HasColumnType("decimal(5,2)");

                    b.Property<DateTimeOffset?>("SentAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<DateTimeOffset?>("UpdatedAt")
                        .HasColumnType("datetimeoffset");

                    b.HasKey("Id");

                    b.HasIndex("CommissionId")
                        .HasDatabaseName("IX_DeadlineReminders_CommissionId");

                    b.HasIndex("MilestoneId");

                    b.HasIndex("SentAt", "RemindAt")
                        .HasDatabaseName("IX_DeadlineReminders_Pending_RemindAt");

                    b.ToTable("DeadlineReminders", (string)null);
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Ai.UserReminderSetting", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<bool>("EmailEnabled")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bit")
                        .HasDefaultValue(true);

                    b.Property<bool>("IsDeleted")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bit")
                        .HasDefaultValue(false);

                    b.Property<int>("LeadHours")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .HasDefaultValue(24);

                    b.Property<bool>("PushEnabled")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bit")
                        .HasDefaultValue(true);

                    b.Property<DateTimeOffset?>("UpdatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    b.HasIndex("UserId")
                        .IsUnique()
                        .HasDatabaseName("UX_UserReminderSettings_UserId");

                    b.ToTable("UserReminderSettings", (string)null);
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.ArtistStudio.Artwork", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<decimal?>("AdultScore")
                        .HasPrecision(5, 4)
                        .HasColumnType("decimal(5,4)");

                    b.Property<decimal?>("AiDetectionScore")
                        .HasColumnType("decimal(18,2)");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<Guid>("CreatorProfileId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Description")
                        .HasColumnType("nvarchar(max)");

                    b.Property<long?>("FileSizeBytes")
                        .HasColumnType("bigint");

                    b.Property<string>("FlagReason")
                        .HasMaxLength(200)
                        .HasColumnType("nvarchar(200)");

                    b.Property<string>("ImageUrl")
                        .IsRequired()
                        .HasMaxLength(500)
                        .HasColumnType("nvarchar(500)");

                    b.Property<bool>("IsAiGenerated")
                        .HasColumnType("bit");

                    b.Property<bool>("IsDeleted")
                        .HasColumnType("bit");

                    b.Property<int>("LikeCount")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .HasDefaultValue(0);

                    b.Property<DateTimeOffset?>("ModeratedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("ModerationNote")
                        .HasMaxLength(1000)
                        .HasColumnType("nvarchar(1000)");

                    b.Property<string>("ModerationStatus")
                        .IsRequired()
                        .ValueGeneratedOnAdd()
                        .HasMaxLength(20)
                        .HasColumnType("nvarchar(20)")
                        .HasDefaultValue("Pending");

                    b.Property<Guid?>("ModeratorId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Resolution")
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)");

                    b.Property<decimal?>("SafeScore")
                        .HasPrecision(5, 4)
                        .HasColumnType("decimal(5,4)");

                    b.Property<string>("Style")
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.Property<string>("ThumbnailUrl")
                        .HasMaxLength(500)
                        .HasColumnType("nvarchar(500)");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("nvarchar(200)");

                    b.Property<DateTimeOffset?>("UpdatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<int>("ViewCount")
                        .HasColumnType("int");

                    b.Property<decimal?>("ViolenceScore")
                        .HasPrecision(5, 4)
                        .HasColumnType("decimal(5,4)");

                    b.Property<string>("WatermarkedUrl")
                        .HasMaxLength(500)
                        .HasColumnType("nvarchar(500)");

                    b.HasKey("Id");

                    b.HasIndex("CreatorProfileId");

                    b.HasIndex("ModerationStatus");

                    b.ToTable("Artworks", (string)null);
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.ArtistStudio.ArtworkTag", b =>
                {
                    b.Property<Guid>("ArtworkId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("TagId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("ArtworkId", "TagId");

                    b.HasIndex("TagId");

                    b.ToTable("ArtworkTags", (string)null);
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.ArtistStudio.CreatorProfile", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("BannerUrl")
                        .HasMaxLength(500)
                        .HasColumnType("nvarchar(500)");

                    b.Property<string>("Bio")
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("CommissionSlots")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .HasDefaultValue(0);

                    b.Property<int>("CompletedOrdersCount")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .HasDefaultValue(0);

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("DisplayName")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.Property<int>("FollowerCount")
                        .HasColumnType("int");

                    b.Property<string>("Headline")
                        .HasMaxLength(200)
                        .HasColumnType("nvarchar(200)");

                    b.Property<bool>("IsAcceptingOrders")
                        .HasColumnType("bit");

                    b.Property<bool>("IsAiVerified")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bit")
                        .HasDefaultValue(false);

                    b.Property<bool>("IsApproved")
                        .HasColumnType("bit");

                    b.Property<bool>("IsDeleted")
                        .HasColumnType("bit");

                    b.Property<string>("Location")
                        .HasMaxLength(200)
                        .HasColumnType("nvarchar(200)");

                    b.Property<string>("RateCard")
                        .HasColumnType("nvarchar(max)");

                    b.Property<decimal>("RatingAverage")
                        .HasColumnType("decimal(18,2)");

                    b.Property<int>("RatingCount")
                        .HasColumnType("int");

                    b.Property<string>("Specialties")
                        .HasMaxLength(500)
                        .HasColumnType("nvarchar(500)");

                    b.Property<DateTimeOffset?>("UpdatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("WebsiteUrl")
                        .HasMaxLength(500)
                        .HasColumnType("nvarchar(500)");

                    b.HasKey("Id");

                    b.HasIndex("UserId")
                        .IsUnique();

                    b.ToTable("CreatorProfiles", (string)null);
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.ArtistStudio.Follow", b =>
                {
                    b.Property<Guid>("FollowerUserId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("CreatorProfileId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("datetimeoffset");

                    b.HasKey("FollowerUserId", "CreatorProfileId");

                    b.HasIndex("CreatorProfileId");

                    b.ToTable("Follows", (string)null);
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.ArtistStudio.Tag", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<bool>("IsAiGenerated")
                        .HasColumnType("bit");

                    b.Property<bool>("IsDeleted")
                        .HasColumnType("bit");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)");

                    b.Property<DateTimeOffset?>("UpdatedAt")
                        .HasColumnType("datetimeoffset");

                    b.HasKey("Id");

                    b.HasIndex("Name")
                        .IsUnique();

                    b.ToTable("Tags", (string)null);
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Auction.ArtworkOwnership", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTimeOffset>("AcquiredAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<Guid>("ArtworkId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid?>("AuctionId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid?>("CommissionId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<bool>("IsCurrent")
                        .HasColumnType("bit");

                    b.Property<bool>("IsDeleted")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bit")
                        .HasDefaultValue(false);

                    b.Property<Guid>("OwnerId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTimeOffset?>("ReleasedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("TransferReason")
                        .IsRequired()
                        .ValueGeneratedOnAdd()
                        .HasMaxLength(30)
                        .HasColumnType("nvarchar(30)")
                        .HasDefaultValue("AuctionWin");

                    b.Property<DateTimeOffset?>("UpdatedAt")
                        .HasColumnType("datetimeoffset");

                    b.HasKey("Id");

                    b.HasIndex("ArtworkId")
                        .IsUnique()
                        .HasDatabaseName("UX_ArtworkOwnerships_CurrentOwner")
                        .HasFilter("[IsCurrent] = 1");

                    b.HasIndex("AuctionId");

                    b.HasIndex("OwnerId", "IsCurrent")
                        .HasDatabaseName("IX_ArtworkOwnerships_Owner_Current");

                    b.ToTable("ArtworkOwnerships", (string)null);
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Auction.Auction", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("ArtworkId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("AuctionType")
                        .IsRequired()
                        .ValueGeneratedOnAdd()
                        .HasMaxLength(20)
                        .HasColumnType("nvarchar(20)")
                        .HasDefaultValue("Standard");

                    b.Property<int>("BidCount")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .HasDefaultValue(0);

                    b.Property<decimal>("BidStep")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<decimal?>("BuyNowPrice")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<string>("CancelReason")
                        .HasMaxLength(500)
                        .HasColumnType("nvarchar(500)");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<decimal>("CurrentPrice")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<DateTimeOffset>("EndAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<decimal?>("FinalPrice")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<bool>("IsDeleted")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bit")
                        .HasDefaultValue(false);

                    b.Property<DateTimeOffset?>("PaymentDeadline")
                        .HasColumnType("datetimeoffset");

                    b.Property<decimal?>("ReservePrice")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<Guid>("SellerId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTimeOffset?>("SettledAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<DateTimeOffset>("StartAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<decimal>("StartPrice")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<string>("Status")
                        .IsRequired()
                        .ValueGeneratedOnAdd()
                        .HasMaxLength(20)
                        .HasColumnType("nvarchar(20)")
                        .HasDefaultValue("Scheduled");

                    b.Property<DateTimeOffset?>("UpdatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<Guid?>("WinnerId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    b.HasIndex("ArtworkId")
                        .HasDatabaseName("IX_Auctions_ArtworkId");

                    b.HasIndex("WinnerId");

                    b.HasIndex("SellerId", "Status")
                        .HasDatabaseName("IX_Auctions_Seller_Status");

                    b.HasIndex("Status", "CurrentPrice")
                        .HasDatabaseName("IX_Auctions_Status_CurrentPrice");

                    b.HasIndex("Status", "EndAt")
                        .HasDatabaseName("IX_Auctions_Status_EndAt");

                    b.ToTable("Auctions", (string)null);
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Auction.AuctionWatch", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("AuctionId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<bool>("IsDeleted")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bit")
                        .HasDefaultValue(false);

                    b.Property<bool>("NotifyEndingSoon")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bit")
                        .HasDefaultValue(true);

                    b.Property<bool>("NotifyOnOutbid")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bit")
                        .HasDefaultValue(true);

                    b.Property<DateTimeOffset?>("UpdatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    b.HasIndex("UserId");

                    b.HasIndex("AuctionId", "UserId")
                        .IsUnique()
                        .HasDatabaseName("UX_AuctionWatches_Auction_User");

                    b.ToTable("AuctionWatches", (string)null);
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Auction.Bid", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<decimal>("Amount")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<Guid>("AuctionId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("BidderId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<decimal>("HoldAmount")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<string>("HoldStatus")
                        .IsRequired()
                        .ValueGeneratedOnAdd()
                        .HasMaxLength(20)
                        .HasColumnType("nvarchar(20)")
                        .HasDefaultValue("None");

                    b.Property<bool>("IsAuto")
                        .HasColumnType("bit");

                    b.Property<bool>("IsDeleted")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bit")
                        .HasDefaultValue(false);

                    b.Property<decimal?>("MaxAutoBid")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<DateTimeOffset>("PlacedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("Status")
                        .IsRequired()
                        .ValueGeneratedOnAdd()
                        .HasMaxLength(20)
                        .HasColumnType("nvarchar(20)")
                        .HasDefaultValue("Leading");

                    b.Property<DateTimeOffset?>("UpdatedAt")
                        .HasColumnType("datetimeoffset");

                    b.HasKey("Id");

                    b.HasIndex("AuctionId")
                        .IsUnique()
                        .HasDatabaseName("UX_Bids_OneLeadingPerAuction")
                        .HasFilter("[Status] = 'Leading'");

                    b.HasIndex("BidderId")
                        .HasDatabaseName("IX_Bids_BidderId");

                    b.HasIndex("AuctionId", "PlacedAt")
                        .HasDatabaseName("IX_Bids_Auction_PlacedAt");

                    b.ToTable("Bids", (string)null);
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Auction.Deliverable", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("AuctionId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<DateTimeOffset?>("DeliveredAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<int>("DownloadCount")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .HasDefaultValue(0);

                    b.Property<string>("FileName")
                        .IsRequired()
                        .HasMaxLength(300)
                        .HasColumnType("nvarchar(300)");

                    b.Property<long?>("FileSizeBytes")
                        .HasColumnType("bigint");

                    b.Property<string>("FileUrl")
                        .IsRequired()
                        .HasMaxLength(1000)
                        .HasColumnType("nvarchar(1000)");

                    b.Property<bool>("IsDeleted")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bit")
                        .HasDefaultValue(false);

                    b.Property<bool>("IsDelivered")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bit")
                        .HasDefaultValue(false);

                    b.Property<string>("MimeType")
                        .HasMaxLength(120)
                        .HasColumnType("nvarchar(120)");

                    b.Property<Guid>("RecipientId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTimeOffset?>("UpdatedAt")
                        .HasColumnType("datetimeoffset");

                    b.HasKey("Id");

                    b.HasIndex("AuctionId")
                        .IsUnique()
                        .HasDatabaseName("UX_Deliverables_Auction");

                    b.HasIndex("RecipientId");

                    b.ToTable("Deliverables", (string)null);
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Auction.EscrowTransaction", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<decimal>("Amount")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<Guid>("AuctionId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<decimal>("FeeAmount")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<bool>("IsDeleted")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bit")
                        .HasDefaultValue(false);

                    b.Property<string>("Note")
                        .HasMaxLength(500)
                        .HasColumnType("nvarchar(500)");

                    b.Property<Guid>("PayeeId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("PayerId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<decimal>("RefundedAmount")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<DateTimeOffset?>("RefundedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<decimal>("ReleasedAmount")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<DateTimeOffset?>("ReleasedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("Status")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("nvarchar(20)");

                    b.Property<DateTimeOffset?>("UpdatedAt")
                        .HasColumnType("datetimeoffset");

                    b.HasKey("Id");

                    b.HasIndex("AuctionId")
                        .IsUnique()
                        .HasDatabaseName("UX_EscrowTransactions_Auction");

                    b.HasIndex("PayeeId");

                    b.Property<int>("LikeCount")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .HasDefaultValue(0);

                    b.Property<DateTimeOffset?>("ModeratedAt")
                        .HasColumnType("datetimeoffset");
                    b.HasIndex("PayerId");

                    b.ToTable("EscrowTransactions", (string)null);
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Chat.ChatRoom", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid?>("AuctionId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid?>("CommissionId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("Style")
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.Property<string>("ThumbnailUrl")
                        .HasMaxLength(500)
                        .HasColumnType("nvarchar(500)");
                    b.Property<bool>("IsDeleted")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bit")
                        .HasDefaultValue(false);

                    b.Property<bool>("IsLocked")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bit")
                        .HasDefaultValue(false);

                    b.Property<DateTimeOffset?>("LastMessageAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("RoomType")
                        .IsRequired()
                        .ValueGeneratedOnAdd()
                        .HasMaxLength(20)
                        .HasColumnType("nvarchar(20)")
                        .HasDefaultValue("Commission");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("nvarchar(200)");

                    b.Property<DateTimeOffset?>("UpdatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<int>("ViewCount")
                        .HasColumnType("int");

                    b.Property<decimal?>("ViolenceScore")
                        .HasPrecision(5, 4)
                        .HasColumnType("decimal(5,4)");

                    b.Property<string>("WatermarkedUrl")
                        .HasMaxLength(500)
                        .HasColumnType("nvarchar(500)");

                    b.HasKey("Id");

                    b.HasIndex("AuctionId")
                        .HasDatabaseName("IX_ChatRooms_AuctionId");

                    b.HasIndex("CommissionId")
                        .HasDatabaseName("IX_ChatRooms_CommissionId");

                    b.HasIndex("LastMessageAt")
                        .HasDatabaseName("IX_ChatRooms_LastMessageAt");

                    b.ToTable("ChatRooms", (string)null);
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Chat.ChatRoomMember", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<bool>("HasLeft")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bit")
                        .HasDefaultValue(false);

                    b.Property<bool>("IsDeleted")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bit")
                        .HasDefaultValue(false);

                    b.Property<DateTimeOffset?>("LastReadAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("MemberRole")
                        .IsRequired()
                        .HasMaxLength(30)
                        .HasColumnType("nvarchar(30)");

                    b.Property<Guid>("RoomId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTimeOffset?>("UpdatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    b.HasIndex("UserId")
                        .HasDatabaseName("IX_ChatRoomMembers_UserId");

                    b.HasIndex("RoomId", "UserId")
                        .IsUnique()
                        .HasDatabaseName("UX_ChatRoomMembers_Room_User");

                    b.ToTable("ChatRoomMembers", (string)null);
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Chat.Message", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("AttachmentUrl")
                        .HasMaxLength(500)
                        .HasColumnType("nvarchar(500)");

                    b.Property<string>("Body")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("CommissionSlots")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .HasDefaultValue(0);

                    b.Property<int>("CompletedOrdersCount")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .HasDefaultValue(0);
                    b.Property<Guid?>("CommissionId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<bool>("IsDeleted")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bit")
                        .HasDefaultValue(false);

                    b.Property<bool>("IsRead")
                        .HasColumnType("bit");

                    b.Property<string>("MessageType")
                        .IsRequired()
                        .ValueGeneratedOnAdd()
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)")
                        .HasDefaultValue("Text");

                    b.Property<Guid?>("RoomId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("RateCard")
                        .HasColumnType("nvarchar(max)");

                    b.Property<decimal>("RatingAverage")
                        .HasColumnType("decimal(18,2)");
                    b.Property<Guid>("SenderId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTimeOffset>("SentAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("SourceLang")
                        .HasMaxLength(10)
                        .HasColumnType("nvarchar(10)");

                    b.Property<string>("TargetLang")
                        .HasMaxLength(10)
                        .HasColumnType("nvarchar(10)");

                    b.Property<string>("TranslatedBody")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("TranslationError")
                        .HasMaxLength(500)
                        .HasColumnType("nvarchar(500)");

                    b.Property<string>("TranslationStatus")
                        .IsRequired()
                        .ValueGeneratedOnAdd()
                        .HasMaxLength(20)
                        .HasColumnType("nvarchar(20)")
                        .HasDefaultValue("NotRequested");

                    b.Property<DateTimeOffset?>("UpdatedAt")
                        .HasColumnType("datetimeoffset");

                    b.HasKey("Id");

                    b.HasIndex("SenderId");

                    b.HasIndex("CommissionId", "SentAt")
                        .HasDatabaseName("IX_Messages_Commission_SentAt");

                    b.HasIndex("RoomId", "SentAt")
                        .HasDatabaseName("IX_Messages_Room_SentAt");

                    b.ToTable("Messages", (string)null);
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Chat.MessageAttachment", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("FileName")
                        .HasMaxLength(300)
                        .HasColumnType("nvarchar(300)");

                    b.Property<long>("FileSize")
                        .HasColumnType("bigint");

                    b.Property<string>("FileUrl")
                        .IsRequired()
                        .HasMaxLength(1000)
                        .HasColumnType("nvarchar(1000)");

                    b.Property<bool>("IsDeleted")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bit")
                        .HasDefaultValue(false);

                    b.Property<Guid>("MessageId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("MimeType")
                        .IsRequired()
                        .HasMaxLength(120)
                        .HasColumnType("nvarchar(120)");

                    b.Property<DateTimeOffset?>("UpdatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<Guid>("UploadedBy")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    b.HasIndex("MessageId")
                        .HasDatabaseName("IX_MessageAttachments_MessageId");

                    b.HasIndex("UploadedBy");

                    b.ToTable("MessageAttachments", (string)null);
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Chat.Message", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("AttachmentUrl")
                        .HasMaxLength(500)
                        .HasColumnType("nvarchar(500)");

                    b.Property<string>("Body")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<Guid>("CommissionId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<bool>("IsDeleted")
                        .HasColumnType("bit");

                    b.Property<bool>("IsRead")
                        .HasColumnType("bit");

                    b.Property<string>("MessageType")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)");

                    b.Property<Guid>("SenderId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTimeOffset>("SentAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<DateTimeOffset?>("UpdatedAt")
                        .HasColumnType("datetimeoffset");

                    b.HasKey("Id");

                    b.HasIndex("SenderId");

                    b.HasIndex("CommissionId", "SentAt")
                        .HasDatabaseName("IX_Messages_Commission_SentAt");

                    b.ToTable("Messages", (string)null);
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Commission.Commission", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("ClientId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<Guid>("CreatorId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<int>("CurrentStage")
                        .IsConcurrencyToken()
                        .HasColumnType("int");

                    b.Property<DateTimeOffset?>("DeadlineAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("Description")
                        .HasColumnType("nvarchar(max)");

                    b.Property<decimal>("DisbursedAmount")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<decimal>("DiscountAmount")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<decimal>("EscrowHeldAmount")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<string>("EscrowStatus")
                        .IsConcurrencyToken()
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)");

                    b.Property<decimal>("FinalPrice")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<bool>("IsDeleted")
                        .HasColumnType("bit");

                    b.Property<string>("Status")
                        .IsConcurrencyToken()
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("nvarchar(200)");

                    b.Property<decimal>("TotalPrice")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<DateTimeOffset?>("UpdatedAt")
                        .IsConcurrencyToken()
                        .HasColumnType("datetimeoffset");

                    b.Property<Guid?>("VoucherId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    b.ToTable("Commissions", (string)null);
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Commission.Dispute", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("AdminNote")
                        .HasColumnType("nvarchar(max)");

                    b.Property<decimal?>("ArtistPayAmount")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<decimal?>("ClientRefundAmount")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<Guid>("CommissionId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("EvidenceUrls")
                        .HasColumnType("nvarchar(max)");

                    b.Property<bool>("IsDeleted")
                        .HasColumnType("bit");

                    b.Property<Guid>("RaisedById")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Reason")
                        .IsRequired()
                        .HasMaxLength(2000)
                        .HasColumnType("nvarchar(2000)");

                    b.Property<string>("Resolution")
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)");

                    b.Property<DateTimeOffset?>("ResolvedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("Status")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)");

                    b.Property<DateTimeOffset?>("UpdatedAt")
                        .HasColumnType("datetimeoffset");

                    b.HasKey("Id");

                    b.HasIndex("CommissionId")
                        .IsUnique();

                    b.ToTable("Disputes", (string)null);
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Commission.Milestone", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTimeOffset?>("ApprovedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<Guid>("CommissionId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("CreatorNote")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("FinalDeliverableUrl")
                        .HasMaxLength(500)
                        .HasColumnType("nvarchar(500)");

                    b.Property<bool>("IsDeleted")
                        .HasColumnType("bit");

                    b.Property<int>("MaxRevisions")
                        .HasColumnType("int");

                    b.Property<string>("OriginalWipUrl")
                        .HasColumnType("nvarchar(max)");

                    b.Property<decimal>("Price")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<int>("RevisionCount")
                        .HasColumnType("int");

                    b.Property<string>("RevisionFeedback")
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("Sequence")
                        .HasColumnType("int");

                    b.Property<string>("Status")
                        .IsConcurrencyToken()
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)");

                    b.Property<DateTimeOffset?>("SubmittedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("nvarchar(200)");

                    b.Property<DateTimeOffset?>("UpdatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("WatermarkedUrl")
                        .HasMaxLength(500)
                        .HasColumnType("nvarchar(500)");

                    b.Property<string>("WipPreviewUrl")
                        .HasMaxLength(500)
                        .HasColumnType("nvarchar(500)");

                    b.HasKey("Id");

                    b.HasIndex("CommissionId");

                    b.ToTable("Milestones", (string)null);
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Commission.Review", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("AttachedImagesJson")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Comment")
                        .HasMaxLength(1000)
                        .HasColumnType("nvarchar(1000)");

                    b.Property<Guid>("CommissionId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<bool>("IsDeleted")
                        .HasColumnType("bit");

                    b.Property<bool>("IsVisible")
                        .HasColumnType("bit");

                    b.Property<int>("Rating")
                        .HasColumnType("int");

                    b.Property<DateTimeOffset?>("RespondedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<Guid>("ReviewerId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("ReviewerReply")
                        .HasMaxLength(1000)
                        .HasColumnType("nvarchar(1000)");

                    b.Property<DateTimeOffset?>("UpdatedAt")
                        .HasColumnType("datetimeoffset");

                    b.HasKey("Id");

                    b.HasIndex("CommissionId")
                        .IsUnique();

                    b.ToTable("Reviews", (string)null);
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.CreatorApplication.CreatorApplication", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("ApplicantId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("IdProofUrl")
                        .IsRequired()
                        .HasMaxLength(500)
                        .HasColumnType("nvarchar(500)");

                    b.Property<bool>("IsDeleted")
                        .HasColumnType("bit");

                    b.Property<bool>("IsNationalIdVerified")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bit")
                        .HasDefaultValue(false);

                    b.Property<string>("PortfolioLinks")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("PrimaryStyle")
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.Property<string>("ReviewNote")
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTimeOffset?>("ReviewedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<Guid?>("ReviewedByModId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("SocialLinks")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("SpeedpaintVideoUrl")
                        .HasMaxLength(500)
                        .HasColumnType("nvarchar(500)");

                    b.Property<string>("Status")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)");

                    b.Property<DateTimeOffset>("SubmittedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<DateTimeOffset?>("UpdatedAt")
                        .HasColumnType("datetimeoffset");

                    b.HasKey("Id");

                    b.HasIndex("ReviewedByModId");

                    b.HasIndex("ApplicantId", "Status");

                    b.ToTable("CreatorApplications", (string)null);
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Identity.ApplicationUser", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<int>("AccessFailedCount")
                        .HasColumnType("int");

                    b.Property<string>("ConcurrencyStamp")
                        .IsConcurrencyToken()
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("Email")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)");

                    b.Property<bool>("EmailConfirmed")
                        .HasColumnType("bit");

                    b.Property<string>("FullName")
                        .IsRequired()
                        .HasMaxLength(150)
                        .HasColumnType("nvarchar(150)");

                    b.Property<bool>("IsDeleted")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bit")
                        .HasDefaultValue(false);

                    b.Property<bool>("IsVerified")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bit")
                        .HasDefaultValue(false);

                    b.Property<bool>("LockoutEnabled")
                        .HasColumnType("bit");

                    b.Property<DateTimeOffset?>("LockoutEnd")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("NormalizedEmail")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)");

                    b.Property<string>("NormalizedUserName")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)");

                    b.Property<string>("PasswordHash")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("PhoneNumber")
                        .HasColumnType("nvarchar(max)");

                    b.Property<bool>("PhoneNumberConfirmed")
                        .HasColumnType("bit");

                    b.Property<string>("SecurityStamp")
                        .HasColumnType("nvarchar(max)");

                    b.Property<bool>("TwoFactorEnabled")
                        .HasColumnType("bit");

                    b.Property<DateTimeOffset?>("UpdatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("UserName")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)");

                    b.HasKey("Id");

                    b.HasIndex("NormalizedEmail")
                        .HasDatabaseName("EmailIndex");

                    b.HasIndex("NormalizedUserName")
                        .IsUnique()
                        .HasDatabaseName("UserNameIndex")
                        .HasFilter("[NormalizedUserName] IS NOT NULL");

                    b.ToTable("Users", (string)null);
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Identity.RefreshToken", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("CreatedByIp")
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)");

                    b.Property<DateTimeOffset>("ExpiresAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<bool>("IsDeleted")
                        .HasColumnType("bit");

                    b.Property<string>("ReplacedByTokenHash")
                        .HasMaxLength(450)
                        .HasColumnType("nvarchar(450)");

                    b.Property<DateTimeOffset?>("RevokedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("TokenHash")
                        .IsRequired()
                        .HasMaxLength(450)
                        .HasColumnType("nvarchar(450)");

                    b.Property<DateTimeOffset?>("UpdatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    b.HasIndex("TokenHash")
                        .IsUnique()
                        .HasDatabaseName("UX_RefreshTokens_TokenHash");

                    b.HasIndex("UserId")
                        .HasDatabaseName("IX_RefreshTokens_UserId");

                    b.ToTable("RefreshTokens", (string)null);
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Identity.UserSanction", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("ActionByAdminId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("ActionType")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<int?>("DurationDays")
                        .HasColumnType("int");

                    b.Property<DateTimeOffset?>("ExpiresAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<bool>("IsDeleted")
                        .HasColumnType("bit");

                    b.Property<string>("Reason")
                        .IsRequired()
                        .HasMaxLength(1000)
                        .HasColumnType("nvarchar(1000)");

                    b.Property<DateTimeOffset?>("UpdatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    b.HasIndex("ActionByAdminId");

                    b.HasIndex("UserId", "CreatedAt");

                    b.ToTable("UserSanctions", (string)null);
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Notifications.Notification", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Body")
                        .IsRequired()
                        .HasMaxLength(1000)
                        .HasColumnType("nvarchar(1000)");

                    b.Property<string>("Channel")
                        .IsRequired()
                        .ValueGeneratedOnAdd()
                        .HasMaxLength(20)
                        .HasColumnType("nvarchar(20)")
                        .HasDefaultValue("InApp");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("DedupKey")
                        .HasMaxLength(200)
                        .HasColumnType("nvarchar(200)");

                    b.Property<string>("FailedReason")
                        .HasMaxLength(500)
                        .HasColumnType("nvarchar(500)");

                    b.Property<bool>("IsDeleted")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bit")
                        .HasDefaultValue(false);

                    b.Property<bool>("IsRead")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bit")
                        .HasDefaultValue(false);

                    b.Property<string>("NotificationTitle")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("nvarchar(200)");

                    b.Property<string>("NotificationType")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)");

                    b.Property<DateTimeOffset?>("ReadAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<Guid?>("RefId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("RefType")
                        .HasMaxLength(30)
                        .HasColumnType("nvarchar(30)");

                    b.Property<DateTimeOffset?>("SentAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<DateTimeOffset?>("UpdatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    b.HasIndex("UserId", "CreatedAt")
                        .HasDatabaseName("IX_Notification_UserId_CreatedAt");

                    b.HasIndex("UserId", "DedupKey")
                        .IsUnique()
                        .HasDatabaseName("UX_Notification_UserId_DedupKey")
                        .HasFilter("[DedupKey] IS NOT NULL");

                    b.HasIndex("UserId", "IsRead")
                        .HasDatabaseName("IX_Notification_UserId_IsRead");

                    b.ToTable("Notification", (string)null);
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Payment.BankAccount", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("AccountHolder")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("nvarchar(200)");

                    b.Property<string>("AccountNumber")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)");

                    b.Property<string>("BankBin")
                        .HasMaxLength(20)
                        .HasColumnType("nvarchar(20)");

                    b.Property<string>("BankCode")
                        .HasMaxLength(20)
                        .HasColumnType("nvarchar(20)");

                    b.Property<string>("BankName")
                        .IsRequired()
                        .HasMaxLength(200)
                        .HasColumnType("nvarchar(200)");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<bool>("IsDefault")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bit")
                        .HasDefaultValue(false);

                    b.Property<bool>("IsDeleted")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bit")
                        .HasDefaultValue(false);

                    b.Property<bool>("IsVerified")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bit")
                        .HasDefaultValue(false);

                    b.Property<DateTimeOffset?>("UpdatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    b.HasIndex("UserId")
                        .IsUnique()
                        .HasDatabaseName("UX_BankAccount_DefaultPerUser")
                        .HasFilter("[IsDefault] = 1 AND [IsDeleted] = 0");

                    b.HasIndex("UserId", "IsDeleted")
                        .HasDatabaseName("IX_BankAccount_UserId_IsDeleted");

                    b.ToTable("BankAccount", (string)null);
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Payment.PaymentOrder", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<decimal>("Amount")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<string>("CheckoutUrl")
                        .HasMaxLength(500)
                        .HasColumnType("nvarchar(500)");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<DateTimeOffset?>("ExpiredAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("FailureReason")
                        .HasMaxLength(500)
                        .HasColumnType("nvarchar(500)");

                    b.Property<string>("Gateway")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("nvarchar(20)");

                    b.Property<bool>("IsDeleted")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bit")
                        .HasDefaultValue(false);

                    b.Property<string>("OrderRef")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)");

                    b.Property<DateTimeOffset?>("PaidAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<long>("PayOsOrderCode")
                        .HasColumnType("bigint");

                    b.Property<string>("PaymentLinkId")
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.Property<string>("QrCode")
                        .HasMaxLength(1000)
                        .HasColumnType("nvarchar(1000)");

                    b.Property<string>("Status")
                        .IsRequired()
                        .ValueGeneratedOnAdd()
                        .HasMaxLength(20)
                        .HasColumnType("nvarchar(20)")
                        .HasDefaultValue("Pending");

                    b.Property<string>("TransactionRef")
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.Property<DateTimeOffset?>("UpdatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("WalletId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    b.HasIndex("OrderRef")
                        .IsUnique()
                        .HasDatabaseName("UX_PaymentOrder_OrderRef");

                    b.HasIndex("Status")
                        .HasDatabaseName("IX_PaymentOrder_Status");

                    b.HasIndex("WalletId");

                    b.HasIndex("Gateway", "PayOsOrderCode")
                        .IsUnique()
                        .HasDatabaseName("UX_PaymentOrder_Gateway_PayOsOrderCode");

                    b.HasIndex("Gateway", "TransactionRef")
                        .IsUnique()
                        .HasDatabaseName("UX_PaymentOrder_Gateway_TransactionRef")
                        .HasFilter("[TransactionRef] IS NOT NULL");

                    b.HasIndex("UserId", "CreatedAt")
                        .IsDescending(false, true)
                        .HasDatabaseName("IX_PaymentOrder_UserId_CreatedAt");

                    b.ToTable("PaymentOrder", (string)null);
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Payment.PayoutRequest", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<decimal>("Amount")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<Guid>("BankAccountId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("BankAccountSnapshot")
                        .HasMaxLength(1000)
                        .HasColumnType("nvarchar(1000)");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<bool>("IsDeleted")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bit")
                        .HasDefaultValue(false);

                    b.Property<string>("PayoutNote")
                        .HasMaxLength(500)
                        .HasColumnType("nvarchar(500)");

                    b.Property<DateTimeOffset?>("ProcessedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<Guid?>("ProcessedBy")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("RejectReason")
                        .HasMaxLength(500)
                        .HasColumnType("nvarchar(500)");

                    b.Property<string>("Status")
                        .IsRequired()
                        .ValueGeneratedOnAdd()
                        .HasMaxLength(20)
                        .HasColumnType("nvarchar(20)")
                        .HasDefaultValue("Pending");

                    b.Property<string>("TransactionRef")
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.Property<DateTimeOffset?>("UpdatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("WalletId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    b.HasIndex("BankAccountId")
                        .HasDatabaseName("IX_PayoutRequest_BankAccountId");

                    b.HasIndex("Status")
                        .HasDatabaseName("IX_PayoutRequest_Status");

                    b.HasIndex("WalletId");

                    b.HasIndex("UserId", "CreatedAt")
                        .IsDescending(false, true)
                        .HasDatabaseName("IX_PayoutRequest_UserId_CreatedAt");

                    b.ToTable("PayoutRequest", (string)null);
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Payment.PlatformConfig", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("Description")
                        .HasMaxLength(500)
                        .HasColumnType("nvarchar(500)");

                    b.Property<bool>("IsDeleted")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bit")
                        .HasDefaultValue(false);

                    b.Property<string>("Key")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("nvarchar(100)");

                    b.Property<DateTimeOffset?>("UpdatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<Guid?>("UpdatedByAdminId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Value")
                        .IsRequired()
                        .HasMaxLength(500)
                        .HasColumnType("nvarchar(500)");

                    b.HasKey("Id");

                    b.HasIndex("Key")
                        .IsUnique()
                        .HasDatabaseName("UX_PlatformConfig_Key");

                    b.ToTable("PlatformConfig", (string)null);
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Payment.Voucher", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<Guid?>("CreatedByUserId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("DiscountType")
                        .IsRequired()
                        .HasMaxLength(20)
                        .HasColumnType("nvarchar(20)");

                    b.Property<decimal>("DiscountValue")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<DateOnly>("EndDate")
                        .HasColumnType("date");

                    b.Property<bool>("IsActive")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bit")
                        .HasDefaultValue(true);

                    b.Property<bool>("IsDeleted")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bit")
                        .HasDefaultValue(false);

                    b.Property<decimal?>("MaxDiscountAmount")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<decimal>("MinOrderAmount")
                        .ValueGeneratedOnAdd()
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)")
                        .HasDefaultValue(0m);

                    b.Property<string>("Name")
                        .HasMaxLength(200)
                        .HasColumnType("nvarchar(200)");

                    b.Property<int>("Scope")
                        .HasColumnType("int");

                    b.Property<DateOnly>("StartDate")
                        .HasColumnType("date");

                    b.Property<DateTimeOffset?>("UpdatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<int?>("UsageLimit")
                        .HasColumnType("int");

                    b.Property<int>("UsedCount")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .HasDefaultValue(0);

                    b.Property<string>("VoucherCode")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("nvarchar(50)");

                    b.HasKey("Id");

                    b.HasIndex("CreatedByUserId")
                        .HasDatabaseName("IX_Voucher_CreatedByUserId");

                    b.HasIndex("IsDeleted")
                        .HasDatabaseName("IX_Voucher_IsDeleted");

                    b.HasIndex("VoucherCode")
                        .IsUnique()
                        .HasDatabaseName("UX_Voucher_VoucherCode");

                    b.HasIndex("IsActive", "StartDate", "EndDate")
                        .HasDatabaseName("IX_Voucher_Active_Window");

                    b.ToTable("Voucher", (string)null);
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Payment.VoucherRedemption", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<decimal>("DiscountAmount")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<decimal>("FinalAmount")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<decimal>("OrderAmount")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<DateTimeOffset>("RedeemedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<Guid>("RefId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("RefType")
                        .IsRequired()
                        .HasMaxLength(30)
                        .HasColumnType("nvarchar(30)");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("VoucherId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    b.HasIndex("UserId")
                        .HasDatabaseName("IX_VoucherRedemption_UserId");

                    b.HasIndex("RefType", "RefId")
                        .IsUnique()
                        .HasDatabaseName("UX_VoucherRedemption_Ref");

                    b.HasIndex("VoucherId", "UserId")
                        .HasDatabaseName("IX_VoucherRedemption_Voucher_User");

                    b.ToTable("VoucherRedemption", (string)null);
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Payment.Wallet", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<decimal>("Balance")
                        .ValueGeneratedOnAdd()
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)")
                        .HasDefaultValue(0m);

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("Currency")
                        .IsRequired()
                        .ValueGeneratedOnAdd()
                        .HasMaxLength(3)
                        .HasColumnType("nvarchar(3)")
                        .HasDefaultValue("VND");

                    b.Property<bool>("IsDeleted")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bit")
                        .HasDefaultValue(false);

                    b.Property<decimal>("LockedBalance")
                        .ValueGeneratedOnAdd()
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)")
                        .HasDefaultValue(0m);

                    b.Property<byte[]>("RowVersion")
                        .IsConcurrencyToken()
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("rowversion");

                    b.Property<string>("Status")
                        .IsRequired()
                        .ValueGeneratedOnAdd()
                        .HasMaxLength(20)
                        .HasColumnType("nvarchar(20)")
                        .HasDefaultValue("Active");

                    b.Property<DateTimeOffset?>("UpdatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    b.HasIndex("UserId")
                        .IsUnique()
                        .HasDatabaseName("UX_Wallet_UserId");

                    b.ToTable("Wallet", (string)null);
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Payment.WalletTransaction", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<decimal>("Amount")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<decimal>("BalanceAfter")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("Direction")
                        .IsRequired()
                        .HasMaxLength(3)
                        .HasColumnType("nvarchar(3)");

                    b.Property<bool>("IsDeleted")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bit")
                        .HasDefaultValue(false);

                    b.Property<decimal>("LockedBalanceAfter")
                        .ValueGeneratedOnAdd()
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)")
                        .HasDefaultValue(0m);

                    b.Property<string>("Note")
                        .HasMaxLength(500)
                        .HasColumnType("nvarchar(500)");

                    b.Property<Guid?>("RefId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("RefType")
                        .HasMaxLength(30)
                        .HasColumnType("nvarchar(30)");

                    b.Property<string>("Type")
                        .IsRequired()
                        .HasMaxLength(30)
                        .HasColumnType("nvarchar(30)");

                    b.Property<DateTimeOffset?>("UpdatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<Guid>("WalletId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    b.HasIndex("WalletId", "CreatedAt")
                        .IsDescending(false, true)
                        .HasDatabaseName("IX_WalletTransaction_WalletId_CreatedAt");

                    b.HasIndex("RefType", "RefId", "Type")
                        .IsUnique()
                        .HasDatabaseName("UX_WalletTransaction_Ref")
                        .HasFilter("[RefId] IS NOT NULL");

                    b.ToTable("WalletTransaction", (string)null);
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Revenue.RevenueSnapshot", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<decimal?>("AuctionGrossAmount")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<decimal?>("CommissionGrossAmount")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<int>("CompletedOrderCount")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .HasDefaultValue(0);

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<Guid>("CreatorId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<decimal>("FeeAmount")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<decimal>("GrossAmount")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<bool>("IsDeleted")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bit")
                        .HasDefaultValue(false);

                    b.Property<decimal>("NetAmount")
                        .HasPrecision(18, 2)
                        .HasColumnType("decimal(18,2)");

                    b.Property<int>("RebuildCount")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .HasDefaultValue(0);

                    b.Property<DateTimeOffset?>("RebuiltAt")
                        .HasColumnType("datetimeoffset");

                    b.Property<string>("Scope")
                        .IsRequired()
                        .ValueGeneratedOnAdd()
                        .HasMaxLength(20)
                        .HasColumnType("nvarchar(20)")
                        .HasDefaultValue("Daily");

                    b.Property<DateOnly>("SnapshotDate")
                        .HasColumnType("date");

                    b.Property<DateTimeOffset?>("UpdatedAt")
                        .HasColumnType("datetimeoffset");

                    b.HasKey("Id");

                    b.HasIndex("Scope", "SnapshotDate")
                        .HasDatabaseName("IX_RevenueSnapshots_Scope_Date");

                    b.HasIndex("CreatorId", "Scope", "SnapshotDate")
                        .IsUnique()
                        .HasDatabaseName("UX_RevenueSnapshots_Creator_Scope_Date");

                    b.ToTable("RevenueSnapshots", (string)null);
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityRole<System.Guid>", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("ConcurrencyStamp")
                        .IsConcurrencyToken()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Name")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)");

                    b.Property<string>("NormalizedName")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)");

                    b.HasKey("Id");

                    b.HasIndex("NormalizedName")
                        .IsUnique()
                        .HasDatabaseName("RoleNameIndex")
                        .HasFilter("[NormalizedName] IS NOT NULL");

                    b.ToTable("Roles", (string)null);
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityRoleClaim<System.Guid>", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));

                    b.Property<string>("ClaimType")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("ClaimValue")
                        .HasColumnType("nvarchar(max)");

                    b.Property<Guid>("RoleId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    b.HasIndex("RoleId");

                    b.ToTable("RoleClaims", (string)null);
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserClaim<System.Guid>", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));

                    b.Property<string>("ClaimType")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("ClaimValue")
                        .HasColumnType("nvarchar(max)");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    b.HasIndex("UserId");

                    b.ToTable("UserClaims", (string)null);
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserLogin<System.Guid>", b =>
                {
                    b.Property<string>("LoginProvider")
                        .HasColumnType("nvarchar(450)");

                    b.Property<string>("ProviderKey")
                        .HasColumnType("nvarchar(450)");

                    b.Property<string>("ProviderDisplayName")
                        .HasColumnType("nvarchar(max)");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("LoginProvider", "ProviderKey");

                    b.HasIndex("UserId");

                    b.ToTable("UserLogins", (string)null);
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserRole<System.Guid>", b =>
                {
                    b.Property<Guid>("UserId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("RoleId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("UserId", "RoleId");

                    b.HasIndex("RoleId");

                    b.ToTable("UserRoles", (string)null);
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserToken<System.Guid>", b =>
                {
                    b.Property<Guid>("UserId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("LoginProvider")
                        .HasColumnType("nvarchar(450)");

                    b.Property<string>("Name")
                        .HasColumnType("nvarchar(450)");

                    b.Property<string>("Value")
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("UserId", "LoginProvider", "Name");

                    b.ToTable("UserTokens", (string)null);
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Ai.AiConversation", b =>
                {
                    b.HasOne("ArtCommission.Domain.Entities.Identity.ApplicationUser", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Ai.AiMessage", b =>
                {
                    b.HasOne("ArtCommission.Domain.Entities.Ai.AiConversation", "Conversation")
                        .WithMany("Messages")
                        .HasForeignKey("ConversationId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Conversation");
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Ai.DeadlineReminder", b =>
                {
                    b.HasOne("ArtCommission.Domain.Entities.Commission.Commission", null)
                        .WithMany()
                        .HasForeignKey("CommissionId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("ArtCommission.Domain.Entities.Commission.Milestone", null)
                        .WithMany()
                        .HasForeignKey("MilestoneId")
                        .OnDelete(DeleteBehavior.NoAction);
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Ai.UserReminderSetting", b =>
                {
                    b.HasOne("ArtCommission.Domain.Entities.Identity.ApplicationUser", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.ArtistStudio.Artwork", b =>
                {
                    b.HasOne("ArtCommission.Domain.Entities.ArtistStudio.CreatorProfile", "CreatorProfile")
                        .WithMany("Artworks")
                        .HasForeignKey("CreatorProfileId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("CreatorProfile");
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.ArtistStudio.ArtworkTag", b =>
                {
                    b.HasOne("ArtCommission.Domain.Entities.ArtistStudio.Artwork", "Artwork")
                        .WithMany("ArtworkTags")
                        .HasForeignKey("ArtworkId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("ArtCommission.Domain.Entities.ArtistStudio.Tag", "Tag")
                        .WithMany("ArtworkTags")
                        .HasForeignKey("TagId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Artwork");

                    b.Navigation("Tag");
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.ArtistStudio.CreatorProfile", b =>
                {
                    b.HasOne("ArtCommission.Domain.Entities.Identity.ApplicationUser", "User")
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("User");
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.ArtistStudio.Follow", b =>
                {
                    b.HasOne("ArtCommission.Domain.Entities.ArtistStudio.CreatorProfile", "CreatorProfile")
                        .WithMany()
                        .HasForeignKey("CreatorProfileId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("ArtCommission.Domain.Entities.Identity.ApplicationUser", "Follower")
                        .WithMany()
                        .HasForeignKey("FollowerUserId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.Navigation("CreatorProfile");

                    b.Navigation("Follower");
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Auction.ArtworkOwnership", b =>
                {
                    b.HasOne("ArtCommission.Domain.Entities.ArtistStudio.Artwork", null)
                        .WithMany()
                        .HasForeignKey("ArtworkId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.HasOne("ArtCommission.Domain.Entities.Auction.Auction", null)
                        .WithMany()
                        .HasForeignKey("AuctionId")
                        .OnDelete(DeleteBehavior.SetNull);

                    b.HasOne("ArtCommission.Domain.Entities.Identity.ApplicationUser", null)
                        .WithMany()
                        .HasForeignKey("OwnerId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Auction.Auction", b =>
                {
                    b.HasOne("ArtCommission.Domain.Entities.ArtistStudio.Artwork", "Artwork")
                        .WithMany()
                        .HasForeignKey("ArtworkId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.HasOne("ArtCommission.Domain.Entities.Identity.ApplicationUser", null)
                        .WithMany()
                        .HasForeignKey("SellerId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.HasOne("ArtCommission.Domain.Entities.Identity.ApplicationUser", null)
                        .WithMany()
                        .HasForeignKey("WinnerId")
                        .OnDelete(DeleteBehavior.SetNull);

                    b.Navigation("Artwork");
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Auction.AuctionWatch", b =>
                {
                    b.HasOne("ArtCommission.Domain.Entities.Auction.Auction", "Auction")
                        .WithMany("Watches")
                        .HasForeignKey("AuctionId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("ArtCommission.Domain.Entities.Identity.ApplicationUser", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Auction");
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Auction.Bid", b =>
                {
                    b.HasOne("ArtCommission.Domain.Entities.Auction.Auction", "Auction")
                        .WithMany("Bids")
                        .HasForeignKey("AuctionId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("ArtCommission.Domain.Entities.Identity.ApplicationUser", "Bidder")
                        .WithMany()
                        .HasForeignKey("BidderId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.Navigation("Auction");
                    b.Navigation("Bidder");
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Auction.Deliverable", b =>
                {
                    b.HasOne("ArtCommission.Domain.Entities.Auction.Auction", "Auction")
                        .WithMany()
                        .HasForeignKey("AuctionId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("ArtCommission.Domain.Entities.Identity.ApplicationUser", null)
                        .WithMany()
                        .HasForeignKey("RecipientId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.Navigation("Auction");
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Auction.EscrowTransaction", b =>
                {
                    b.HasOne("ArtCommission.Domain.Entities.Auction.Auction", "Auction")
                        .WithMany()
                        .HasForeignKey("AuctionId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.HasOne("ArtCommission.Domain.Entities.Identity.ApplicationUser", null)
                        .WithMany()
                        .HasForeignKey("PayeeId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.HasOne("ArtCommission.Domain.Entities.Identity.ApplicationUser", null)
                        .WithMany()
                        .HasForeignKey("PayerId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.Navigation("Auction");
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Chat.ChatRoom", b =>
                {
                    b.HasOne("ArtCommission.Domain.Entities.Auction.Auction", null)
                        .WithMany()
                        .HasForeignKey("AuctionId")
                        .OnDelete(DeleteBehavior.SetNull);

                    b.HasOne("ArtCommission.Domain.Entities.Commission.Commission", null)
                        .WithMany()
                        .HasForeignKey("CommissionId")
                        .OnDelete(DeleteBehavior.SetNull);
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Chat.ChatRoomMember", b =>
                {
                    b.HasOne("ArtCommission.Domain.Entities.Chat.ChatRoom", "Room")
                        .WithMany("Members")
                        .HasForeignKey("RoomId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("ArtCommission.Domain.Entities.Identity.ApplicationUser", "User")
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Room");
                    b.Navigation("User");
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Chat.Message", b =>
                {
                    b.HasOne("ArtCommission.Domain.Entities.Chat.ChatRoom", "Room")
                        .WithMany("Messages")
                        .HasForeignKey("RoomId")
                        .OnDelete(DeleteBehavior.Cascade);
        
                    b.HasOne("ArtCommission.Domain.Entities.Identity.ApplicationUser", "Sender")
                        .WithMany()
                        .HasForeignKey("SenderId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();
                        
                    b.Navigation("Room");
                    b.Navigation("Sender");
               });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Chat.MessageAttachment", b =>
              {
                  b.HasOne("ArtCommission.Domain.Entities.Chat.Message", "Message")
                      .WithMany("Attachments")
                      .HasForeignKey("MessageId")
                      .OnDelete(DeleteBehavior.Cascade)
                      .IsRequired();

                  b.HasOne("ArtCommission.Domain.Entities.Identity.ApplicationUser", null)
                      .WithMany()
                      .HasForeignKey("UploadedBy")
                      .OnDelete(DeleteBehavior.Restrict)
                      .IsRequired();

                  b.Navigation("Message");
              });
            
            modelBuilder.Entity("ArtCommission.Domain.Entities.Commission.Dispute", b =>
                {
                    b.HasOne("ArtCommission.Domain.Entities.Commission.Commission", "Commission")
                        .WithMany("Disputes")
                        .HasForeignKey("CommissionId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.Navigation("Commission");
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Commission.Milestone", b =>
                {
                    b.HasOne("ArtCommission.Domain.Entities.Commission.Commission", "Commission")
                        .WithMany("Milestones")
                        .HasForeignKey("CommissionId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Commission");
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Commission.Review", b =>
                {
                    b.HasOne("ArtCommission.Domain.Entities.Commission.Commission", "Commission")
                        .WithMany("Reviews")
                        .HasForeignKey("CommissionId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.Navigation("Commission");
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.CreatorApplication.CreatorApplication", b =>
                {
                    b.HasOne("ArtCommission.Domain.Entities.Identity.ApplicationUser", "Applicant")
                        .WithMany()
                        .HasForeignKey("ApplicantId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.HasOne("ArtCommission.Domain.Entities.Identity.ApplicationUser", "ReviewedByMod")
                        .WithMany()
                        .HasForeignKey("ReviewedByModId")
                        .OnDelete(DeleteBehavior.Restrict);

                    b.Navigation("Applicant");

                    b.Navigation("ReviewedByMod");
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Identity.RefreshToken", b =>
                {
                    b.HasOne("ArtCommission.Domain.Entities.Identity.ApplicationUser", "User")
                        .WithMany("RefreshTokens")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("User");
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Identity.UserSanction", b =>
                {
                    b.HasOne("ArtCommission.Domain.Entities.Identity.ApplicationUser", "ActionByAdmin")
                        .WithMany()
                        .HasForeignKey("ActionByAdminId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.HasOne("ArtCommission.Domain.Entities.Identity.ApplicationUser", "User")
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.Navigation("ActionByAdmin");

                    b.Navigation("User");
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Notifications.Notification", b =>
                {
                    b.HasOne("ArtCommission.Domain.Entities.Identity.ApplicationUser", "User")
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("User");
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Payment.BankAccount", b =>
                {
                    b.HasOne("ArtCommission.Domain.Entities.Identity.ApplicationUser", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Payment.PaymentOrder", b =>
                {
                    b.HasOne("ArtCommission.Domain.Entities.Payment.Wallet", "Wallet")
                        .WithMany()
                        .HasForeignKey("WalletId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.Navigation("Wallet");
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Payment.PayoutRequest", b =>
                {
                    b.HasOne("ArtCommission.Domain.Entities.Payment.BankAccount", "BankAccount")
                        .WithMany()
                        .HasForeignKey("BankAccountId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.HasOne("ArtCommission.Domain.Entities.Identity.ApplicationUser", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.HasOne("ArtCommission.Domain.Entities.Payment.Wallet", null)
                        .WithMany()
                        .HasForeignKey("WalletId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.Navigation("BankAccount");
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Payment.Voucher", b =>
                {
                    b.HasOne("ArtCommission.Domain.Entities.Identity.ApplicationUser", null)
                        .WithMany()
                        .HasForeignKey("CreatedByUserId")
                        .OnDelete(DeleteBehavior.SetNull);
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Payment.VoucherRedemption", b =>
                {
                    b.HasOne("ArtCommission.Domain.Entities.Identity.ApplicationUser", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.HasOne("ArtCommission.Domain.Entities.Payment.Voucher", "Voucher")
                        .WithMany("Redemptions")
                        .HasForeignKey("VoucherId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Voucher");
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Payment.Wallet", b =>
                {
                    b.HasOne("ArtCommission.Domain.Entities.Identity.ApplicationUser", "User")
                        .WithOne()
                        .HasForeignKey("ArtCommission.Domain.Entities.Payment.Wallet", "UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("User");
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Payment.WalletTransaction", b =>
                {
                    b.HasOne("ArtCommission.Domain.Entities.Payment.Wallet", "Wallet")
                        .WithMany("Transactions")
                        .HasForeignKey("WalletId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Wallet");
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Revenue.RevenueSnapshot", b =>
                {
                    b.HasOne("ArtCommission.Domain.Entities.Identity.ApplicationUser", null)
                        .WithMany()
                        .HasForeignKey("CreatorId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityRoleClaim<System.Guid>", b =>
                {
                    b.HasOne("Microsoft.AspNetCore.Identity.IdentityRole<System.Guid>", null)
                        .WithMany()
                        .HasForeignKey("RoleId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserClaim<System.Guid>", b =>
                {
                    b.HasOne("ArtCommission.Domain.Entities.Identity.ApplicationUser", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserLogin<System.Guid>", b =>
                {
                    b.HasOne("ArtCommission.Domain.Entities.Identity.ApplicationUser", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserRole<System.Guid>", b =>
                {
                    b.HasOne("Microsoft.AspNetCore.Identity.IdentityRole<System.Guid>", null)
                        .WithMany()
                        .HasForeignKey("RoleId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("ArtCommission.Domain.Entities.Identity.ApplicationUser", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.IdentityUserToken<System.Guid>", b =>
                {
                    b.HasOne("ArtCommission.Domain.Entities.Identity.ApplicationUser", null)
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Ai.AiConversation", b =>
                {
                    b.Navigation("Messages");
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.ArtistStudio.Artwork", b =>
                {
                    b.Navigation("ArtworkTags");
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.ArtistStudio.CreatorProfile", b =>
                {
                    b.Navigation("Artworks");
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.ArtistStudio.Tag", b =>
                {
                    b.Navigation("ArtworkTags");
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Auction.Auction", b =>
                {
                    b.Navigation("Bids");

                    b.Navigation("Watches");
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Chat.ChatRoom", b =>
                {
                    b.Navigation("Members");

                    b.Navigation("Messages");
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Chat.Message", b =>
                {
                    b.Navigation("Attachments");
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Commission.Commission", b =>
                {
                    b.Navigation("Disputes");

                    b.Navigation("Milestones");

                    b.Navigation("Reviews");
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Identity.ApplicationUser", b =>
                {
                    b.Navigation("RefreshTokens");
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Payment.Voucher", b =>
                {
                    b.Navigation("Redemptions");
                });

            modelBuilder.Entity("ArtCommission.Domain.Entities.Payment.Wallet", b =>
                {
                    b.Navigation("Transactions");
                });
#pragma warning restore 612, 618
        }
    }
}
