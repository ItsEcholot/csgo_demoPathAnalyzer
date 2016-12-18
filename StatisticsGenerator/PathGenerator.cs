using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DemoInfo;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using Newtonsoft.Json;
using MySql.Data;
using MySql.Data.MySqlClient;

namespace StatisticsGenerator
{
    public static class PathGenerator
    {
        public static void GeneratePath(DemoParser parser, bool generatePNGs, bool generateCSV, bool analyzePath, String fileName)
        {
            Console.WriteLine();
            Console.WriteLine(getTimeStamp() + "---PathGenerator---");

            //Variables////////////////////////////////////////////////////////////////////////////////////
            bool matchAlreadyAnalyzed = false;

            String ctTeamName = "", tTeamName = "";
            int counter = 0;
            int resolutionDivider = 1;
            float minimapScale = 0;
            bool matchStarted = false;

            float maxX = 0, maxY = 0, minX = 0, minY = 0;

            Dictionary<long, IngamePlayer> ingamePlayerList = new Dictionary<long, IngamePlayer>();
            Random randonGen = new Random();

            long matchDBID = 0;

            Vector bombPlantPos = new Vector(0, 0, 0);
            bool bombPlantedInThisRound = false;

            var dbCon = new DBConnection();

            int ctScoreDB = 0;
            int tScoreDB = 0;
            ///////////////////////////////////////////////////////////////////////////////////////////////


            parser.ParseHeader();

            Console.WriteLine(getTimeStamp() + "Playing on " + parser.Map);

            //Get JSON with HLTV overview description of the map.
            string jsonContent = System.IO.File.ReadAllText("overviews/" + parser.Map + ".txt");
            var hltvOverviewDescription = JsonConvert.DeserializeObject<dynamic>(jsonContent);

            //Set all the variables using the data from the JSON
            minimapScale = hltvOverviewDescription.scale;
            minX = hltvOverviewDescription.minPos_x;
            maxX = hltvOverviewDescription.maxPos_x;
            minY = hltvOverviewDescription.minPos_y;
            maxY = hltvOverviewDescription.maxPos_y;

            Console.WriteLine(getTimeStamp() + "Playing using " + parser.TickRate + " tickrate");

            //Set the resolution of the analysis based on the TickRate
            if (parser.TickRate < 30)
                resolutionDivider = 2;
            else if (parser.TickRate > 30 && parser.TickRate < 60)
                resolutionDivider = 4;
            else if (parser.TickRate > 60)
                resolutionDivider = 8;

            // And now, generate the filename of the resulting file
            string outputFileName = "csv/" + fileName + "-paths.csv";
            // and open it. 
            var outputStream = new StreamWriter(outputFileName);
            if (generateCSV)
            {
                //And write a header so you know what is what in the resulting file
                outputStream.WriteLine(GenerateCSVHeader());
            }

            //Check if the file has already been analyzed
            if (analyzePath)
                if (dbCon.SelectMatch("SELECT * FROM `match` WHERE filename='" + fileName + "' LIMIT 1;")[0].Count != 0)
                {
                    matchAlreadyAnalyzed = true;
                    Console.WriteLine(getTimeStamp() + "Match already analyzed... Skipping Database Insertion.");
                }
            
            parser.MatchStarted += (sender, e) =>
            {
                matchStarted = true;
                //Check if the team names have already been parsed and saved them in vars.
                if (ctTeamName == "" && tTeamName == "" && parser.CTClanName != "" && parser.TClanName != "")
                {
                    ctTeamName = parser.CTClanName;
                    tTeamName = parser.TClanName;
                    Console.WriteLine(getTimeStamp() + ctTeamName + " vs " + tTeamName);

                    if (analyzePath && !matchAlreadyAnalyzed)
                    {
                        //Write match info into the database
                        string matchInsertQuery = "INSERT INTO `match` (filename, team1, team2, score1, score2, mapName) VALUES ('" + fileName + "', '" + parser.CTClanName + "', '" + parser.TClanName + "', " + parser.CTScore + ", " + parser.TScore + ", '"+parser.Map+"');";
                        matchDBID = dbCon.Insert(matchInsertQuery);
                    }
                }
            };

            parser.RoundStart += (sender, e) =>
            {
                bombPlantedInThisRound = false;
                foreach (var player in parser.PlayingParticipants)
                {
                    ingamePlayerList[player.SteamID] = new IngamePlayer(player.SteamID, player.Team);
                }
            };

            parser.PlayerKilled += (sender, e) =>
            {
                if (ingamePlayerList.ContainsKey(e.Victim.SteamID))
                {
                    if (!bombPlantedInThisRound)
                        ingamePlayerList[e.Victim.SteamID].playerDeadBeforePlant = true;

                    ingamePlayerList[e.Victim.SteamID].playerDead = true;

                    int x = (int)(Remap(e.Victim.Position.X, minX, maxX, 0, (float)hltvOverviewDescription.minimapRes));
                    int y = (int)(Remap(e.Victim.Position.Y, minY, maxY, 0, (float)hltvOverviewDescription.minimapRes));
                    ingamePlayerList[e.Victim.SteamID].deathPos = new Vector(x, y, e.Victim.Position.Z);
                }
            };

            parser.NadeReachedTarget += (sender, e) =>
            {
                if (e.ThrownBy != null && ingamePlayerList.ContainsKey(e.ThrownBy.SteamID))
                {
                    int x = (int)(Remap(e.Position.X, minX, maxX, 0, (float)hltvOverviewDescription.minimapRes));
                    int y = (int)(Remap(e.Position.Y, minY, maxY, 0, (float)hltvOverviewDescription.minimapRes));

                    if (!bombPlantedInThisRound)
                        ingamePlayerList[e.ThrownBy.SteamID].thrownGrenades.Add(new IngameGrenade(new Vector(x, y, e.Position.Z), e.NadeType, true));
                    else
                        ingamePlayerList[e.ThrownBy.SteamID].thrownGrenades.Add(new IngameGrenade(new Vector(x, y, e.Position.Z), e.NadeType, false));
                }
            };

            parser.BombPlanted += (sender, e) =>
            {
                bombPlantedInThisRound = true;

                int x = (int)(Remap(e.Player.Position.X, minX, maxX, 0, (float)hltvOverviewDescription.minimapRes));
                int y = (int)(Remap(e.Player.Position.Y, minY, maxY, 0, (float)hltvOverviewDescription.minimapRes));
                bombPlantPos = new Vector(x, y, e.Player.Position.Z);
            };

            //At the end of every round draw the strategic maps
            parser.RoundEnd += (sender, e) =>
            {
                int playersOnEcoT = 0;
                int playersDeadOnCT = 0;
                String roundType = "";
                bool enemyTeamWiped = false;

                //Increase counter for each player that is on an eco
                foreach (var player in parser.PlayingParticipants.Where(n => n.Team.Equals(Team.Terrorist)))
                {
                    if (player.FreezetimeEndEquipmentValue <= 1750)
                        playersOnEcoT++;
                }
                if (playersOnEcoT == 5)
                    roundType = "Eco";
                else if (playersOnEcoT > 0 && playersOnEcoT < 5)
                    roundType = "Force";
                else if (playersOnEcoT == 0)
                    roundType = "Full";

                //Increase counter for each player that is on T and dead
                foreach (var player in parser.PlayingParticipants.Where(n => n.Team.Equals(Team.CounterTerrorist)))
                {
                    if (!player.IsAlive)
                        playersDeadOnCT++;
                }
                if (playersDeadOnCT == 5)
                    enemyTeamWiped = true;
                if (analyzePath && !matchAlreadyAnalyzed && matchDBID != 0 && ingamePlayerList.Count != 0)
                {
                    long roundDBID = 0;


                    //Write round info into the database
                    string roundInsertQuery = "INSERT INTO `round` (matchID, roundType, bombPlanted, enemyTeamWiped, mapName) VALUES (" + matchDBID + ", '" + roundType + "', " + bombPlantedInThisRound + ", " + enemyTeamWiped + ", '" + parser.Map + "');";
                    roundDBID = dbCon.Insert(roundInsertQuery);

                    //Write player steps into the databse
                    foreach (var ingamePlayer in ingamePlayerList.Where(n => n.Value.playerTeam.Equals(Team.Terrorist)))
                    {
                        string playerStepsInsertQuery = "INSERT INTO `pathpoints` (roundID, steamID, X, Y, Z) VALUES ";

                        int increaseBy = (ingamePlayer.Value.vectorList.Count / 100);
                        Vector lastPlayerStep = new Vector(0,0,0);

                        if (increaseBy != 0)
                        {
                            for (int i = 0; i < ingamePlayer.Value.vectorList.Count; i += increaseBy)
                            {
                                //Calculate distance between last inserted step and this step
                                //double distance = Math.Sqrt(MyPow((lastPlayerStep.X - (int)ingamePlayer.Value.vectorList[i].X), 2) + MyPow((lastPlayerStep.Y - (int)ingamePlayer.Value.vectorList[i].Y), 2) + MyPow((lastPlayerStep.Z - (int)ingamePlayer.Value.vectorList[i].Z), 2));

                                int roundedX = ((int)Math.Round((int)ingamePlayer.Value.vectorList[i].X / 30.0)) * 30;
                                int roundedY = ((int)Math.Round((int)ingamePlayer.Value.vectorList[i].Y / 30.0)) * 30;
                                int roundedZ = ((int)Math.Round((int)ingamePlayer.Value.vectorList[i].Z / 30.0)) * 30;

                                //if (distance >= 20)
                                if(roundedX != lastPlayerStep.X && roundedY != lastPlayerStep.Y && roundedZ != lastPlayerStep.Z)
                                {
                                    playerStepsInsertQuery += "(" + roundDBID + ", " + ingamePlayer.Value.steamID + ", " + (int)ingamePlayer.Value.vectorList[i].X + ", " + (int)ingamePlayer.Value.vectorList[i].Y + ", " + (int)ingamePlayer.Value.vectorList[i].Z + "),";
                                    //playerStepsInsertQuery += "(" + roundDBID + ", " + ingamePlayer.Value.steamID + ", " + roundedX + ", " + roundedY + ", " + roundedZ + "),";

                                    lastPlayerStep.X = roundedX;//(int)ingamePlayer.Value.vectorList[i].X;
                                    lastPlayerStep.Y = roundedY;//(int)ingamePlayer.Value.vectorList[i].Y;
                                    lastPlayerStep.Z = roundedZ;//(int)ingamePlayer.Value.vectorList[i].Z;
                                }
                            }
                            //Remove last komma
                            playerStepsInsertQuery = playerStepsInsertQuery.Substring(0, playerStepsInsertQuery.Length - 1);
                            //Add semicolon
                            playerStepsInsertQuery += ";";

                            dbCon.Insert(playerStepsInsertQuery);
                        }
                    }
                }
                if (analyzePath && !matchAlreadyAnalyzed)
                {
                    dbCon.Insert("UPDATE `match` SET score1=" + parser.CTScore + ", score2=" + parser.TScore + " WHERE id=" + matchDBID);
                    ctScoreDB = parser.CTScore;
                    tScoreDB = parser.TScore;
                }



                if (generatePNGs)
                {
                    //Begin drawing
                    Bitmap bitmap = new Bitmap(1024, 1024);
                    Graphics graphics = Graphics.FromImage(bitmap);
                    if (ingamePlayerList.Count != 0)
                    {
                        //Draw Map background
                        Image mapBackground = Image.FromFile("overviews/" + parser.Map + "_radar.png");
                        graphics.DrawImage(mapBackground, 0, 0);
                        //Prepare minimap Icons
                        Image deathIcon = Image.FromFile("mapIcons/death.png");
                        Image smokeIcon = Image.FromFile("mapIcons/smoke.png");
                        Image fireIcon = Image.FromFile("mapIcons/fire.png");
                        Image bombIcon = Image.FromFile("mapIcons/bomb.png");

                        int playerNumberT = 0, playerNumberCT = 0;
                        int playersOnEcoCT = 0;
                        foreach (var ingamePlayer in ingamePlayerList.Where(n => n.Value.playerTeam.Equals(Team.Terrorist)))
                        {
                            //Generate a random color for the player
                            SolidBrush brush;
                            playerNumberT++;
                            switch (playerNumberT)
                            {
                                case 1:
                                    brush = new SolidBrush(Color.DarkRed);
                                    deathIcon = Image.FromFile("mapIcons/deathDarkRed.png");
                                    break;
                                case 2:
                                    brush = new SolidBrush(Color.DarkGreen);
                                    deathIcon = Image.FromFile("mapIcons/deathDarkGreen.png");
                                    break;
                                case 3:
                                    brush = new SolidBrush(Color.Blue);
                                    deathIcon = Image.FromFile("mapIcons/deathBlue.png");
                                    break;
                                case 4:
                                    brush = new SolidBrush(Color.Yellow);
                                    deathIcon = Image.FromFile("mapIcons/deathYellow.png");
                                    break;
                                case 5:
                                    brush = new SolidBrush(Color.Purple);
                                    deathIcon = Image.FromFile("mapIcons/deathPurple.png");
                                    break;
                                default:
                                    brush = new SolidBrush(Color.FromArgb(255, randonGen.Next(75, 255), randonGen.Next(75, 255), randonGen.Next(75, 255)));
                                    deathIcon = Image.FromFile("mapIcons/death.png");
                                    break;
                            }

                            //Draw a dot for each step
                            foreach (var step in ingamePlayer.Value.vectorList)
                            {
                                graphics.FillEllipse(brush, new Rectangle((int)step.X, (int)step.Y, 5, 5));
                            }
                            //Draw thrown smokes
                            foreach (var grenade in ingamePlayer.Value.thrownGrenades.Where(n => n.grenadeType.Equals(EquipmentElement.Smoke)))
                            {
                                if (grenade.thrownBeforePlant)
                                {
                                    //Draw smokes twice to make them less transparence.
                                    graphics.DrawImage(smokeIcon, new Rectangle((int)grenade.destinationPos.X - 16, (int)grenade.destinationPos.Y - 16, 32, 32));
                                    graphics.DrawImage(smokeIcon, new Rectangle((int)grenade.destinationPos.X - 16, (int)grenade.destinationPos.Y - 16, 32, 32));
                                }
                            }
                            //Draw thrown molotoves / flames
                            foreach (var grenade in ingamePlayer.Value.thrownGrenades.Where(n => n.grenadeType.Equals(EquipmentElement.Molotov)))
                            {
                                if (grenade.thrownBeforePlant)
                                    graphics.DrawImage(fireIcon, new Rectangle((int)grenade.destinationPos.X - 16, (int)grenade.destinationPos.Y - 16, 32, 32));
                            }
                            //Draw skulls where a player has died
                            if (ingamePlayer.Value.playerDead && ingamePlayer.Value.playerDeadBeforePlant)
                            {
                                graphics.DrawImage(deathIcon, (int)ingamePlayer.Value.deathPos.X - 12, (int)ingamePlayer.Value.deathPos.Y - 12, 24, 24);
                            }
                        }
                        //Draw the bomb if it's been planted
                        if (bombPlantedInThisRound)
                        {
                            graphics.DrawImage(bombIcon, (int)bombPlantPos.X - 20, (int)bombPlantPos.Y - 20, 40, 40);
                        }

                        //Increase counter for each player that is on an eco
                        foreach (var player in parser.PlayingParticipants.Where(n => n.Team.Equals(Team.Terrorist)))
                        {
                            if (player.FreezetimeEndEquipmentValue <= 1500)
                                playersOnEcoT++;
                        }
                        //Write onto the picture what kind of buy the team is using
                        if (playersOnEcoT == 5)
                            graphics.DrawString("Eco", new Font("Tahoma", 20), Brushes.White, new Rectangle(0, 0, 500, 200));
                        else if (playersOnEcoT > 0 && playersOnEcoT < 5)
                            graphics.DrawString("Force Buy / Part Buy", new Font("Tahoma", 20), Brushes.White, new Rectangle(0, 0, 500, 200));
                        else if (playersOnEcoT == 0)
                            graphics.DrawString("Full Buy", new Font("Tahoma", 20), Brushes.White, new Rectangle(0, 0, 500, 200));

                        graphics.Dispose();
                        int rounds = parser.CTScore + parser.TScore;
                        bitmap.Save(fileName + "_" + rounds + "_T.png", ImageFormat.Png);
                        bitmap.Dispose();


                        //CT-Side/////////////////////////////////////////////////////////////////////////////////////////////////////////

                        bitmap = new Bitmap(1024, 1024);
                        graphics = Graphics.FromImage(bitmap);
                        graphics.DrawImage(mapBackground, 0, 0);
                        foreach (var ingamePlayer in ingamePlayerList.Where(n => n.Value.playerTeam.Equals(Team.CounterTerrorist)))
                        {
                            //Generate a random color for the player
                            SolidBrush brush;
                            playerNumberCT++;
                            switch (playerNumberCT)
                            {
                                case 1:
                                    brush = new SolidBrush(Color.DarkRed);
                                    deathIcon = Image.FromFile("mapIcons/deathDarkRed.png");
                                    break;
                                case 2:
                                    brush = new SolidBrush(Color.DarkGreen);
                                    deathIcon = Image.FromFile("mapIcons/deathDarkGreen.png");
                                    break;
                                case 3:
                                    brush = new SolidBrush(Color.Blue);
                                    deathIcon = Image.FromFile("mapIcons/deathBlue.png");
                                    break;
                                case 4:
                                    brush = new SolidBrush(Color.Yellow);
                                    deathIcon = Image.FromFile("mapIcons/deathYellow.png");
                                    break;
                                case 5:
                                    brush = new SolidBrush(Color.Purple);
                                    deathIcon = Image.FromFile("mapIcons/deathPurple.png");
                                    break;
                                default:
                                    brush = new SolidBrush(Color.FromArgb(255, randonGen.Next(75, 255), randonGen.Next(75, 255), randonGen.Next(75, 255)));
                                    deathIcon = Image.FromFile("mapIcons/death.png");
                                    break;
                            }


                            //Draw a dot for each step
                            foreach (var step in ingamePlayer.Value.vectorList)
                            {
                                graphics.FillEllipse(brush, new Rectangle((int)step.X, (int)step.Y, 5, 5));
                            }
                            //Draw thrown smokes
                            foreach (var grenade in ingamePlayer.Value.thrownGrenades.Where(n => n.grenadeType.Equals(EquipmentElement.Smoke)))
                            {
                                //Draw smokes twice to make them less transparence.
                                graphics.DrawImage(smokeIcon, new Rectangle((int)grenade.destinationPos.X - 16, (int)grenade.destinationPos.Y - 16, 32, 32));
                                graphics.DrawImage(smokeIcon, new Rectangle((int)grenade.destinationPos.X - 16, (int)grenade.destinationPos.Y - 16, 32, 32));
                            }
                            //Draw thrown molotoves / flames
                            foreach (var grenade in ingamePlayer.Value.thrownGrenades.Where(n => n.grenadeType.Equals(EquipmentElement.Incendiary)))
                            {
                                graphics.DrawImage(fireIcon, new Rectangle((int)grenade.destinationPos.X - 16, (int)grenade.destinationPos.Y - 16, 32, 32));
                            }
                            //Draw skulls where a player has died
                            if (ingamePlayer.Value.playerDead)
                            {
                                graphics.DrawImage(deathIcon, (int)ingamePlayer.Value.deathPos.X - 12, (int)ingamePlayer.Value.deathPos.Y - 12, 24, 24);
                            }
                        }

                        //Increase counter for each player that is on an eco
                        foreach (var player in parser.PlayingParticipants.Where(n => n.Team.Equals(Team.CounterTerrorist)))
                        {
                            if (player.FreezetimeEndEquipmentValue <= 1750)
                                playersOnEcoCT++;
                        }
                        //Write onto the picture what kind of buy the team is using
                        if (playersOnEcoCT == 5)
                            graphics.DrawString("Eco", new Font("Tahoma", 20), Brushes.White, new Rectangle(0, 0, 500, 200));
                        else if (playersOnEcoCT > 0 && playersOnEcoCT < 5)
                            graphics.DrawString("Force Buy / Part Buy", new Font("Tahoma", 20), Brushes.White, new Rectangle(0, 0, 500, 200));
                        else if (playersOnEcoCT == 0)
                            graphics.DrawString("Full Buy", new Font("Tahoma", 20), Brushes.White, new Rectangle(0, 0, 500, 200));

                        graphics.Dispose();
                        rounds = parser.CTScore + parser.TScore;
                        bitmap.Save(fileName + "_" + rounds + "_CT.png", ImageFormat.Png);
                        bitmap.Dispose();

                        //System.Environment.Exit(1);
                    }
                }
            };

            parser.TickDone += (sender, e) =>
            {
                if (counter % resolutionDivider == 0)
                {
                    if (matchStarted)
                    {
                        foreach (var player in parser.PlayingParticipants)
                        {
                            if (player.IsAlive)
                            {
                                ingamePlayerList[player.SteamID].playerDead = false;
                                if (generateCSV)
                                    writePlayerPosToFile(outputStream, parser.CurrentTick, parser.CTScore + parser.TScore, player.Team, player.Name, player.SteamID, player.IsAlive, player.Position);

                                if (ingamePlayerList.ContainsKey(player.SteamID) && !bombPlantedInThisRound)
                                {
                                    int x = (int)(Remap(player.Position.X, minX, maxX, 0, (float)hltvOverviewDescription.minimapRes));
                                    int y = (int)(Remap(player.Position.Y, minY, maxY, 0, (float)hltvOverviewDescription.minimapRes));
                                    ingamePlayerList[player.SteamID].vectorList.Add(new Vector(x, y, player.Position.Z));
                                }
                            }
                        }
                    }
                }
                //Count the number of ticks
                counter++;
            };

            //Iterate trough every tick in the demo file
            while (parser.ParseNextTick())
            {
                
            }
            if(ctScoreDB > tScoreDB)
                dbCon.Insert("UPDATE `match` SET score1=" + (ctScoreDB+1) + ", score2=" + tScoreDB + " WHERE id=" + matchDBID);
            else if(tScoreDB > ctScoreDB)
                dbCon.Insert("UPDATE `match` SET score1=" + ctScoreDB + ", score2=" + (tScoreDB+1) + " WHERE id=" + matchDBID);

            Console.WriteLine(getTimeStamp() + "Parsed " + counter/resolutionDivider + " ticks of the original " + counter);
        }



