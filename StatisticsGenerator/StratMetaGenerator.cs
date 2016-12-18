using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StatisticsGenerator
{
    class StratMetaGenerator
    {
        static public void GenerateStratMeta(int analyseStratID)
        {
            Console.WriteLine();
            Console.WriteLine(getTimeStamp() + "---StratMetaGenerator---");
            //Setup connection with the MySQL Server//////////////////////////////////////
            MySqlConnection sqlConnection = null;
            MySqlDataReader sqlReader = null;
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



            //Variables///////////////////////////////////////////////////////////////////
            List<ingameRound> stratRounds = new List<ingameRound>();
            List<ingamePathpoint> stratPathpoints = new List<ingamePathpoint>();
            string mapName = "";
            //////////////////////////////////////////////////////////////////////////////


            //Load mapName from DB
            MySqlCommand getStratInfo = new MySqlCommand("SELECT mapName FROM `strats` WHERE id=" + analyseStratID, sqlConnection);
            sqlReader = getStratInfo.ExecuteReader();
            while(sqlReader.Read())
            {
                mapName = sqlReader.GetString("mapName");
            }
            sqlReader.Close();
            Console.WriteLine(getTimeStamp() + "Loaded info for the strat " + analyseStratID);

            //Load all stratrounds
            MySqlCommand getRoundsFromStrat = new MySqlCommand("SELECT stratID,roundID,roundType FROM `stratrounds` WHERE stratID=" + analyseStratID, sqlConnection);
            sqlReader = getRoundsFromStrat.ExecuteReader();
            while(sqlReader.Read())
            {
                stratRounds.Add(new ingameRound(sqlReader.GetInt64("roundID"),sqlReader.GetString("roundType"),true));
            }
            sqlReader.Close();
            Console.WriteLine(getTimeStamp() + "Loaded " + stratRounds.Count + " rounds for the strat");

            //Load all pathpoints
            foreach (var round in stratRounds) {
                MySqlCommand getPathpointsFromStrat = new MySqlCommand("SELECT steamID,X,Y,Z FROM `pathpoints` WHERE roundID=" + round.roundID, sqlConnection);
                sqlReader = getPathpointsFromStrat.ExecuteReader();
                while(sqlReader.Read())
                {
                    ingamePathpoint pathpoint = new ingamePathpoint(round.roundID, sqlReader.GetInt32("X"), sqlReader.GetInt32("Y"), sqlReader.GetInt32("Z"));
                    stratPathpoints.Add(pathpoint);
                    stratPathpoints[stratPathpoints.IndexOf(pathpoint)].steamID = sqlReader.GetInt64("steamID");
                }
                sqlReader.Close();
            }
            Console.WriteLine(getTimeStamp() + "Loaded " + stratPathpoints.Count + " pathpoints for the strat");


            Parallel.ForEach(stratRounds, (round) =>//Limit to round --> Find way to combine data from different rounds
            {
                Bitmap bitmap = new Bitmap(1024, 1024);
                Graphics graphics = Graphics.FromImage(bitmap);

                Image mapBackground = Image.FromFile("overviews/" + mapName + "_radar.png");
                graphics.DrawImage(mapBackground, 0, 0);


                //List to store the players steamIDs
                List<long> steamIDs = new List<long>();
                steamIDs.Clear();
                foreach (var pathpoint in stratPathpoints.Where(n => !steamIDs.Contains(n.steamID) && n.roundID == round.roundID))
                {
                    steamIDs.Add(pathpoint.steamID);
                }

                //Foreach player get all pathpoints and draw lines between them.
                int playerNumberT = 0;
                foreach (var playerSteamID in steamIDs)
                {
                    Pen pen;
                    playerNumberT++;
                    switch (playerNumberT)
                    {
                        case 1:
                            pen = new Pen(Color.DarkRed);
                            break;
                        case 2:
                            pen = new Pen(Color.DarkGreen);
                            break;
                        case 3:
                            pen = new Pen(Color.Blue);
                            break;
                        case 4:
                            pen = new Pen(Color.Yellow);
                            break;
                        case 5:
                            pen = new Pen(Color.Purple);
                            break;
                        default:
                            pen = new Pen(Color.White);
                            break;
                    }

                    //Generate a list with the pathpoints for this player.
                    List<ingamePathpoint> validPathpoints = new List<ingamePathpoint>();
                    validPathpoints.Clear();
                    foreach (var validPathpoint in stratPathpoints)
                    {
                        if (validPathpoint.steamID == playerSteamID && validPathpoint.roundID == round.roundID)
                            validPathpoints.Add(validPathpoint);
                    }

                    for (int i = 1; i < validPathpoints.Count; i++)
                    {
                        //if(validPathpoints[i] != null && validPathpoints[i-1] != null)
                        graphics.DrawLine(pen, validPathpoints[i-1].X, validPathpoints[i-1].Y, validPathpoints[i].X, validPathpoints[i].Y);
                    }
                }


                graphics.Dispose();
                DirectoryInfo di = Directory.CreateDirectory("renderings/strat" + analyseStratID);
                bitmap.Save("renderings/strat" + analyseStratID + "/" + round.roundID + ".png", ImageFormat.Png);
                bitmap.Dispose();
            });
        }



        private static String getTimeStamp()
        {
            String timeStamp = "[" + DateTime.Now.ToString("HH:mm:ss") + "]    ";
            return timeStamp;
        }
    }
}
