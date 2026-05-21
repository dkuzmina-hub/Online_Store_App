using System.Data;
using Microsoft.Data.SqlClient;

namespace OnlineStoreApp.Data
{
	public enum UserRole { Admin, Customer }

	public static class DatabaseHelper
	{
		public static UserRole CurrentRole { get; set; } = UserRole.Admin;
		public static string CurrentUserName { get; set; } = string.Empty;
		public static int CurrentUserId { get; set; } = 0;

		public static SqlConnection GetConnection() => new SqlConnection(DB.ConnStr);

		public static bool TestConnection(string connectionString)
		{
			try
			{
				using var conn = new SqlConnection(connectionString);
				conn.Open();
				return true;
			}
			catch { return false; }
		}

		public static DataTable ExecuteQuery(string query, SqlParameter[]? parameters = null)
		{
			var dt = new DataTable();
			using var conn = GetConnection();
			conn.Open();
			using var cmd = new SqlCommand(query, conn);
			if (parameters != null) cmd.Parameters.AddRange(parameters);
			using var adapter = new SqlDataAdapter(cmd);
			adapter.Fill(dt);
			return dt;
		}

		public static int ExecuteNonQuery(string query, SqlParameter[]? parameters = null)
		{
			using var conn = GetConnection();
			conn.Open();
			using var cmd = new SqlCommand(query, conn);
			if (parameters != null) cmd.Parameters.AddRange(parameters);
			return cmd.ExecuteNonQuery();
		}

		public static object? ExecuteScalar(string query, SqlParameter[]? parameters = null)
		{
			using var conn = GetConnection();
			conn.Open();
			using var cmd = new SqlCommand(query, conn);
			if (parameters != null) cmd.Parameters.AddRange(parameters);
			return cmd.ExecuteScalar();
		}

		public static void ExecuteStoredProcedure(string procName, SqlParameter[]? parameters = null)
		{
			using var conn = GetConnection();
			conn.Open();
			using var cmd = new SqlCommand(procName, conn);
			cmd.CommandType = CommandType.StoredProcedure;
			if (parameters != null) cmd.Parameters.AddRange(parameters);
			cmd.ExecuteNonQuery();
		}

		public static DataTable ExecuteStoredProcedureWithResult(string procName, SqlParameter[]? parameters = null)
		{
			var dt = new DataTable();
			using var conn = GetConnection();
			conn.Open();
			using var cmd = new SqlCommand(procName, conn);
			cmd.CommandType = CommandType.StoredProcedure;
			if (parameters != null) cmd.Parameters.AddRange(parameters);
			using var adapter = new SqlDataAdapter(cmd);
			adapter.Fill(dt);
			return dt;
		}
	}
}