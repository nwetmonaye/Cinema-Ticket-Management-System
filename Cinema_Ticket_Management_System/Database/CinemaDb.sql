/* =====================================================================
   Cinema Ticket Management System - SQL Server schema
   Server : localhost\SQLEXPRESS   (Windows Authentication)
   Run    : open in SSMS / Azure Data Studio and Execute (F5),
            or: sqlcmd -S localhost\SQLEXPRESS -E -C -i Database\CinemaDb.sql
   The script is safe to re-run: it drops and recreates the tables.
   Creates 2 halls + seats and 8 now-showing movies. The app adds
   showtimes and sample bookings on first start.
   ===================================================================== */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;   -- required for the filtered unique index below
GO

IF DB_ID(N'CinemaTicketDb') IS NULL
    CREATE DATABASE CinemaTicketDb;
GO

USE CinemaTicketDb;
GO

/* Drop in dependency order (children first) */
IF OBJECT_ID(N'dbo.BookingSeats', N'U') IS NOT NULL DROP TABLE dbo.BookingSeats;
IF OBJECT_ID(N'dbo.Bookings',     N'U') IS NOT NULL DROP TABLE dbo.Bookings;
IF OBJECT_ID(N'dbo.Showtimes',    N'U') IS NOT NULL DROP TABLE dbo.Showtimes;
IF OBJECT_ID(N'dbo.Seats',        N'U') IS NOT NULL DROP TABLE dbo.Seats;
IF OBJECT_ID(N'dbo.Halls',        N'U') IS NOT NULL DROP TABLE dbo.Halls;
IF OBJECT_ID(N'dbo.Movies',       N'U') IS NOT NULL DROP TABLE dbo.Movies;
GO

/* ---------- Movies ---------- */
CREATE TABLE dbo.Movies (
    Id              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Movies PRIMARY KEY,
    Title           NVARCHAR(100)     NOT NULL,
    Genre           NVARCHAR(40)      NOT NULL,
    Description     NVARCHAR(600)     NOT NULL CONSTRAINT DF_Movies_Description DEFAULT (N''),
    DurationMinutes INT               NOT NULL,
    PosterUrl       NVARCHAR(500)     NULL,        -- official poster image URL
    ShowingStart    DATE              NOT NULL CONSTRAINT DF_Movies_ShowingStart DEFAULT (CAST(GETDATE() AS date)),               -- first day of the run
    ShowingEnd      DATE              NOT NULL CONSTRAINT DF_Movies_ShowingEnd DEFAULT (DATEADD(day, 28, CAST(GETDATE() AS date))), -- last day; hidden from the movie list after this
    Version         UNIQUEIDENTIFIER  NOT NULL CONSTRAINT DF_Movies_Version DEFAULT (NEWID()),
    CONSTRAINT UQ_Movies_Title      UNIQUE (Title),
    CONSTRAINT CK_Movie_Duration    CHECK (DurationMinutes BETWEEN 30 AND 300),
    CONSTRAINT CK_Movie_ShowingPeriod CHECK (ShowingEnd >= ShowingStart)
);
GO

/* ---------- Halls ---------- */
CREATE TABLE dbo.Halls (
    Id      INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Halls PRIMARY KEY,
    Name    NVARCHAR(50)      NOT NULL,
    Version UNIQUEIDENTIFIER  NOT NULL CONSTRAINT DF_Halls_Version DEFAULT (NEWID())
);
GO

/* ---------- Seats (physical seats in a hall) ---------- */
CREATE TABLE dbo.Seats (
    Id     INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Seats PRIMARY KEY,
    HallId INT               NOT NULL CONSTRAINT FK_Seats_Halls REFERENCES dbo.Halls (Id),
    [Row]  NVARCHAR(1)       NOT NULL,
    Number INT               NOT NULL,
    CONSTRAINT UQ_Seats_Hall_Row_Number UNIQUE (HallId, [Row], Number)
);
GO

