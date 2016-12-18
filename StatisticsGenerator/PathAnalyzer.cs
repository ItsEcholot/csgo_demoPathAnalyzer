using DemoInfo;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StatisticsGenerator
{
    class PathAnalyzer
    {
        static public void AnalyzePaths(string filename, FileStream fileStream)
        {
            long matchID = 0;
            string mapName = "";
            List<ingameRound> allRoundsList = new List<ingameRound>();
            List<ingameRound> matchRoundsList = new List<ingameRound>();
            List<ingameRound> existingStratRoundsList = new List<ingameRound>(); 
            List<ingamePathpoint> allPathpointsList = new List<ingamePathpoint>();

            MySqlConnection sqlConnection = null;
            MySqlDataReader sqlReader = null;


            //Setup connection with the MySQL Server//////////////////////////////////////
            try
            {
                string connectionString = "SERVER=localhost;DATABASE=demostatistics;UID=demostatistics;PASSWORD=1ax4X7M4;";
                sqlConnection = new MySqlConnection(connectionString);
                sqlConnection.Open();
                Console.WriteLine(getTimeStamp() + "Connected to MySQL Server");
            }
            catch (MySqlException er)
            {
                Console.WriteLine(getTimeStamp() + "Error: {0}", er.ToString());
            }
            //////////////////////////////////////////////////////////////////////////////



            //Preloading info for analysis.///////////////////////////////////////////////
            //Get match based on the demo filename
            string matchSelectQuery = "SELECT id,mapName FROM `match` WHERE filename='" + filename + "'";
            MySqlCommand matchSelectionCommand = new MySqlCommand(matchSelectQuery, sqlConnection);
            sqlReader = matchSelectionCommand.ExecuteReader();

            while(sqlReader.Read())
            {
                //For each match
                matchID = sqlReader.GetInt64("id");
                mapName = sqlReader.GetString("mapName");
            }
            sqlReader.Close();
            //If match is not found in database launch PathGenerator
            if (matchID == 0)
            {
                Console.WriteLine(getTimeStamp() + "Match not found in DB... Running PathGenerator...");
                Console.WriteLine(); Console.WriteLine(); Console.WriteLine();

                using (var parser = new DemoParser(fileStream))
                {
                    PathGenerator.GeneratePath(parser, false, false, true, filename);
                }

                sqlReader = matchSelectionCommand.ExecuteReader();
                while (sqlReader.Read())
                {
                    //For each match
                    matchID = sqlReader.GetInt64("id");
                    mapName = sqlReader.GetString("mapName");
                }
                sqlReader.Close();
                //System.Environment.Exit(1);
            }
            Console.WriteLine(getTimeStamp() + filename + " is being analyzed");

            //Prepare statement to get all viable rounds from database
            Console.Write(getTimeStamp() + "Loading all rounds in DB... ");
            string allRoundsSelectionQuery = "SELECT id,roundType,stratFound FROM `round` WHERE bombPlanted=1 OR enemyTeamWiped=1 AND mapName='" + mapName + "'";
            MySqlCommand allRoundsSelectionCommand = new MySqlCommand(allRoundsSelectionQuery, sqlConnection);
            sqlReader = allRoundsSelectionCommand.ExecuteReader();

            //Load each round which meets the query
            while(sqlReader.Read())
            {
                allRoundsList.Add(new ingameRound(sqlReader.GetInt64("id"), sqlReader.GetString("roundType"), sqlReader.GetBoolean("stratFound")));
            }
            sqlReader.Close();
            Console.WriteLine(allRoundsList.Count + " entries - Done");

            //Prepare statement to get viable rounds from match
            Console.Write(getTimeStamp() + "Loading rounds from Demo-File... ");
            string matchRoundSelectionQuery = "SELECT id,roundType,stratFound FROM `round` WHERE matchID="+matchID+" AND bombPlanted=1 OR enemyTeamWiped=1 AND stratFound=0";
            MySqlCommand matchRoundSelectionCommand = new MySqlCommand(matchRoundSelectionQuery, sqlConnection);
            sqlReader = matchRoundSelectionCommand.ExecuteReader();

            while(sqlReader.Read())
            {
                matchRoundsList.Add(new ingameRound(sqlReader.GetInt64("id"), sqlReader.GetString("roundType"), sqlReader.GetBoolean("stratFound")));
            }
            sqlReader.Close();
            Console.WriteLine(matchRoundsList.Count + " entries - Done");

            //Prepare statement to get viable rounds from existing strats
            Console.Write(getTimeStamp() + "Loading rounds from existing strats... ");
            string existingStratsRoundSelectionQuery = "SELECT stratID,roundID,roundType FROM `stratrounds`";
            MySqlCommand existingStratsRoundSelectionCommand = new MySqlCommand(existingStratsRoundSelectionQuery, sqlConnection);
            sqlReader = existingStratsRoundSelectionCommand.ExecuteReader();
            while(sqlReader.Read())
            {
                existingStratRoundsList.Add(new ingameRound(sqlReader.GetInt64("roundID"), sqlReader.GetString("roundType"), true));
                //existingStratRoundsList.Find(n => n.roundID == sqlReader.GetInt64("roundID")).stratID = sqlReader.GetInt64("stratID");

                foreach(var existingRound in existingStratRoundsList.Where(n => n.roundID == sqlReader.GetInt64("roundID")))
                {
                    existingRound.stratID = sqlReader.GetInt64("stratID");
                }
            }
            sqlReader.Close();
            Console.WriteLine(existingStratRoundsList.Count + " entries - Done");

            //Prepare statement to get all viable pathpoints from database
            Console.Write(getTimeStamp() + "Loading all pathpoints in DB... ");
            foreach (var round in allRoundsList)
            {
                string pathpointsSelectionQuery = "SELECT roundID,X,Y,Z FROM `pathpoints` WHERE roundID=" + round.roundID;
                MySqlCommand pathpointsSelectionCommand = new MySqlCommand(pathpointsSelectionQuery, sqlConnection);
                sqlReader = pathpointsSelectionCommand.ExecuteReader();

                while(sqlReader.Read())
                {
                    allPathpointsList.Add(new ingamePathpoint(sqlReader.GetInt64("roundID"), sqlReader.GetInt32("X"), sqlReader.GetInt32("Y"), sqlReader.GetInt32("Z")));
                }
                sqlReader.Close();
            }
            Console.WriteLine(allPathpointsList.Count + " entries - Done");
            /////////////////////////////////////////////////////////////////////////////



            //Analysis Loop - ITS HAPPENING!!!
            Console.WriteLine(); Console.WriteLine();
            Console.WriteLine(getTimeStamp() + "Starting analysis loop");
            foreach (var round in matchRoundsList)
            {
                //Load all pathpoints for this round in the demo file
                List <ingamePathpoint> demoPathpointList = new List<ingamePathpoint>();

                Console.Write(getTimeStamp() + "Loading pathpoints for round " + round.roundID + "... ");
                string demoPathpointsSelectionQuery = "SELECT roundID,X,Y,Z FROM `pathpoints` WHERE roundID=" + round.roundID;
                MySqlCommand demoPathpointsSelectionCommand = new MySqlCommand(demoPathpointsSelectionQuery, sqlConnection);
                sqlReader = demoPathpointsSelectionCommand.ExecuteReader();

                while(sqlReader.Read())
                {
                    demoPathpointList.Add(new ingamePathpoint(sqlReader.GetInt64("roundID"), sqlReader.GetInt32("X"), sqlReader.GetInt32("Y"), sqlReader.GetInt32("Z")));
                }
                sqlReader.Close();
                Console.WriteLine(demoPathpointList.Count + " entries - Done");

                bool matchingRoundInDB = false;
                //List which contains all rounds which are similar to this round from the Demo
                List<ingameRound> demoRoundSimilarRounds = new List<ingameRound>();
                //Add the round from the demo itself to the list
                demoRoundSimilarRounds.Add(round);

                Parallel.ForEach(allRoundsList, dbRound =>
                {
                    int counter = 0;
                    int successCounter = 0;
                    foreach (var demoPathPoint in demoPathpointList)
                    {
                        ingamePathpoint roundedDemoPathPoint = new ingamePathpoint(demoPathPoint.roundID, (((int)Math.Round((int)demoPathPoint.X / 30.0)) * 30), (((int)Math.Round((int)demoPathPoint.Y / 30.0)) * 30), (((int)Math.Round((int)demoPathPoint.Z / 30.0)) * 30));
                        foreach (var dbPathPoint in allPathpointsList.Where(n => n.roundID != demoPathPoint.roundID && n.roundID == dbRound.roundID))
                        {
                            ingamePathpoint roundedDbPathPoint = new ingamePathpoint(dbPathPoint.roundID, (((int)Math.Round((int)dbPathPoint.X / 30.0)) * 30), (((int)Math.Round((int)dbPathPoint.Y / 30.0)) * 30), (((int)Math.Round((int)dbPathPoint.Z / 30.0)) * 30));

                            counter++;
                            double distance = Math.Sqrt(MyPow((roundedDbPathPoint.X - roundedDemoPathPoint.X), 2) + MyPow((roundedDbPathPoint.Y - roundedDemoPathPoint.Y), 2) + MyPow((roundedDbPathPoint.Z - roundedDemoPathPoint.Z), 2));


                            if (distance == 0)
                                successCounter++;
                        }
                    }

                    if (counter != 0)
                    {
                        //Calculate percentage of pathpoints from the DB which are close to the pathpoints from the demo
                        float successPercentage = ((float)successCounter / (float)counter) * 100;
                        if (successPercentage >= 1.7)
                        {
                            //Found a round which seems to be the same strat like the the round from the demo
                            Console.WriteLine("Success round " + dbRound.roundID + ": " + successPercentage.ToString(".0###") + "%");

                            //Add the round from the database which seems similar to the list.
                            demoRoundSimilarRounds.Add(dbRound);
                            matchingRoundInDB = true;
                        }
                    }
                });

                //If a match has been found
                if(matchingRoundInDB)
                {
                    long existingStratID = -1;
                    foreach(var similarRound in demoRoundSimilarRounds.Where(n => n.stratFound==true))
                    {
                        foreach (var stratRound in existingStratRoundsList.Where(n => n.roundID == similarRound.roundID))
                        {
                            Console.WriteLine("The matching round " + similarRound.roundID + " is already in the strat " + stratRound.stratID);
                            existingStratID = stratRound.stratID;
                        }
                    }

                    if(existingStratID != -1)
                    {
                        int newRoundCounter = 0;
                        string addRoundToExistingStratQuery = "INSERT INTO `stratrounds` (stratID,roundID,roundType) VALUES ";
                        foreach (var similarRound in demoRoundSimilarRounds.Where(n => n.stratFound==false))
                        {
                            Console.WriteLine("Adding round " + similarRound.roundID + " to the existing strat " + existingStratID);
                            newRoundCounter++;

                            similarRound.stratFound = true;
                            similarRound.stratID = existingStratID;
                            existingStratRoundsList.Add(similarRound);

                            foreach (var dbRound in allRoundsList.Where(n => n.roundID == similarRound.roundID))
                            {
                                dbRound.stratFound = true;
                                dbRound.stratID = existingStratID;
                            }
                            foreach (var matchRound in matchRoundsList.Where(n => n.roundID == similarRound.roundID))
                            {
                                matchRound.stratFound = true;
                                matchRound.stratID = existingStratID;
                            }

                            //Write stratFound into DB
                            MySqlCommand setStratFoundForRound = new MySqlCommand("UPDATE `round` SET stratFound=1 WHERE id=" + similarRound.roundID, sqlConnection);
                            setStratFoundForRound.ExecuteNonQuery();

                            addRoundToExistingStratQuery += "(" + existingStratID + "," + similarRound.roundID + ",'" + similarRound.roundType + "'),";
                        }

                        if (newRoundCounter >= 1)
                        {
                            addRoundToExistingStratQuery = addRoundToExistingStratQuery.Remove(addRoundToExistingStratQuery.Length - 1);
                            addRoundToExistingStratQuery += ";";
                            MySqlCommand addRoundToExistingStrat = new MySqlCommand(addRoundToExistingStratQuery, sqlConnection);
                            addRoundToExistingStrat.ExecuteNonQuery();
                            Console.WriteLine("Added all rounds to the existing strat " + existingStratID);
                        }
                    }

                    else if(existingStratID == -1)
                    {
                        //Create new strat in DB
                        MySqlCommand createNewStratForRounds = new MySqlCommand("INSERT INTO `strats` (mapName,stratType) VALUES ('" + mapName + "','" + round.roundType + "')", sqlConnection);
                        createNewStratForRounds.ExecuteNonQuery();
                        Console.WriteLine("Created new strat " + createNewStratForRounds.LastInsertedId);

                        string addRoundToNewStratQuery = "INSERT INTO `stratrounds` (stratID,roundID,roundType) VALUES ";
                        string updateRoundsQuery = "UPDATE `round` SET stratFound=1 WHERE";
                        foreach (var similarRound in demoRoundSimilarRounds)
                        {
                            Console.WriteLine("Adding round " + similarRound.roundID + " to the new strat " + createNewStratForRounds.LastInsertedId);
                            similarRound.stratFound = true;
                            similarRound.stratID = createNewStratForRounds.LastInsertedId;
                            existingStratRoundsList.Add(similarRound);

                            foreach (var dbRound in allRoundsList.Where(n => n.roundID == similarRound.roundID))
                            {
                                dbRound.stratFound = true;
                                dbRound.stratID = createNewStratForRounds.LastInsertedId;
                            }
                            foreach(var matchRound in matchRoundsList.Where(n => n.roundID == similarRound.roundID))
                            {
                                matchRound.stratFound = true;
                                matchRound.stratID = createNewStratForRounds.LastInsertedId;
                            } 

                            addRoundToNewStratQuery += "(" + createNewStratForRounds.LastInsertedId + "," + similarRound.roundID + ",'" + similarRound.roundType + "'),";
                            updateRoundsQuery += " id=" + similarRound.roundID + " OR";
                        }
                        addRoundToNewStratQuery = addRoundToNewStratQuery.Remove(addRoundToNewStratQuery.Length - 1);
                        addRoundToNewStratQuery += ";";
                        MySqlCommand insertRoundToStrat = new MySqlCommand(addRoundToNewStratQuery, sqlConnection);
                        insertRoundToStrat.ExecuteNonQuery();

                        updateRoundsQuery = updateRoundsQuery.Remove(updateRoundsQuery.Length - 3);
                        updateRoundsQuery += ";";
                        MySqlCommand updateRounds = new MySqlCommand(updateRoundsQuery, sqlConnection);
                        updateRounds.ExecuteNonQuery();

                        Console.WriteLine("Added all rounds to the new strat " + createNewStratForRounds.LastInsertedId);
                    }
                }
            }
        }


        private static String getTimeStamp()
        {
            String timeStamp = "[" + DateTime.Now.ToString("HH:mm:ss") + "]    ";
            return timeStamp;
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
    }

    public class ingameRound
    {
        public long roundID;
        public string roundType;
        public bool stratFound;
        public long stratID;
        public ingameRound(long roundID, string roundType, bool stratFound)
        {
            this.roundID = roundID;
            this.roundType = roundType;
            this.stratFound = stratFound;
        }
        public void setStratID(long stratID)
        {
            this.stratID = stratID;
        }
    }

    public class ingamePathpoint
    {
        public long roundID;
        public long steamID;
        public int X;
        public int Y;
        public int Z;

        public ingamePathpoint(long roundID, int X, int Y, int Z)
        {
            this.roundID = roundID;
            this.X = X;
            this.Y = Y;
            this.Z = Z;
        }
    }
}
