using Fookbase.Api.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fookbase.Api.Persistence.Migrations;

[DbContext(typeof(FookbaseDbContext))]
[Migration("20260912100000_AddGlobalSearchIndexes")]
public partial class AddGlobalSearchIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // PostgreSQL ILIKE '%query%' predicates below use pg_trgm's GIN operator classes.
        migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");

        // People name and username search.
        migrationBuilder.Sql("""
            CREATE INDEX IF NOT EXISTS "IX_UserProfiles_SearchDisplayNameTrgm"
            ON "UserProfiles" USING gin ("DisplayName" gin_trgm_ops);
            CREATE INDEX IF NOT EXISTS "IX_UserProfiles_SearchUsernameTrgm"
            ON "UserProfiles" USING gin ("Username" gin_trgm_ops);
            """);

        // Group name and description search, restricted to active groups.
        migrationBuilder.Sql("""
            CREATE INDEX IF NOT EXISTS "IX_Groups_SearchNameTrgm"
            ON "Groups" USING gin ("Name" gin_trgm_ops)
            WHERE "DeletedAtUtc" IS NULL;
            CREATE INDEX IF NOT EXISTS "IX_Groups_SearchDescriptionTrgm"
            ON "Groups" USING gin ("Description" gin_trgm_ops)
            WHERE "DeletedAtUtc" IS NULL;
            """);

        // Published Page discovery by name and username.
        migrationBuilder.Sql("""
            CREATE INDEX IF NOT EXISTS "IX_Pages_SearchNameTrgm"
            ON "Pages" USING gin ("Name" gin_trgm_ops)
            WHERE "DeletedAtUtc" IS NULL AND "Status" = 0;
            CREATE INDEX IF NOT EXISTS "IX_Pages_SearchUsernameTrgm"
            ON "Pages" USING gin ("Username" gin_trgm_ops)
            WHERE "DeletedAtUtc" IS NULL AND "Status" = 0;
            """);

        // Caption/body search is scoped by post type and excludes deleted posts.
        migrationBuilder.Sql("""
            CREATE INDEX IF NOT EXISTS "IX_Posts_SearchStandardContentTrgm"
            ON "Posts" USING gin ("Content" gin_trgm_ops)
            WHERE "DeletedAtUtc" IS NULL AND "PostType" = 0;
            CREATE INDEX IF NOT EXISTS "IX_Posts_SearchReelContentTrgm"
            ON "Posts" USING gin ("Content" gin_trgm_ops)
            WHERE "DeletedAtUtc" IS NULL AND "PostType" = 1;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_UserProfiles_SearchDisplayNameTrgm\";");
        migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_UserProfiles_SearchUsernameTrgm\";");
        migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Groups_SearchNameTrgm\";");
        migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Groups_SearchDescriptionTrgm\";");
        migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Pages_SearchNameTrgm\";");
        migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Pages_SearchUsernameTrgm\";");
        migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Posts_SearchStandardContentTrgm\";");
        migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_Posts_SearchReelContentTrgm\";");
        // pg_trgm is shared PostgreSQL database infrastructure and may be used by other migrations.
    }
}