        public class IngamePlayer
        {
            public long steamID;
            public Team playerTeam;

            public List<Vector> vectorList = new List<Vector>();
            public Vector deathPos = new Vector(0, 0, 0);
            public bool playerDead = false;
            public bool playerDeadBeforePlant = false;

            public List<IngameGrenade> thrownGrenades = new List<IngameGrenade>();

            public IngamePlayer(long steamID, Team playerTeam)
            {
                this.steamID = steamID;
                this.playerTeam = playerTeam;
            }
        }

        public class IngameGrenade
        {
            public Vector destinationPos = new Vector(0, 0, 0);
            public EquipmentElement grenadeType;

            public bool thrownBeforePlant = false;

            public IngameGrenade(Vector destinationPos, EquipmentElement grenadeType, bool thrownBeforePlant)
            {
                this.destinationPos = destinationPos;
                this.grenadeType = grenadeType;
                this.thrownBeforePlant = thrownBeforePlant;
            }
        }

        private static float Remap(this float value, float fromSource, float toSource, float fromTarget, float toTarget)
        {
            return (value - fromSource) / (toSource - fromSource) * (toTarget - fromTarget) + fromTarget;
        }

        static double MyPow(double num, int exp)
        {
            double result = 1.0;

            if (exp < 0)
                return (1 / MyPow(num, Math.Abs(exp)));

            while (exp > 0)
            {
                if (exp % 2 == 1)
                    result *= num;
                exp >>= 1;
                num *= num;
            }

            return result;
        }

        private static String getTimeStamp()
        {
            String timeStamp = "[" + DateTime.Now.ToString("HH:mm:ss") + "]    ";
            return timeStamp;
        }

        private static void writePlayerPosToFile(StreamWriter outputStream, int tick, int round, Team playerTeam, String playerName, long steamID, Boolean isAlive, Vector playerPos)
        {
            String playerTeamString = "";

            if (playerTeam.Equals(DemoInfo.Team.CounterTerrorist))
                playerTeamString = "CT";
            else if (playerTeam.Equals(DemoInfo.Team.Terrorist))
                playerTeamString = "T";

            outputStream.WriteLine(string.Format(
                "{0};{1};{2};{3};{4};{5};{6};{7};{8};",
                tick,
                round,
                playerTeamString,
                playerName,
                steamID,
                isAlive,
                playerPos.X,
                playerPos.Y,
                playerPos.Z
            ));
        }

        private static string GenerateCSVHeader()
        {
            return string.Format(
                "{0};{1};{2};{3};{4};{5};{6};{7};{8};",
                "Tick",
                "Round",
                "Player-Team",
                "Player-Nickname",
                "Player-SteamID",
                "IsAlive",
                "PosX",
                "PosY",
                "PosZ"
            );
        }
    }
}
