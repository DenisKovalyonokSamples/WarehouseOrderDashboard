-- Sample data script for Oracle Warehouse database
-- Creates at least 100 rows for every table defined in oracle_schema.sql
-- Safe to re-run: it clears existing data first using child-to-parent delete order.

SET DEFINE OFF;

PROMPT Cleaning existing data...
DELETE FROM picking_task_lines;
DELETE FROM picking_tasks;
DELETE FROM stock_reservations;
DELETE FROM order_lines;
DELETE FROM warehouse_orders;
DELETE FROM stock_balances;
DELETE FROM items;
DELETE FROM warehouses;
DELETE FROM customers;
DELETE FROM audit_logs;
COMMIT;

PROMPT Inserting customers (100)...
INSERT INTO customers (code, name, created_at, version)
SELECT
    'CUST-' || LPAD(level, 4, '0'),
    'Customer ' || level,
    SYSTIMESTAMP - NUMTODSINTERVAL(level, 'DAY'),
    0
FROM dual
CONNECT BY level <= 100;

PROMPT Inserting warehouses (100)...
INSERT INTO warehouses (code, name, created_at, version)
SELECT
    'WH-' || LPAD(level, 4, '0'),
    'Warehouse ' || level,
    SYSTIMESTAMP - NUMTODSINTERVAL(level, 'DAY'),
    0
FROM dual
CONNECT BY level <= 100;

PROMPT Inserting items (100)...
INSERT INTO items (sku, name, created_at, version)
SELECT
    'SKU-' || LPAD(level, 4, '0'),
    'Item ' || level,
    SYSTIMESTAMP - NUMTODSINTERVAL(level, 'DAY'),
    0
FROM dual
CONNECT BY level <= 100;

PROMPT Inserting stock_balances (100)...
WITH seq AS (
    SELECT level AS n
    FROM dual
    CONNECT BY level <= 100
)
INSERT INTO stock_balances (item_id, warehouse_id, available_quantity, reserved_quantity, created_at, version)
SELECT
    item_row.id,
    warehouse_row.id,
    500 + MOD(seq.n * 7, 250),
    MOD(seq.n * 3, 40),
    SYSTIMESTAMP - NUMTODSINTERVAL(seq.n, 'HOUR'),
    0
FROM seq
JOIN items item_row
    ON item_row.sku = 'SKU-' || LPAD(seq.n, 4, '0')
JOIN warehouses warehouse_row
    ON warehouse_row.code = 'WH-' || LPAD(seq.n, 4, '0');

PROMPT Inserting warehouse_orders (100)...
WITH seq AS (
    SELECT level AS n
    FROM dual
    CONNECT BY level <= 100
)
INSERT INTO warehouse_orders (
    order_number,
    customer_id,
    warehouse_id,
    status,
    picking_started_at,
    shipped_at,
    created_at,
    version
)
SELECT
    'ORD-' || LPAD(seq.n, 6, '0') AS order_number,
    customer_row.id AS customer_id,
    warehouse_row.id AS warehouse_id,
    CASE MOD(seq.n, 6)
        WHEN 0 THEN 'New'
        WHEN 1 THEN 'Confirmed'
        WHEN 2 THEN 'PartiallyReserved'
        WHEN 3 THEN 'Reserved'
        WHEN 4 THEN 'InPicking'
        ELSE 'Shipped'
    END AS status,
    CASE
        WHEN MOD(seq.n, 6) IN (4, 5)
            THEN SYSTIMESTAMP - NUMTODSINTERVAL(MOD(seq.n, 48), 'HOUR')
        ELSE NULL
    END AS picking_started_at,
    CASE
        WHEN MOD(seq.n, 6) = 5
            THEN SYSTIMESTAMP - NUMTODSINTERVAL(MOD(seq.n, 24), 'HOUR')
        ELSE NULL
    END AS shipped_at,
    SYSTIMESTAMP - NUMTODSINTERVAL(seq.n, 'DAY') AS created_at,
    0 AS version
FROM seq
JOIN customers customer_row
    ON customer_row.code = 'CUST-' || LPAD(seq.n, 4, '0')
JOIN warehouses warehouse_row
    ON warehouse_row.code = 'WH-' || LPAD(seq.n, 4, '0');

