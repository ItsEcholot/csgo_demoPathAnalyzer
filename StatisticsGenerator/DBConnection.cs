using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

class DBConnection
{
    private MySqlConnection connection;
    private string server;
    private string database;
    private string uid;
    private string password;

    //Constructor
    public DBConnection()
    {
        Initialize();
    }

    //Initialize values
    private void Initialize()
    {
        server = "localhost";
        database = "demostatistics";
        uid = "demostatistics";
        password = "1ax4X7M4";
        string connectionString;
        connectionString = "SERVER=" + server + ";" + "DATABASE=" +
        database + ";" + "UID=" + uid + ";" + "PASSWORD=" + password + ";";

        connection = new MySqlConnection(connectionString);
    }

    //open connection to database
    private bool OpenConnection()
    {
        try
        {
            connection.Open();
            return true;
        }
        catch (MySqlException ex)
        {
            //When handling errors, you can your application's response based 
            //on the error number.
            //The two most common error numbers when connecting are as follows:
            //0: Cannot connect to server.
            //1045: Invalid user name and/or password.
            switch (ex.Number)
            {
                case 0:
                    Console.WriteLine("Cannot connect to server.  Contact administrator");
                    break;

                case 1045:
                    Console.WriteLine("Invalid username/password, please try again");
                    break;
            }
            return false;
        }
    }

    //Close connection
    private bool CloseConnection()
    {
        try
        {
            connection.Close();
            return true;
        }
        catch (MySqlException ex)
        {
            Console.WriteLine(ex.Message);
            return false;
        }
    }

    //Insert statement
    public long Insert(string query)
    {
        long insertID = 0;

        //open connection
        if (this.OpenConnection() == true)
        {
            //create command and assign the query and connection from the constructor
            MySqlCommand cmd = new MySqlCommand(query, connection);

            //Execute command
            cmd.ExecuteNonQuery();
            insertID = cmd.LastInsertedId;

            //close connection
            this.CloseConnection();
        }
        return insertID;
    }

    //Update statement
    public void Update()
    {
        string query = "UPDATE tableinfo SET name='Joe', age='22' WHERE name='John Smith'";

        //Open connection
        if (this.OpenConnection() == true)
        {
            //create mysql command
            MySqlCommand cmd = new MySqlCommand();
            //Assign the query using CommandText
            cmd.CommandText = query;
            //Assign the connection using Connection
            cmd.Connection = connection;

            //Execute query
            cmd.ExecuteNonQuery();

            //close connection
            this.CloseConnection();
        }
    }

    //Delete statement
    public void Delete()
    {
        string query = "DELETE FROM tableinfo WHERE name='John Smith'";

        if (this.OpenConnection() == true)
        {
            MySqlCommand cmd = new MySqlCommand(query, connection);
            cmd.ExecuteNonQuery();
            this.CloseConnection();
        }
    }

