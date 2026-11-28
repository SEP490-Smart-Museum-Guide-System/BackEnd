using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SMGS.Infrastructure.Data;

namespace SMGS.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261128140000_AddAdmissionTickets")]
public class AddAdmissionTickets : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE purchase_orders ADD COLUMN "DigitalCancelledAt" timestamptz;
        CREATE TABLE admission_tickets (
            "OrderId" uuid PRIMARY KEY REFERENCES purchase_orders ("Id") ON DELETE RESTRICT,
            "Code" varchar(64) NOT NULL,
            "Quantity" integer NOT NULL,
            "Amount" numeric(18,2) NOT NULL,
            "CancelledAt" timestamptz,
            "RefundedAmount" numeric(18,2) NOT NULL
        );
        CREATE UNIQUE INDEX "IX_admission_tickets_Code" ON admission_tickets ("Code");
        CREATE TABLE admission_check_ins (
            "OrderId" uuid REFERENCES admission_tickets ("OrderId") ON DELETE RESTRICT,
            "OperationId" uuid,
            "StaffId" uuid NOT NULL REFERENCES "Users" ("Id") ON DELETE RESTRICT,
            "Quantity" integer NOT NULL,
            "CheckedInAt" timestamptz NOT NULL,
            PRIMARY KEY ("OrderId", "OperationId")
        );
        CREATE INDEX "IX_admission_check_ins_StaffId" ON admission_check_ins ("StaffId");
        """);

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("admission_check_ins");
        migrationBuilder.DropTable("admission_tickets");
        migrationBuilder.DropColumn("DigitalCancelledAt", "purchase_orders");
    }
}