/* ---------- Showtimes (a movie scheduled in a hall) ---------- */
CREATE TABLE dbo.Showtimes (
    Id              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Showtimes PRIMARY KEY,
    MovieId         INT               NOT NULL CONSTRAINT FK_Showtimes_Movies REFERENCES dbo.Movies (Id),
    HallId          INT               NOT NULL CONSTRAINT FK_Showtimes_Halls  REFERENCES dbo.Halls (Id),
    StartsAtUtc     DATETIME2         NOT NULL,
    DurationMinutes INT               NOT NULL,
    TicketPriceVnd  BIGINT            NOT NULL,
    Version         UNIQUEIDENTIFIER  NOT NULL CONSTRAINT DF_Showtimes_Version DEFAULT (NEWID()),
    CONSTRAINT UQ_Showtimes_Hall_Start UNIQUE (HallId, StartsAtUtc),
    CONSTRAINT CK_Showtime_Price    CHECK (TicketPriceVnd BETWEEN 1 AND 10000000),
    CONSTRAINT CK_Showtime_Duration CHECK (DurationMinutes BETWEEN 30 AND 300)
);
CREATE INDEX IX_Showtimes_MovieId ON dbo.Showtimes (MovieId);
GO

/* ---------- Bookings (one customer order for one showtime) ---------- */
CREATE TABLE dbo.Bookings (
    Id           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Bookings PRIMARY KEY,
    ShowtimeId   INT               NOT NULL CONSTRAINT FK_Bookings_Showtimes REFERENCES dbo.Showtimes (Id),
    CustomerName NVARCHAR(80)      NOT NULL,
    CreatedAtUtc DATETIME2         NOT NULL CONSTRAINT DF_Bookings_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    Status       NVARCHAR(20)      NOT NULL CONSTRAINT DF_Bookings_Status DEFAULT (N'Confirmed'),
    MovieTitle   NVARCHAR(100)     NOT NULL,   -- snapshot at booking time
    HallName     NVARCHAR(50)      NOT NULL,   -- snapshot at booking time
    StartsAtUtc  DATETIME2         NOT NULL,   -- snapshot at booking time
    Version      UNIQUEIDENTIFIER  NOT NULL CONSTRAINT DF_Bookings_Version DEFAULT (NEWID()),
    CONSTRAINT AK_Bookings_Id_ShowtimeId UNIQUE (Id, ShowtimeId),
    CONSTRAINT CK_Booking_Status CHECK (Status IN (N'Confirmed', N'Cancelled'))
);
CREATE INDEX IX_Bookings_CreatedAtUtc ON dbo.Bookings (CreatedAtUtc);
GO

/* ---------- BookingSeats (ticket lines: one row per seat) ---------- */
CREATE TABLE dbo.BookingSeats (
    Id           INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_BookingSeats PRIMARY KEY,
    BookingId    INT               NOT NULL,
    ShowtimeId   INT               NOT NULL CONSTRAINT FK_BookingSeats_Showtimes REFERENCES dbo.Showtimes (Id),
    SeatId       INT               NOT NULL CONSTRAINT FK_BookingSeats_Seats     REFERENCES dbo.Seats (Id),
    SeatLabel    NVARCHAR(5)       NOT NULL,
    UnitPriceVnd BIGINT            NOT NULL,
    IsActive     BIT               NOT NULL CONSTRAINT DF_BookingSeats_IsActive DEFAULT (1),
    -- composite FK: a ticket line must belong to its booking's showtime
    CONSTRAINT FK_BookingSeats_Bookings FOREIGN KEY (BookingId, ShowtimeId)
        REFERENCES dbo.Bookings (Id, ShowtimeId) ON DELETE CASCADE,
    CONSTRAINT CK_Ticket_Price CHECK (UnitPriceVnd BETWEEN 1 AND 10000000)
);
CREATE INDEX IX_BookingSeats_BookingId_ShowtimeId ON dbo.BookingSeats (BookingId, ShowtimeId);
CREATE INDEX IX_BookingSeats_SeatId ON dbo.BookingSeats (SeatId);
-- Core rule: a seat can have only ONE active ticket per showtime (no double booking)
CREATE UNIQUE INDEX UX_BookingSeats_Showtime_Seat_Active
    ON dbo.BookingSeats (ShowtimeId, SeatId) WHERE IsActive = 1;
GO

/* ---------- Halls and seats ----------
   Hall 1 (IMAX) : rows A-H, 12 seats per row (96 seats)
   Hall 2        : rows A-G, 10 seats per row (70 seats)                 */
INSERT INTO dbo.Halls (Name) VALUES (N'Hall 1 (IMAX)'), (N'Hall 2');

INSERT INTO dbo.Seats (HallId, [Row], Number)
SELECT h.Id, r.[Row], n.Number
FROM dbo.Halls h
CROSS JOIN (VALUES (N'A'),(N'B'),(N'C'),(N'D'),(N'E'),(N'F'),(N'G'),(N'H')) AS r([Row])
CROSS JOIN (VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9),(10),(11),(12)) AS n(Number)
WHERE h.Name = N'Hall 1 (IMAX)';

