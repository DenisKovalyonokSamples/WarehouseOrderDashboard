-- Sample data script for Oracle Warehouse database
-- Creates at least 100 rows for every table defined in oracle_schema.sql
-- Safe to re-run: it clears existing data first using child-to-parent delete order.

SET DEFINE OFF;

PROMPT Cleaning existing data...
DELETE FROM "PickingTaskLines";
DELETE FROM "PickingTasks";
DELETE FROM "StockReservations";
DELETE FROM "OrderLines";
DELETE FROM "WarehouseOrders";
DELETE FROM "StockBalances";
DELETE FROM "Items";
DELETE FROM "Warehouses";
DELETE FROM "Customers";
DELETE FROM "AuditLogs";
COMMIT;

PROMPT Inserting Customers (100)...
INSERT INTO "Customers" ("Code", "Name", "CreatedAt", "Version")
SELECT
    'CUST-' || LPAD(level, 4, '0'),
    'Customer ' || level,
    SYSTIMESTAMP - NUMTODSINTERVAL(level, 'DAY'),
    0
FROM dual
CONNECT BY level <= 100;

PROMPT Inserting Warehouses (100)...
INSERT INTO "Warehouses" ("Code", "Name", "CreatedAt", "Version")
SELECT
    'WH-' || LPAD(level, 4, '0'),
    'Warehouse ' || level,
    SYSTIMESTAMP - NUMTODSINTERVAL(level, 'DAY'),
    0
FROM dual
CONNECT BY level <= 100;

PROMPT Inserting Items (100)...
INSERT INTO "Items" ("Sku", "Name", "CreatedAt", "Version")
SELECT
    'SKU-' || LPAD(level, 4, '0'),
    'Item ' || level,
    SYSTIMESTAMP - NUMTODSINTERVAL(level, 'DAY'),
    0
FROM dual
CONNECT BY level <= 100;

PROMPT Inserting StockBalances (100)...
INSERT INTO "StockBalances" ("ItemId", "WarehouseId", "AvailableQuantity", "ReservedQuantity", "CreatedAt", "Version")
WITH seq AS (
    SELECT level AS n
    FROM dual
    CONNECT BY level <= 100
)
SELECT
    item_row."Id",
    warehouse_row."Id",
    500 + MOD(seq.n * 7, 250),
    MOD(seq.n * 3, 40),
    SYSTIMESTAMP - NUMTODSINTERVAL(seq.n, 'HOUR'),
    0
FROM seq
JOIN "Items" item_row
    ON item_row."Sku" = 'SKU-' || LPAD(seq.n, 4, '0')
JOIN "Warehouses" warehouse_row
    ON warehouse_row."Code" = 'WH-' || LPAD(seq.n, 4, '0');

PROMPT Inserting WarehouseOrders (100)...
INSERT INTO "WarehouseOrders" (
    "OrderNumber",
    "CustomerId",
    "WarehouseId",
    "Status",
    "PickingStartedAt",
    "ShippedAt",
    "CreatedAt",
    "Version"
)
WITH seq AS (
    SELECT level AS n
    FROM dual
    CONNECT BY level <= 100
)
SELECT
    'ORD-' || LPAD(seq.n, 6, '0') AS "OrderNumber",
    customer_row."Id" AS "CustomerId",
    warehouse_row."Id" AS "WarehouseId",
    CASE MOD(seq.n, 6)
        WHEN 0 THEN 'New'
        WHEN 1 THEN 'Confirmed'
        WHEN 2 THEN 'PartiallyReserved'
        WHEN 3 THEN 'Reserved'
        WHEN 4 THEN 'InPicking'
        ELSE 'Shipped'
    END AS "Status",
    CASE
        WHEN MOD(seq.n, 6) IN (4, 5)
            THEN SYSTIMESTAMP - NUMTODSINTERVAL(MOD(seq.n, 48), 'HOUR')
        ELSE NULL
    END AS "PickingStartedAt",
    CASE
        WHEN MOD(seq.n, 6) = 5
            THEN SYSTIMESTAMP - NUMTODSINTERVAL(MOD(seq.n, 24), 'HOUR')
        ELSE NULL
    END AS "ShippedAt",
    SYSTIMESTAMP - NUMTODSINTERVAL(seq.n, 'DAY') AS "CreatedAt",
    0 AS "Version"
FROM seq
JOIN "Customers" customer_row
    ON customer_row."Code" = 'CUST-' || LPAD(seq.n, 4, '0')
JOIN "Warehouses" warehouse_row
    ON warehouse_row."Code" = 'WH-' || LPAD(seq.n, 4, '0');

