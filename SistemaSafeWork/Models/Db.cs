using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Collections.Generic;

namespace SistemaSafeWork.Models
{
    public static class Db
    {
        public static SqlConnection Open()
        {
            var connection = new SqlConnection(ConfigurationManager.ConnectionStrings["SafeWork"].ConnectionString);
            connection.Open(); return connection;
        }
        public static SqlParameter P(string name, object value) { return new SqlParameter(name, value ?? DBNull.Value); }
        public static DataTable Query(string sql, params SqlParameter[] parameters)
        {
            using (var connection = Open()) using (var command = new SqlCommand(sql, connection))
            { command.Parameters.AddRange(parameters); using (var adapter = new SqlDataAdapter(command)) { var table = new DataTable(); adapter.Fill(table); return table; } }
        }
        public static int Execute(string sql, params SqlParameter[] parameters)
        {
            using (var connection = Open()) using (var command = new SqlCommand(sql, connection))
            { command.Parameters.AddRange(parameters); return command.ExecuteNonQuery(); }
        }
        public static string Text(DataRow row, string key) { return row[key] == DBNull.Value ? null : Convert.ToString(row[key]); }
        public static List<T> Map<T>(DataTable table, Func<DataRow,T> map)
        { var list = new List<T>(); foreach (DataRow row in table.Rows) list.Add(map(row)); return list; }
    }
}
