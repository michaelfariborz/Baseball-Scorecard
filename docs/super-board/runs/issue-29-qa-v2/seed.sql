INSERT INTO "Rosters"("TeamName","Year") VALUES ('Cubs',2026),('Sox',2026);
INSERT INTO "Players"("RosterId","UniformNumber","FirstName","LastName") VALUES
 (1,'8','Ian','Happ'),(1,'7','Dansby','Swanson'),(1,'4','Kyle','Tucker'),(1,'24','Seiya','Suzuki'),(1,'27','Michael','Busch'),
 (1,'6','Pete','Crow'),(1,'9','Matt','Shaw'),(1,'5','Nico','Hoerner'),(1,'34','Carson','Kelly'),
 (2,'1','Sox','One'),(2,'2','Sox','Two');
INSERT INTO "Games"("Year","Date","AwayRosterId","HomeRosterId","Innings","Batters") VALUES (2026,'2026-04-02',1,2,9,9);
INSERT INTO "Lineups"("GameId","RosterId","StartingPitcherId") VALUES (1,1,NULL);
INSERT INTO "LineupSlot"("LineupId","BattingOrder","PlayerId","Position") VALUES
 (1,1,1,7),(1,2,2,6),(1,3,3,9),(1,4,4,10),(1,5,5,3),(1,6,6,8),(1,7,7,5),(1,8,8,4),(1,9,9,2);