PROMPT Inserting order_lines (300)...
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
INSERT INTO order_lines (
    order_id,
    item_id,
    quantity,
    reserved_quantity,
    picked_quantity,
    created_at,
    version
)
SELECT
    order_row.id AS order_id,
    item_row.id AS item_id,
    CAST(2 + line_seq.line_n + MOD(order_seq.order_n, 5) AS NUMBER(18,3)) AS quantity,
    CAST(
        CASE
            WHEN order_row.status IN ('Confirmed', 'PartiallyReserved', 'Reserved', 'InPicking', 'Shipped')
                THEN GREATEST(0, (2 + line_seq.line_n + MOD(order_seq.order_n, 5)) - CASE WHEN MOD(order_seq.order_n + line_seq.line_n, 4) = 0 THEN 1 ELSE 0 END)
            ELSE 0
        END
        AS NUMBER(18,3)
    ) AS reserved_quantity,
    CAST(
        CASE
            WHEN order_row.status = 'Shipped'
                THEN GREATEST(0, (2 + line_seq.line_n + MOD(order_seq.order_n, 5)) - CASE WHEN MOD(order_seq.order_n + line_seq.line_n, 4) = 0 THEN 1 ELSE 0 END)
            WHEN order_row.status = 'InPicking'
                THEN ROUND(GREATEST(0, (2 + line_seq.line_n + MOD(order_seq.order_n, 5)) - CASE WHEN MOD(order_seq.order_n + line_seq.line_n, 4) = 0 THEN 1 ELSE 0 END) / 2, 3)
            ELSE 0
        END
        AS NUMBER(18,3)
    ) AS picked_quantity,
    order_row.created_at + NUMTODSINTERVAL(line_seq.line_n, 'MINUTE') AS created_at,
    0 AS version
FROM order_seq
CROSS JOIN line_seq
JOIN warehouse_orders order_row
    ON order_row.order_number = 'ORD-' || LPAD(order_seq.order_n, 6, '0')
JOIN items item_row
    ON item_row.sku = 'SKU-' || LPAD(MOD(order_seq.order_n + line_seq.line_n - 2, 100) + 1, 4, '0');

PROMPT Inserting stock_reservations (>=100)...
INSERT INTO stock_reservations (order_line_id, warehouse_id, item_id, quantity, created_at, version)
SELECT
    order_line_row.id,
    order_row.warehouse_id,
    order_line_row.item_id,
    order_line_row.reserved_quantity,
    order_line_row.created_at,
    0
FROM order_lines order_line_row
JOIN warehouse_orders order_row
    ON order_row.id = order_line_row.order_id
WHERE order_line_row.reserved_quantity > 0;

PROMPT Inserting picking_tasks (100)...
INSERT INTO picking_tasks (task_number, status, created_at, version)
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

PROMPT Inserting picking_task_lines (>=100)...
INSERT INTO picking_task_lines (picking_task_id, order_line_id, quantity, picked_quantity, created_at, version)
SELECT
    picking_task_row.id,
    order_line_row.id,
    line_quantity,
    CASE picking_task_row.status
        WHEN 'Completed' THEN line_quantity
        WHEN 'InProgress' THEN ROUND(line_quantity / 2, 3)
        ELSE 0
    END AS picked_quantity,
    order_line_row.created_at,
    0
FROM (
    SELECT
        order_line_row.id,
        order_line_row.order_id,
        CASE
            WHEN order_line_row.reserved_quantity > 0 THEN order_line_row.reserved_quantity
            ELSE order_line_row.quantity
        END AS line_quantity,
        order_line_row.created_at
    FROM order_lines order_line_row
) order_line_row
JOIN warehouse_orders order_row
    ON order_row.id = order_line_row.order_id
JOIN picking_tasks picking_task_row
    ON picking_task_row.task_number = 'PT-' || SUBSTR(order_row.order_number, 5);

PROMPT Inserting audit_logs (>=100)...
INSERT INTO audit_logs (entity_name, entity_id, action, details, created_at, version)
SELECT
    'WarehouseOrder',
    order_row.id,
    'Seeded',
    'Sample order seeded: ' || order_row.order_number,
    order_row.created_at,
    0
FROM warehouse_orders order_row;

INSERT INTO audit_logs (entity_name, entity_id, action, details, created_at, version)
SELECT
    'PickingTask',
    picking_task_row.id,
    'Seeded',
    'Sample picking task seeded: ' || picking_task_row.task_number,
    picking_task_row.created_at,
    0
FROM picking_tasks picking_task_row;

COMMIT;

PROMPT Verifying row counts...
SELECT 'customers' AS table_name, COUNT(*) AS rows_count FROM customers
UNION ALL SELECT 'warehouses', COUNT(*) FROM warehouses
UNION ALL SELECT 'items', COUNT(*) FROM items
UNION ALL SELECT 'stock_balances', COUNT(*) FROM stock_balances
UNION ALL SELECT 'warehouse_orders', COUNT(*) FROM warehouse_orders
UNION ALL SELECT 'order_lines', COUNT(*) FROM order_lines
UNION ALL SELECT 'stock_reservations', COUNT(*) FROM stock_reservations
UNION ALL SELECT 'picking_tasks', COUNT(*) FROM picking_tasks
UNION ALL SELECT 'picking_task_lines', COUNT(*) FROM picking_task_lines
UNION ALL SELECT 'audit_logs', COUNT(*) FROM audit_logs;
