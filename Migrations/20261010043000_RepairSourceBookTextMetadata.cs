using EduMation.Data;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduMation.Migrations;

public sealed partial class RepairSourceBookTextMetadata : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        IF COL_LENGTH(N'dbo.SourceBooks', N'PageCount') IS NULL
        BEGIN
            ALTER TABLE [dbo].[SourceBooks] ADD [PageCount] int NOT NULL
                CONSTRAINT [DF_SourceBooks_PageCount_Repair] DEFAULT 0;
        END;
        IF COL_LENGTH(N'dbo.SourceBooks', N'ExtractedText') IS NULL
        BEGIN
            ALTER TABLE [dbo].[SourceBooks] ADD [ExtractedText] nvarchar(max) NOT NULL
                CONSTRAINT [DF_SourceBooks_ExtractedText_Repair] DEFAULT N'';
        END;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        IF COL_LENGTH(N'dbo.SourceBooks', N'ExtractedText') IS NOT NULL
            ALTER TABLE [dbo].[SourceBooks] DROP COLUMN [ExtractedText];
        IF COL_LENGTH(N'dbo.SourceBooks', N'PageCount') IS NOT NULL
            ALTER TABLE [dbo].[SourceBooks] DROP COLUMN [PageCount];
        """);
}
