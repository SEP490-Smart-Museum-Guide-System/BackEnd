-- Upgrade commerce for date-based admission, wallet credits and cancellations.
-- Run once against the existing SMGS PostgreSQL database.
BEGIN;

ALTER TABLE purchase_orders ADD COLUMN IF NOT EXISTS "VisitDate" date;
ALTER TABLE purchase_orders ADD COLUMN IF NOT EXISTS "RefundedAmount" numeric(18,2) NOT NULL DEFAULT 0;
ALTER TABLE purchase_orders ADD COLUMN IF NOT EXISTS "CancelledAt" timestamp with time zone;

-- Preserve existing orders by assigning their creation date in Vietnam time.
UPDATE purchase_orders
SET "VisitDate" = ("CreatedAt" AT TIME ZONE 'Asia/Ho_Chi_Minh')::date
WHERE "VisitDate" IS NULL;
ALTER TABLE purchase_orders ALTER COLUMN "VisitDate" SET NOT NULL;

-- The new policy is one visit day; old products are normalized to that policy.
UPDATE commerce_products SET "ValidityHours" = 24, "ValidityStartsAt" = 'visit_date';
UPDATE purchase_orders SET "ValidityHours" = 24, "ValidityStartsAt" = 'visit_date';

CREATE TABLE IF NOT EXISTS wallet_transactions (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "OrderId" uuid,
    "Amount" numeric(18,2) NOT NULL,
    "Currency" character varying(3) NOT NULL,
    "Type" character varying(20) NOT NULL,
    "Description" character varying(500) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_wallet_transactions" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_wallet_transactions_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_wallet_transactions_purchase_orders_OrderId" FOREIGN KEY ("OrderId") REFERENCES purchase_orders ("Id") ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS "IX_wallet_transactions_UserId_CreatedAt" ON wallet_transactions ("UserId", "CreatedAt");
CREATE INDEX IF NOT EXISTS "IX_wallet_transactions_OrderId" ON wallet_transactions ("OrderId") WHERE "OrderId" IS NOT NULL;
COMMIT;
