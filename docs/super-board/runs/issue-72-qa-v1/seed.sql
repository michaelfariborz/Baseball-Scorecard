-- QA #72 seed: away Cubs, 9 starters (slot 1 CF, slot 6 RF), bench 10 Bench Hitter, 11 Bench Spare; player 10 pinch hits for slot 6 in the 7th.
INSERT INTO "Rosters"("TeamName","Year") VALUES ('Cubs',2026),('Sox',2026);
INSERT INTO "Players"("RosterId","UniformNumber","FirstName","LastName") VALUES
 (1,'1','Cub','Player1'),(1,'2','Cub','Player2'),(1,'3','Cub','Player3'),(1,'4','Cub','Player4'),(1,'5','Cub','Player5'),
 (1,'6','Cub','Player6'),(1,'7','Cub','Player7'),(1,'8','Cub','Player8'),(1,'9','Cub','Player9'),
 (1,'10','Bench','Hitter'),(1,'11','Bench','Spare'),
 (2,'1','Sox','One'),(2,'2','Sox','Two');
INSERT INTO "Games"("Year","Date","AwayRosterId","HomeRosterId","Innings","Batters") VALUES (2026,'2026-04-02',1,2,9,9);
INSERT INTO "Lineups"("GameId","RosterId","StartingPitcherId") VALUES (1,1,NULL);
INSERT INTO "LineupSlot"("LineupId","BattingOrder","PlayerId","Position") VALUES
 (1,1,1,8),(1,2,2,6),(1,3,3,7),(1,4,4,3),(1,5,5,5),(1,6,6,9),(1,7,7,2),(1,8,8,4),(1,9,9,1);