INSERT INTO dbo.Seats (HallId, [Row], Number)
SELECT h.Id, r.[Row], n.Number
FROM dbo.Halls h
CROSS JOIN (VALUES (N'A'),(N'B'),(N'C'),(N'D'),(N'E'),(N'F'),(N'G')) AS r([Row])
CROSS JOIN (VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9),(10)) AS n(Number)
WHERE h.Name = N'Hall 2';
GO

/* ---------- Now-showing movies (posters: Wikipedia / Wikimedia) ---------- */
INSERT INTO dbo.Movies (Title, Genre, DurationMinutes, PosterUrl, Description) VALUES
(N'Avatar: Fire and Ash', N'Adventure', 197,
 N'https://upload.wikimedia.org/wikipedia/en/9/95/Avatar_Fire_and_Ash_poster.jpeg',
 N'Jake Sully, Neytiri and their family face a new threat on Pandora when the volcanic Ash People rise against the other Na''vi clans.'),
(N'Zootopia 2', N'Animation', 108,
 N'https://upload.wikimedia.org/wikipedia/en/6/6a/Zootopia_2_%282025_film%29.jpg',
 N'Partners Judy Hopps and Nick Wilde follow the trail of a mysterious reptile who turns the city of Zootopia upside down.'),
(N'Wicked: For Good', N'Musical', 137,
 N'https://upload.wikimedia.org/wikipedia/en/b/bf/Wicked_For_Good_poster.jpg',
 N'Elphaba, now branded the Wicked Witch of the West, and Glinda, the celebrated face of Oz, must face what their friendship means as Oz turns against them.'),
(N'Superman', N'Action', 129,
 N'https://upload.wikimedia.org/wikipedia/en/3/32/Superman_%282025_film%29_poster.jpg',
 N'Clark Kent balances his Kryptonian heritage with his Smallville upbringing while facing Lex Luthor and a world unsure of where he belongs.'),
(N'F1', N'Drama', 156,
 N'https://upload.wikimedia.org/wikipedia/en/3/38/F1_%282025_film%29.png',
 N'A veteran driver returns to Formula One to mentor a young hotshot teammate and rescue a struggling team in one last shot at glory.'),
(N'Sinners', N'Horror', 137,
 N'https://upload.wikimedia.org/wikipedia/en/5/5f/Sinners_%282025_film%29_poster.jpg',
 N'Twin brothers return to their Mississippi hometown in 1932 to open a juke joint, only to discover a far darker evil waiting for them.'),
(N'Dune: Part Two', N'Sci-Fi', 166,
 N'https://upload.wikimedia.org/wikipedia/en/5/52/Dune_Part_Two_poster.jpeg',
 N'Paul Atreides unites with Chani and the Fremen on a path of revenge against the conspirators who destroyed his family.'),
(N'Interstellar', N'Sci-Fi', 169,
 N'https://upload.wikimedia.org/wikipedia/en/b/bc/Interstellar_film_poster.jpg',
 N'With Earth''s crops failing, a former pilot leads a team through a wormhole in search of a new home for humanity.');
GO

/* Showing periods, relative to the day this script runs:
   Dune: Part Two has already ended (hidden from the movie list), Interstellar is a coming-soon re-release. */
DECLARE @today DATE = CAST(GETDATE() AS date);
UPDATE m SET ShowingStart = DATEADD(day, r.StartOffset, @today), ShowingEnd = DATEADD(day, r.EndOffset, @today)
FROM dbo.Movies m JOIN (VALUES
    (N'Avatar: Fire and Ash', -14, 42), (N'Zootopia 2', -10, 35), (N'Wicked: For Good', -7, 28),
    (N'Superman', -21, 10), (N'F1', -18, 12), (N'Sinners', -25, 6),
    (N'Dune: Part Two', -40, -1), (N'Interstellar', 5, 30)
) AS r(Title, StartOffset, EndOffset) ON r.Title = m.Title;
CREATE INDEX IX_Movies_ShowingEnd ON dbo.Movies (ShowingEnd);
GO

/* Showtimes and sample bookings are generated by the app on first start
   (Data/SeedData.cs), relative to the current date. */

/* ---------- Quick check ---------- */
SELECT t.name AS TableName, SUM(p.rows) AS [Rows]
FROM sys.tables t JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0, 1)
GROUP BY t.name ORDER BY t.name;
GO
