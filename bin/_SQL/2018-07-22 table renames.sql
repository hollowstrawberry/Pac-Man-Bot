CREATE TABLE PacManScores (Score INT, UserId BIGINT, State INT, Turns INT, Username TEXT, Channel TEXT, Date DATETIME);
INSERT INTO PacManScores (Score, UserId, State, Turns, Username, Channel, Date)
   SELECT score, userid, state, turns, username, channel, Date FROM scoreboard;
DROP TABLE scoreboard;

CREATE TABLE Prefixes2 (Id BIGINT PRIMARY KEY, Prefix TEXT);
INSERT INTO Prefixes2 (Id, Prefix)
    SELECT id, prefix FROM prefixes;
DROP TABLE prefixes;
ALTER TABLE Prefixes2 RENAME TO Prefixes;

CREATE TABLE NoPrefixChannels (Id BIGINT PRIMARY KEY);
INSERT INTO NoPrefixChannels (id)
    SELECT id FROM noprefix;
DROP TABLE noprefix;

CREATE TABLE NoAutoresponseGuilds (Id BIGINT PRIMARY KEY);
INSERT INTO NoAutoresponseGuilds (id)
    SELECT id FROM noautoresponse;
DROP TABLE noautoresponse;