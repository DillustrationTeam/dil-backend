using ArtCommission.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtCommission.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260930095000_AddCreatorWorkstationTables")]
public class AddCreatorWorkstationTables : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Some databases already have these tables from SyncLocalRuntimeSchema.
        // Create only missing objects, preserving their data and migration history.
        migrationBuilder.Sql("""
IF OBJECT_ID(N'[dbo].[CreatorAssets]', N'U') IS NULL
BEGIN
CREATE TABLE [CreatorAssets] (
    [Id] uniqueidentifier NOT NULL,
    [CreatorProfileId] uniqueidentifier NOT NULL,
    [AssetType] nvarchar(max) NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [AssetUrl] nvarchar(max) NULL,
    [MetadataJson] nvarchar(max) NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [UpdatedAt] datetimeoffset NULL,
    [IsDeleted] bit NOT NULL,
    CONSTRAINT [PK_CreatorAssets] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CreatorAssets_CreatorProfiles_CreatorProfileId] FOREIGN KEY ([CreatorProfileId]) REFERENCES [CreatorProfiles] ([Id]) ON DELETE CASCADE
);
END;

IF OBJECT_ID(N'[dbo].[CreatorAutoReplySettings]', N'U') IS NULL
BEGIN
CREATE TABLE [CreatorAutoReplySettings] (
    [Id] uniqueidentifier NOT NULL,
    [CreatorProfileId] uniqueidentifier NOT NULL,
    [IsEnabled] bit NOT NULL,
    [BriefTemplate] nvarchar(max) NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [UpdatedAt] datetimeoffset NULL,
    [IsDeleted] bit NOT NULL,
    CONSTRAINT [PK_CreatorAutoReplySettings] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CreatorAutoReplySettings_CreatorProfiles_CreatorProfileId] FOREIGN KEY ([CreatorProfileId]) REFERENCES [CreatorProfiles] ([Id]) ON DELETE CASCADE
);
END;

IF OBJECT_ID(N'[dbo].[CreatorFaqs]', N'U') IS NULL
BEGIN
CREATE TABLE [CreatorFaqs] (
    [Id] uniqueidentifier NOT NULL,
    [CreatorProfileId] uniqueidentifier NOT NULL,
    [Question] nvarchar(max) NOT NULL,
    [Answer] nvarchar(max) NOT NULL,
    [DisplayOrder] int NOT NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [UpdatedAt] datetimeoffset NULL,
    [IsDeleted] bit NOT NULL,
    CONSTRAINT [PK_CreatorFaqs] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CreatorFaqs_CreatorProfiles_CreatorProfileId] FOREIGN KEY ([CreatorProfileId]) REFERENCES [CreatorProfiles] ([Id]) ON DELETE CASCADE
);
END;

IF OBJECT_ID(N'[dbo].[CreatorTerms]', N'U') IS NULL
BEGIN
CREATE TABLE [CreatorTerms] (
    [Id] uniqueidentifier NOT NULL,
    [CreatorProfileId] uniqueidentifier NOT NULL,
    [CommercialLicenseMultiplier] decimal(18,2) NOT NULL,
    [RevisionPolicy] nvarchar(max) NULL,
    [CancellationPolicy] nvarchar(max) NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [UpdatedAt] datetimeoffset NULL,
    [IsDeleted] bit NOT NULL,
    CONSTRAINT [PK_CreatorTerms] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CreatorTerms_CreatorProfiles_CreatorProfileId] FOREIGN KEY ([CreatorProfileId]) REFERENCES [CreatorProfiles] ([Id]) ON DELETE CASCADE
);
END;

IF OBJECT_ID(N'[dbo].[CreatorWorkItems]', N'U') IS NULL
BEGIN
CREATE TABLE [CreatorWorkItems] (
    [Id] uniqueidentifier NOT NULL,
    [CreatorProfileId] uniqueidentifier NOT NULL,
    [ClientName] nvarchar(max) NOT NULL,
    [Title] nvarchar(max) NOT NULL,
    [Stage] nvarchar(max) NOT NULL,
    [DueAt] datetimeoffset NULL,
    [CreatedAt] datetimeoffset NOT NULL,
    [UpdatedAt] datetimeoffset NULL,
    [IsDeleted] bit NOT NULL,
    CONSTRAINT [PK_CreatorWorkItems] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CreatorWorkItems_CreatorProfiles_CreatorProfileId] FOREIGN KEY ([CreatorProfileId]) REFERENCES [CreatorProfiles] ([Id]) ON DELETE CASCADE
);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[CreatorAssets]') AND name = N'IX_CreatorAssets_CreatorProfileId')
BEGIN
CREATE INDEX [IX_CreatorAssets_CreatorProfileId] ON [CreatorAssets] ([CreatorProfileId]);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[CreatorAutoReplySettings]') AND name = N'IX_CreatorAutoReplySettings_CreatorProfileId')
BEGIN
CREATE INDEX [IX_CreatorAutoReplySettings_CreatorProfileId] ON [CreatorAutoReplySettings] ([CreatorProfileId]);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[CreatorFaqs]') AND name = N'IX_CreatorFaqs_CreatorProfileId')
BEGIN
CREATE INDEX [IX_CreatorFaqs_CreatorProfileId] ON [CreatorFaqs] ([CreatorProfileId]);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[CreatorTerms]') AND name = N'IX_CreatorTerms_CreatorProfileId')
BEGIN
CREATE INDEX [IX_CreatorTerms_CreatorProfileId] ON [CreatorTerms] ([CreatorProfileId]);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[CreatorWorkItems]') AND name = N'IX_CreatorWorkItems_CreatorProfileId')
BEGIN
CREATE INDEX [IX_CreatorWorkItems_CreatorProfileId] ON [CreatorWorkItems] ([CreatorProfileId]);
END;
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Do not drop tables shared with the older schema-repair migration.
    }
}
