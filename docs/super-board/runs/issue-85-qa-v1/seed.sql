-- QA #85 seed, applied at migration AddStolenBases (before AddInningColumns).
INSERT INTO "Rosters" ("Id","TeamName","Year") VALUES (1,'[TEST] Cubs',2026),(2,'[TEST] Sox',2026);
INSERT INTO "Players" ("Id","RosterId","UniformNumber","FirstName","LastName")
  SELECT i, 1, i::text, 'Cub', 'Batter'||i FROM generate_series(1,9) i;
INSERT INTO "Players" ("Id","RosterId","UniformNumber","FirstName","LastName")
  SELECT 9+i, 2, i::text, 'Sox', 'Hitter'||i FROM generate_series(1,9) i;
INSERT INTO "Games" ("Id","Year","Date","AwayRosterId","HomeRosterId","Innings","Batters")
  VALUES (1,2026,'2026-04-02',1,2,9,9),(2,2026,'2026-04-03',1,2,9,9);
INSERT INTO "Lineups" ("Id","GameId","RosterId") VALUES (1,1,1),(2,1,2),(3,2,1),(4,2,2);
INSERT INTO "LineupSlot" ("LineupId","BattingOrder","PlayerId","Position")
  SELECT l.id, i, CASE WHEN l.r=1 THEN i ELSE 9+i END, i
  FROM (VALUES (1,1),(2,2),(3,1),(4,2)) l(id,r), generate_series(1,9) i;
INSERT INTO "BattingNotes" ("GameId","RosterId","BattingOrder","Inning","Note","BasesReached","Fielders","MultiOut","CaughtStealingBase","CaughtStealingFielders","StolenBases") VALUES
 (1,1,1,1,'K',0,'','',0,'','{}'),
 (1,1,2,1,'GO',0,'4-6-3','DP',0,'','{}'),
 (1,1,3,1,'1B',3,'','',0,'','{2,3}'),
 (1,2,1,1,'BB',1,'','',2,'2-6','{}'),
 (1,2,2,1,'HR',4,'','',0,'','{}'),
 (2,1,1,3,'2B',2,'','',0,'','{}');
INSERT INTO "InningHitsLob" ("GameId","RosterId","Inning","Hits","Lob") VALUES (1,1,1,1,1);
SELECT setval(pg_get_serial_sequence('"Rosters"','Id'),10), setval(pg_get_serial_sequence('"Players"','Id'),100),
       setval(pg_get_serial_sequence('"Games"','Id'),10), setval(pg_get_serial_sequence('"Lineups"','Id'),10);