PROMPT Inserting OrderLines (300)...
INSERT INTO "OrderLines" (
    "OrderId",
    "ItemId",
    "Quantity",
    "ReservedQuantity",
    "PickedQuantity",
    "CreatedAt",
    "Version"
)
WITH order_seq AS (
    SELECT level AS order_n
    FROM dual
    CONNECT BY level <= 100
),
line_seq AS (
    SELECT level AS line_n
    FROM dual
    CONNECT BY level <= 3
)
SELECT
    order_row."Id" AS "OrderId",
    item_row."Id" AS "ItemId",
    CAST(2 + line_seq.line_n + MOD(order_seq.order_n, 5) AS NUMBER(18,3)) AS "Quantity",
    CAST(
        CASE
            WHEN order_row."Status" IN ('Confirmed', 'PartiallyReserved', 'Reserved', 'InPicking', 'Shipped')
                THEN GREATEST(0, (2 + line_seq.line_n + MOD(order_seq.order_n, 5)) - CASE WHEN MOD(order_seq.order_n + line_seq.line_n, 4) = 0 THEN 1 ELSE 0 END)
            ELSE 0
        END
        AS NUMBER(18,3)
    ) AS "ReservedQuantity",
    CAST(
        CASE
            WHEN order_row."Status" = 'Shipped'
                THEN GREATEST(0, (2 + line_seq.line_n + MOD(order_seq.order_n, 5)) - CASE WHEN MOD(order_seq.order_n + line_seq.line_n, 4) = 0 THEN 1 ELSE 0 END)
            WHEN order_row."Status" = 'InPicking'
                THEN ROUND(GREATEST(0, (2 + line_seq.line_n + MOD(order_seq.order_n, 5)) - CASE WHEN MOD(order_seq.order_n + line_seq.line_n, 4) = 0 THEN 1 ELSE 0 END) / 2, 3)
            ELSE 0
        END
        AS NUMBER(18,3)
    ) AS "PickedQuantity",
    order_row."CreatedAt" + NUMTODSINTERVAL(line_seq.line_n, 'MINUTE') AS "CreatedAt",
    0 AS "Version"
FROM order_seq
CROSS JOIN line_seq
JOIN "WarehouseOrders" order_row
    ON order_row."OrderNumber" = 'ORD-' || LPAD(order_seq.order_n, 6, '0')
JOIN "Items" item_row
    ON item_row."Sku" = 'SKU-' || LPAD(MOD(order_seq.order_n + line_seq.line_n - 2, 100) + 1, 4, '0');

PROMPT Inserting StockReservations (>=100)...
INSERT INTO "StockReservations" ("OrderLineId", "WarehouseId", "ItemId", "Quantity", "CreatedAt", "Version")
SELECT
    order_line_row."Id",
    order_row."WarehouseId",
    order_line_row."ItemId",
    order_line_row."ReservedQuantity",
    order_line_row."CreatedAt",
    0
FROM "OrderLines" order_line_row
JOIN "WarehouseOrders" order_row
    ON order_row."Id" = order_line_row."OrderId"
WHERE order_line_row."ReservedQuantity" > 0;

PROMPT Inserting PickingTasks (100)...
INSERT INTO "PickingTasks" ("TaskNumber", "Status", "CreatedAt", "Version")
SELECT
    'PT-' || LPAD(level, 6, '0'),
    CASE MOD(level, 3)
        WHEN 0 THEN 'New'
        WHEN 1 THEN 'InProgress'
        ELSE 'Completed'
    END,
    SYSTIMESTAMP - NUMTODSINTERVAL(level, 'HOUR'),
    0
FROM dual
CONNECT BY level <= 100;

PROMPT Inserting PickingTaskLines (>=100)...
INSERT INTO "PickingTaskLines" ("PickingTaskId", "OrderLineId", "Quantity", "PickedQuantity", "CreatedAt", "Version")
SELECT
    picking_task_row."Id",
    order_line_row."Id",
    line_quantity,
    CASE picking_task_row."Status"
        WHEN 'Completed' THEN line_quantity
        WHEN 'InProgress' THEN ROUND(line_quantity / 2, 3)
        ELSE 0
    END AS "PickedQuantity",
    order_line_row."CreatedAt",
    0
FROM (
    SELECT
        order_line_row."Id",
        order_line_row."OrderId",
        CASE
            WHEN order_line_row."ReservedQuantity" > 0 THEN order_line_row."ReservedQuantity"
            ELSE order_line_row."Quantity"
        END AS line_quantity,
        order_line_row."CreatedAt"
    FROM "OrderLines" order_line_row
) order_line_row
JOIN "WarehouseOrders" order_row
    ON order_row."Id" = order_line_row."OrderId"
JOIN "PickingTasks" picking_task_row
    ON picking_task_row."TaskNumber" = 'PT-' || SUBSTR(order_row."OrderNumber", 5);

PROMPT Inserting AuditLogs (>=100)...
INSERT INTO "AuditLogs" ("EntityName", "EntityId", "Action", "Details", "CreatedAt", "Version")
SELECT
    'WarehouseOrder',
    order_row."Id",
    'Seeded',
    'Sample order seeded: ' || order_row."OrderNumber",
    order_row."CreatedAt",
    0
FROM "WarehouseOrders" order_row;

INSERT INTO "AuditLogs" ("EntityName", "EntityId", "Action", "Details", "CreatedAt", "Version")
SELECT
    'PickingTask',
    picking_task_row."Id",
    'Seeded',
    'Sample picking task seeded: ' || picking_task_row."TaskNumber",
    picking_task_row."CreatedAt",
    0
FROM "PickingTasks" picking_task_row;

COMMIT;

PROMPT Verifying row counts...
SELECT 'Customers' AS table_name, COUNT(*) AS rows_count FROM "Customers"
UNION ALL SELECT 'Warehouses', COUNT(*) FROM "Warehouses"
UNION ALL SELECT 'Items', COUNT(*) FROM "Items"
UNION ALL SELECT 'StockBalances', COUNT(*) FROM "StockBalances"
UNION ALL SELECT 'WarehouseOrders', COUNT(*) FROM "WarehouseOrders"
UNION ALL SELECT 'OrderLines', COUNT(*) FROM "OrderLines"
UNION ALL SELECT 'StockReservations', COUNT(*) FROM "StockReservations"
UNION ALL SELECT 'PickingTasks', COUNT(*) FROM "PickingTasks"
UNION ALL SELECT 'PickingTaskLines', COUNT(*) FROM "PickingTaskLines"
UNION ALL SELECT 'AuditLogs', COUNT(*) FROM "AuditLogs";
