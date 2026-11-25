using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SMGS.Infrastructure.Data;

namespace SMGS.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261125000000_RewardSettings")]
public class AddRewardSettings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("RewardSettings", table => new {
            Id = table.Column<int>(nullable: false),
            ScanPoints = table.Column<int>(nullable: false),
            CorrectAnswerPoints = table.Column<int>(nullable: false),
            TourPoints = table.Column<int>(nullable: false)
        }, constraints: table => table.PrimaryKey("PK_RewardSettings", x => x.Id));

        migrationBuilder.Sql("""
            INSERT INTO badges ("Code", "Name", "RuleJson") VALUES
              ('EXPLORE_SCAN_1', 'Lần đầu khám phá', '{"Metric":"scan_artifacts","Threshold":1}'),
              ('EXPLORE_SCAN_10', 'Nhà thám hiểm', '{"Metric":"scan_artifacts","Threshold":10}'),
              ('EXPLORE_QUIZ_5', 'Am hiểu hiện vật', '{"Metric":"quiz_correct","Threshold":5}'),
              ('EXPLORE_TOUR_1', 'Trọn hành trình', '{"Metric":"tours_completed","Threshold":1}'),
              ('EXPLORE_TOUR_3', 'Nhà du hành bảo tàng', '{"Metric":"tours_completed","Threshold":3}')
            ON CONFLICT ("Code") DO NOTHING;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("RewardSettings");
}
