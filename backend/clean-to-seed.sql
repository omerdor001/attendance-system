-- ============================================================
-- Clean database back to seeded state
-- Seeded users:  admin(1), john.doe(2), jane.smith(3), bob.wilson(4)
-- Seeded events: IDs 1-42 (created by DatabaseSeeder.cs)
-- ============================================================

BEGIN TRANSACTION;

-- Step 1: Delete all events added after the initial seed (Id > 42)
DELETE FROM AttendanceDb.dbo.AttendanceEvents WHERE Id > 42;

-- Step 2: Delete non-seeded users (and their events, already gone from step 1)
DELETE FROM AttendanceDb.dbo.Users
WHERE Username NOT IN ('admin', 'john.doe', 'jane.smith', 'bob.wilson');

-- Step 3: Reseed identity counters so next inserts start cleanly after seed range
DBCC CHECKIDENT ('AttendanceDb.dbo.AttendanceEvents', RESEED, 42);
DBCC CHECKIDENT ('AttendanceDb.dbo.Users', RESEED, 4);

-- Verify
SELECT 'Users remaining:' AS Info;
SELECT Id, Username, Role FROM AttendanceDb.dbo.Users ORDER BY Id;

SELECT 'AttendanceEvents remaining (expect 42):' AS Info;
SELECT COUNT(*) AS EventCount FROM AttendanceDb.dbo.AttendanceEvents;

COMMIT TRANSACTION;
