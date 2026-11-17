/* =====================================================================
   Upgrade an EXISTING CinemaTicketDb: adds each movie's showing period
   (ShowingStart / ShowingEnd). Keeps all movies, showtimes and bookings.
   Run once:  sqlcmd -S localhost\SQLEXPRESS -E -C -I -i Database\Upgrade_002_ShowingPeriod.sql
   (A fresh install with Database\CinemaDb.sql already includes this.)
   ===================================================================== */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
USE CinemaTicketDb;
GO

IF COL_LENGTH(N'dbo.Movies', N'ShowingStart') IS NULL
BEGIN
    ALTER TABLE dbo.Movies ADD
        ShowingStart DATE NOT NULL CONSTRAINT DF_Movies_ShowingStart DEFAULT (CAST(GETDATE() AS date)),
        ShowingEnd   DATE NOT NULL CONSTRAINT DF_Movies_ShowingEnd   DEFAULT (DATEADD(day, 28, CAST(GETDATE() AS date)));
END
GO

/* Fit each movie's run around the showtimes it already has, so no existing show falls outside it:
   start = day of its first showing (or today), end = day of its last showing, at least 4 weeks from today.
   Showtimes are stored in UTC; the cinema is in Asia/Ho_Chi_Minh (UTC+7, no daylight saving). */
DECLARE @today DATE = CAST(GETDATE() AS date);
UPDATE m SET
    ShowingStart = COALESCE(s.FirstDay, @today),
    ShowingEnd   = CASE WHEN s.LastDay > DATEADD(day, 28, @today) THEN s.LastDay ELSE DATEADD(day, 28, @today) END
FROM dbo.Movies m
OUTER APPLY (SELECT MIN(CAST(DATEADD(hour, 7, StartsAtUtc) AS date)) AS FirstDay,
                    MAX(CAST(DATEADD(hour, 7, StartsAtUtc) AS date)) AS LastDay
             FROM dbo.Showtimes WHERE MovieId = m.Id) s;
GO

IF OBJECT_ID(N'dbo.CK_Movie_ShowingPeriod', N'C') IS NULL
    ALTER TABLE dbo.Movies ADD CONSTRAINT CK_Movie_ShowingPeriod CHECK (ShowingEnd >= ShowingStart);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Movies_ShowingEnd' AND object_id = OBJECT_ID(N'dbo.Movies'))
    CREATE INDEX IX_Movies_ShowingEnd ON dbo.Movies (ShowingEnd);
GO

SELECT Title, ShowingStart, ShowingEnd FROM dbo.Movies ORDER BY ShowingStart, Title;
GO
