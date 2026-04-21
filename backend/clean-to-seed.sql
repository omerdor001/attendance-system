-- ============================================================
-- Clean database back to seeded state
-- Removes any users/events not defined in DatabaseSeeder.cs
-- Seeded usernames: admin, john.doe, jane.smith, bob.wilson
-- ============================================================

BEGIN TRANSACTION;

-- Step 1: Delete AttendanceEvents that belong to non-seeded users
DELETE FROM AttendanceEvents
WHERE UserId IN (
    SELECT Id FROM Users
    WHERE Username NOT IN ('admin', 'john.doe', 'jane.smith', 'bob.wilson')
);

-- Step 2: Delete the non-seeded users themselves
DELETE FROM Users
WHERE Username NOT IN ('admin', 'john.doe', 'jane.smith', 'bob.wilson');

-- Verify what remains
SELECT 'Users remaining:' AS Info;
SELECT Id, Username, Role FROM Users ORDER BY Id;

SELECT 'AttendanceEvents remaining:' AS Info;
SELECT COUNT(*) AS EventCount FROM AttendanceEvents;

COMMIT TRANSACTION;
