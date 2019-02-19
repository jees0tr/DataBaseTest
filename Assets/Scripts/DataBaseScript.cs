using UnityEngine;
using System;
using System.Linq;
using Mono.Data.Sqlite;
using System.Text;
using System.Data;
using System.IO;
using UnityEngine.UI;
    
    

public class DataBaseScript : MonoBehaviour
{
    public enum LOG_TYPE
    {
        ITEM,
        ITEM_LOG,
    }

    public LOG_TYPE log_type = LOG_TYPE.ITEM_LOG;

    public enum ITEM_CONSUME_TYPE
    {
        none,
        used,
        used_for_craft,
        time_out,
    }

    public string table_name = "";

    public Text m_text;

    public void Init_Setting()
    {
        string user_id = "005";

        StringBuilder sb = new StringBuilder();
        sb.Append(log_type.ToString());
        sb.Append("_");
        sb.Append(user_id);
        sb.Append("_");
        sb.Append(CurrentTimeMillis().ToString());

        table_name = sb.ToString();

        Create_DataBase();
    }

    //데이터 베이스 생성
    public void Create_DataBase()
    {
        table_name = "item_log_" + CurrentTimeMillis().ToString();

        string conn = URI_Path_Name();
        IDbConnection dbconn;
        dbconn = (IDbConnection)new SqliteConnection(conn);
        dbconn.Open(); //Open connection to the database.

        IDbCommand dbcmd = dbconn.CreateCommand();
        string sqlQuery = "";

        if (log_type == LOG_TYPE.ITEM_LOG)
        {
            sqlQuery = "CREATE TABLE " + log_type.ToString() + " (";
            sqlQuery += "ITEM_UNIQUE_ID    INTEGER PRIMARY KEY   NOT NULL,";
            sqlQuery += "ITEM_ID           INTEGER               NOT NULL,";
            sqlQuery += "COUNT             INTEGER               NOT NULL,";
            sqlQuery += "CREATOR           INTEGER               NOT NULL,";
            sqlQuery += "CREATE_DATE       INTEGER               NOT NULL,";
            sqlQuery += "OWNER             INTEGER,";
            sqlQuery += "FINAL_USED_DATE   INTEGER,";
            sqlQuery += "CONSUME_TYPE      INTEGER,";
            sqlQuery += "CONSUME_DATE      INTEGER,";
            sqlQuery += "ITEM_LIST         BLOB";
            //sqlQuery += "ITEM_LIST         TEXT";
            sqlQuery += ");";
        }
        try
        {
            if (!sqlQuery.Equals(""))
            {
                dbcmd.CommandText = sqlQuery;
                dbcmd.ExecuteNonQuery();

                dbcmd.Dispose();
                dbcmd = null;
                dbconn.Close();
                dbconn = null;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogFormat("Exception : {0}", e.ToString());
        }
    }

    int index = 0;

    public void Insert_Data()
    {
        long[] data = new long[0];
        //string data = "{0,0}";

        Insert_Data(index, 0, 0, 0, 0, 0, 0, ITEM_CONSUME_TYPE.none, 0, data);
        index++;
    }


     int query_count = 0;
    SqliteTransaction insert_transaction = null;
    SqliteConnection insert_connection = null;

    public void Check_Connection_and_Transaction()
    {
        if (insert_connection == null && insert_transaction == null)
        {
            string conn = URI_Path_Name();
            insert_connection = new SqliteConnection(conn);
            insert_connection.Open();
            insert_transaction = insert_connection.BeginTransaction();
        }
    }

    /// <summary>
    /// 테이블에 데이터 등록
    /// </summary>
    /// <param name="item_unique_id">아이템 유니크 값</param>
    /// <param name="item_id"></param>
    /// <param name="item_count"></param>
    /// <param name="item_creator"></param>
    /// <param name="item_create_date"></param>
    /// <param name="item_owner"></param>
    /// <param name="item_final_used_date"></param>
    /// <param name="item_consume_type"></param>
    /// <param name="consume_date"></param>
    /// <param name="item_list"></param>
    public void Insert_Data(int item_unique_id, int item_id, short item_count, short item_creator, long item_create_date, short item_owner, long item_final_used_date, ITEM_CONSUME_TYPE item_consume_type, long consume_date, long[] item_list)
    {
        item_create_date = CurrentTimeMillis();

        byte[] byte_data = new byte[0];

        if (item_list.Length > 0)
        {
            byte_data = LongArray_To_Bytes(item_list);
        }

        Check_Connection_and_Transaction();

        using (var cmd = insert_connection.CreateCommand())
        {
            query_count++;

            StringBuilder sqlQuery = new StringBuilder();

            sqlQuery.Append("INSERT INTO ");
            sqlQuery.Append(log_type);
            sqlQuery.Append(" (ITEM_UNIQUE_ID, ITEM_ID, COUNT, CREATOR, CREATE_DATE, OWNER, FINAL_USED_DATE, CONSUME_TYPE, CONSUME_DATE, ITEM_LIST) VALUES (");
            sqlQuery.Append(item_unique_id);
            sqlQuery.Append(",");
            sqlQuery.Append(item_id);
            sqlQuery.Append(",");
            sqlQuery.Append(item_count);
            sqlQuery.Append(",");
            sqlQuery.Append(item_creator);
            sqlQuery.Append(",");
            sqlQuery.Append(item_create_date);
            sqlQuery.Append(",");
            sqlQuery.Append(item_owner);
            sqlQuery.Append(",");
            sqlQuery.Append(item_final_used_date);
            sqlQuery.Append(",");
            sqlQuery.Append((int)item_consume_type);
            sqlQuery.Append(",");
            sqlQuery.Append(consume_date);
            sqlQuery.Append(",@item_list);");

            cmd.CommandText = sqlQuery.ToString();

            cmd.Prepare();

            cmd.Parameters.Add("@item_list", DbType.Binary, byte_data.Length);
            cmd.Parameters["@item_list"].Value = byte_data;

            cmd.ExecuteNonQuery();

            //쿼리가 n개 이상 쌓인 경우에 커밋 처리
            if (query_count >= 10)
            {
                query_count = 0;
                System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
                sw.Start();
                //start timer
                insert_transaction.Commit();

                insert_connection.Close();

                insert_transaction = null;
                insert_connection = null;
                //end timer
                sw.Stop();
                Debug.LogFormat("Insert문 경과 시간 : {0}(msec)", sw.ElapsedMilliseconds);
            }
        }
    }


    public void Read_All_Data()
    {
        string conn = URI_Path_Name();

        using (SqliteConnection con = new SqliteConnection(conn))
        {
            con.Open();

            SqliteCommand cmd = new SqliteCommand(con);

            string sqlQuery = "SELECT *" + "FROM " + log_type;

            cmd.CommandText = sqlQuery;

            using (SqliteDataReader rdr = cmd.ExecuteReader())
            {
                while (rdr.Read())
                {
                    sqlQuery = "INSERT INTO " + log_type + " (ITEM_UNIQUE_ID, ITEM_ID, COUNT, CREATOR, CREATE_DATE, OWNER, FINAL_USED_DATE, CONSUME_TYPE, CONSUME_DATE, ITEM_LIST)";

                    byte[] bytBLOB = new byte[rdr.GetBytes(9, 0, null, 0, int.MaxValue)];
                    rdr.GetBytes(9, 0, bytBLOB, 0, bytBLOB.Length);

                    long[] data_temp = Bytes_To_LongArray(bytBLOB);

                    //Debug.Log("data_temp size : " + data_temp.Length);
                    StringBuilder s = new StringBuilder();
                    s.Append("ITEM_LIST DATA :");
                    
                    for (int i = 0; i < data_temp.Length; i++)
                    {
                        s.Append(" " + data_temp[i]);
                    }

                    
                    Debug.LogFormat("ITEM_UNIQUE_ID : {0}, ITEM_ID : {1}, COUNT : {2}, CREATOR : {3}, CREATE_DATE : {4}, OWNER : {5}, FINAL_USED_DATE : {6}," +
                        " CONSUME_TYPE : {7}, CONSUME_DATE : {8}, {9}",
                        rdr.GetInt64(0), rdr.GetInt32(1), rdr.GetInt16(2), rdr.GetInt16(3), rdr.GetInt64(4), rdr.GetInt16(5), rdr.GetInt64(6),
                        ((ITEM_CONSUME_TYPE)rdr.GetInt32(7)).ToString(), rdr.GetInt64(8), s);
                        
                    //MemoryStream stmBLOB = new MemoryStream(bytBLOB);
                }
            }

            con.Close();
        }

        /*
        string conn = "URI=file:" + Application.dataPath + "/" + log_type + ".s3db"; //Path to database.
        IDbConnection dbconn;
        dbconn = (IDbConnection)new SqliteConnection(conn);
        dbconn.Open(); //Open connection to the database.

        IDbCommand dbcmd = dbconn.CreateCommand();
        string sqlQuery = "SELECT *" + "FROM " + log_type;
        dbcmd.CommandText = sqlQuery;
        IDataReader reader = dbcmd.ExecuteReader();

        while (reader.Read())
        {
            long ITEM_UNIQUE_ID = reader.GetInt64(0);
            string ITEM_ID = reader.GetString(1);
            int COUNT = reader.GetInt32(2);
            long CREATOR = reader.GetInt64(3);
            long CREATE_DATE = reader.GetInt64(4);
            long OWNER = reader.GetInt64(5);
            long FINAL_USED_DATE = reader.GetInt64(6);
            int CONSUME_TYPE = reader.GetInt32(7);
            long CONSUME_DATE = reader.GetInt64(8);
            reader.GetBytes()
            
            //int ITEM_LIST = reader.getb

            
            Debug.LogFormat("USER_ID : {0}, ITEM_ID : {1}, ITEM_INDEX : {2}, ITEM_STACK : {3}, START_TIME : {4}, LAST_CHECK_TIME : {5}," +
                "CAPACITY_OF_SIZE : {6}, ITEM_SIZE : {7}, WEIGHT : {8}"
                , USER_ID, ITEM_ID, ITEM_INDEX, ITEM_STACK, START_TIME, LAST_CHECK_TIME, CAPACITY_OF_SIZE, ITEM_SIZE, WEIGHT);
                
        }
        reader.Close();
        reader = null;
        dbcmd.Dispose();
        dbcmd = null;
        dbconn.Close();
        dbconn = null;
        */
    }

    public string URI_Path_Name()
    {
        string sDirPath = Application.persistentDataPath + "/" + log_type.ToString() + "/";

        DirectoryInfo di = new DirectoryInfo(sDirPath);

        if (di.Exists == false)
        {
            di.Create();
        }

        string conn = "URI=file:" + sDirPath + table_name + ".s3db"; //Path to database.

        m_text.text = conn;

        return conn;
    }

    /*
    public void Find_Data()
    {
        Find_Data(m_table_name, user_id);
    }

    public void Find_Data(string table_name, int user_id)
    {
        string conn = "URI=file:" + Application.dataPath + "/" + table_name + ".s3db"; //Path to database.
        IDbConnection dbconn;
        dbconn = (IDbConnection)new SqliteConnection(conn);
        dbconn.Open(); //Open connection to the database.

        IDbCommand dbcmd = dbconn.CreateCommand();
        string sqlQuery = "SELECT *" + "FROM " + table_name + " WHERE OWNER = " + user_id.ToString();
        dbcmd.CommandText = sqlQuery;
        IDataReader reader = dbcmd.ExecuteReader();

        if (reader.IsDBNull(0))
        {
            Debug.Log("DB is Null");
        }
        else
        {
            while (reader.Read())
            {
                int USER_ID = reader.GetInt32(0);
                string ITEM_ID = reader.GetString(1);
                int ITEM_INDEX = reader.GetInt32(2);
                int ITEM_STACK = reader.GetInt32(3);
                long START_TIME = reader.GetInt64(4);
                long LAST_CHECK_TIME = reader.GetInt64(5);
                int CAPACITY_OF_SIZE = reader.GetInt32(6);
                int ITEM_SIZE = reader.GetInt32(7);
                int WEIGHT = reader.GetInt32(8);
            }
        }
        reader.Close();
        reader = null;
        dbcmd.Dispose();
        dbcmd = null;
        dbconn.Close();
        dbconn = null;
    }
    */

    #region 현재 시간을 UTC기준 밀리세컨드로로 반환, 밀리세컨드를 현재 시간으로 변환

    //현재 시간(밀리세컨드 단위)을 반환하는 메소드 [사용하는 PC의 시간]
    public long CurrentTimeMillis()
    {
        var timeSpan = (System.DateTime.UtcNow - new System.DateTime(1970, 1, 1, 0, 0, 0));

        return (long)timeSpan.TotalMilliseconds;
    }

    private DateTime Milliseconds2Date(Double d)
    {
        TimeSpan time = TimeSpan.FromMilliseconds(d);
        return new DateTime(1970, 1, 1) + time;
    }

    #endregion


    #region byte[]->long[],  long[]->byte[] 변환 메소드

    //바이트 배열을 롱 타입 배열로 변환하여 리턴하는 메소드
    public long[] Bytes_To_LongArray(byte[] bytes)
    {
        var size = bytes.Count() / sizeof(long);
        var longs = new long[size];
        for (var index = 0; index < size; index++)
        {
            longs[index] = BitConverter.ToInt32(bytes, index * sizeof(long));
        }

        return longs;
    }

    //롱 타입 배열을 바이트 배열로 변환하여 리턴하는 메소드
    public byte[] LongArray_To_Bytes(long[] longArray)
    {
        byte[] result = new byte[longArray.Length * sizeof(long)];
        Buffer.BlockCopy(longArray, 0, result, 0, result.Length);

        return result;
    }

    #endregion
}