    //Select statement
    public List<string>[] SelectMatch(string query)
    {
        //Create a list to store the result
        List<string>[] list = new List<string>[6];
        list[0] = new List<string>();
        list[1] = new List<string>();
        list[2] = new List<string>();
        list[3] = new List<string>();
        list[4] = new List<string>();
        list[5] = new List<string>();

        //Open connection
        if (this.OpenConnection() == true)
        {
            //Create Command
            MySqlCommand cmd = new MySqlCommand(query, connection);
            //Create a data reader and Execute the command
            MySqlDataReader dataReader = cmd.ExecuteReader();

            //Read the data and store them in the list
            while (dataReader.Read())
            {
                list[0].Add(dataReader["id"] + "");
                list[1].Add(dataReader["filename"] + "");
                list[2].Add(dataReader["team1"] + "");
                list[3].Add(dataReader["team2"] + "");
                list[4].Add(dataReader["score1"] + "");
                list[5].Add(dataReader["score2"] + "");
            }

            //close Data Reader
            dataReader.Close();

            //close Connection
            this.CloseConnection();

            //return list to be displayed
            return list;
        }
        else
        {
            return list;
        }
    }
    public List<string>[] SelectRound(string query)
    {
        //Create a list to store the result
        List<string>[] list = new List<string>[5];
        list[0] = new List<string>();
        list[1] = new List<string>();
        list[2] = new List<string>();
        list[3] = new List<string>();
        list[4] = new List<string>();

        //Open connection
        if (this.OpenConnection() == true)
        {
            //Create Command
            MySqlCommand cmd = new MySqlCommand(query, connection);
            //Create a data reader and Execute the command
            MySqlDataReader dataReader = cmd.ExecuteReader();

            //Read the data and store them in the list
            while (dataReader.Read())
            {
                list[0].Add(dataReader["id"] + "");
                list[1].Add(dataReader["matchID"] + "");
                list[2].Add(dataReader["roundType"] + "");
                list[3].Add(dataReader["bombPlanted"] + "");
                list[4].Add(dataReader["enemyTeamWiped"] + "");
            }

            //close Data Reader
            dataReader.Close();

            //close Connection
            this.CloseConnection();

            //return list to be displayed
            return list;
        }
        else
        {
            return list;
        }
    }
    public List<string>[] SelectPathpoints(string query)
    {
        //Create a list to store the result
        List<string>[] list = new List<string>[6];
        list[0] = new List<string>();
        list[1] = new List<string>();
        list[2] = new List<string>();
        list[3] = new List<string>();
        list[4] = new List<string>();
        list[5] = new List<string>();

        //Open connection
        if (this.OpenConnection() == true)
        {
            //Create Command
            MySqlCommand cmd = new MySqlCommand(query, connection);
            //Create a data reader and Execute the command
            MySqlDataReader dataReader = cmd.ExecuteReader();

            //Read the data and store them in the list
            while (dataReader.Read())
            {
                list[0].Add(dataReader["id"] + "");
                list[1].Add(dataReader["roundID"] + "");
                list[2].Add(dataReader["steamID"] + "");
                list[3].Add(dataReader["X"] + "");
                list[4].Add(dataReader["Y"] + "");
                list[5].Add(dataReader["Z"] + "");
            }

            //close Data Reader
            dataReader.Close();

            //close Connection
            this.CloseConnection();

            //return list to be displayed
            return list;
        }
        else
        {
            return list;
        }
    }

    //Count statement
    public int Count()
    {
        string query = "SELECT Count(*) FROM tableinfo";
        int Count = -1;

        //Open Connection
        if (this.OpenConnection() == true)
        {
            //Create Mysql Command
            MySqlCommand cmd = new MySqlCommand(query, connection);

            //ExecuteScalar will return one value
            Count = int.Parse(cmd.ExecuteScalar() + "");

            //close Connection
            this.CloseConnection();

            return Count;
        }
        else
        {
            return Count;
        }
    }

    //Backup
    public void Backup()
    {
        try
        {
            DateTime Time = DateTime.Now;
            int year = Time.Year;
            int month = Time.Month;
            int day = Time.Day;
            int hour = Time.Hour;
            int minute = Time.Minute;
            int second = Time.Second;
            int millisecond = Time.Millisecond;

            //Save file to C:\ with the current date as a filename
            string path;
            path = "C:\\MySqlBackup" + year + "-" + month + "-" + day +
        "-" + hour + "-" + minute + "-" + second + "-" + millisecond + ".sql";
            StreamWriter file = new StreamWriter(path);


            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = "mysqldump";
            psi.RedirectStandardInput = false;
            psi.RedirectStandardOutput = true;
            psi.Arguments = string.Format(@"-u{0} -p{1} -h{2} {3}",
                uid, password, server, database);
            psi.UseShellExecute = false;

            Process process = Process.Start(psi);

            string output;
            output = process.StandardOutput.ReadToEnd();
            file.WriteLine(output);
            process.WaitForExit();
            file.Close();
            process.Close();
        }
        catch (IOException ex)
        {
            Console.WriteLine("Error , unable to backup!");
        }
    }

    //Restore
    public void Restore()
    {
        try
        {
            //Read file from C:\
            string path;
            path = "C:\\MySqlBackup.sql";
            StreamReader file = new StreamReader(path);
            string input = file.ReadToEnd();
            file.Close();

            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = "mysql";
            psi.RedirectStandardInput = true;
            psi.RedirectStandardOutput = false;
            psi.Arguments = string.Format(@"-u{0} -p{1} -h{2} {3}",
                uid, password, server, database);
            psi.UseShellExecute = false;


            Process process = Process.Start(psi);
            process.StandardInput.WriteLine(input);
            process.StandardInput.Close();
            process.WaitForExit();
            process.Close();
        }
        catch (IOException ex)
        {
            Console.WriteLine("Error , unable to Restore!");
        }
    }
}