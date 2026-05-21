using System.Data;
using Microsoft.Data.SqlClient;

namespace OnlineStoreApp.Data
{
	public static class DB
	{
		public static string ConnStr { get; set; } = "";

		public static bool Test(string cs)
		{
			try { using var c = new SqlConnection(cs); c.Open(); return true; }
			catch { return false; }
		}

		private static void AddParam(SqlCommand cmd, string name, object? val)
		{
			if (val == null || val == DBNull.Value)
			{
				cmd.Parameters.AddWithValue(name, DBNull.Value);
			}
			else if (val is string str)
			{
				var p = cmd.Parameters.Add(name, SqlDbType.NVarChar, -1);
				p.Value = str;
			}
			else
			{
				cmd.Parameters.AddWithValue(name, val);
			}
		}

		public static DataTable Query(string sql, params (string, object?)[] p)
		{
			var dt = new DataTable();
			using var con = new SqlConnection(ConnStr);
			con.Open();
			using var cmd = new SqlCommand(sql, con);
			foreach (var (name, val) in p)
				AddParam(cmd, name, val);
			new SqlDataAdapter(cmd).Fill(dt);
			return dt;
		}

		public static int Exec(string sql, params (string, object?)[] p)
		{
			using var con = new SqlConnection(ConnStr);
			con.Open();
			using var cmd = new SqlCommand(sql, con);
			foreach (var (name, val) in p)
				AddParam(cmd, name, val);
			return cmd.ExecuteNonQuery();
		}

		public static object? Scalar(string sql, params (string, object?)[] p)
		{
			using var con = new SqlConnection(ConnStr);
			con.Open();
			using var cmd = new SqlCommand(sql, con);
			foreach (var (name, val) in p)
				AddParam(cmd, name, val);
			return cmd.ExecuteScalar();
		}
	}
}