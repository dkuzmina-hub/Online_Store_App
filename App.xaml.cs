using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Data.SqlClient;
using OnlineStoreApp.Data;

namespace OnlineStoreApp
{
	public partial class App : Application
	{
		// Путь к файлу настроек
		public static readonly string SettingsPath = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
			"OnlineStoreApp",
			"connection.cfg");

		// Лог ошибок
		private static readonly string CrashLog = Path.Combine(
			AppDomain.CurrentDomain.BaseDirectory,
			"crash.log");

		// ─────────────────────────────────────────────────────────────
		// ПОДКЛЮЧЕНИЕ К БАЗЕ
		// ─────────────────────────────────────────────────────────────

		private static string DefaultConnStr =>
		@"Server=.\SQLEXPRESS02;
Database=online_store;
Trusted_Connection=True;
TrustServerCertificate=True;";

		// ─────────────────────────────────────────────────────────────

		protected override void OnStartup(StartupEventArgs e)
		{
			// Рабочая папка = папка приложения
			Environment.CurrentDirectory =
				AppDomain.CurrentDomain.BaseDirectory;

			// Ловим критические ошибки
			AppDomain.CurrentDomain.UnhandledException +=
				CurrentDomain_UnhandledException;

			DispatcherUnhandledException +=
				App_DispatcherUnhandledException;

			try
			{
				base.OnStartup(e);

				TryLoadSavedConnection();
			}
			catch (Exception ex)
			{
				WriteLog("StartupCrash", ex);

				MessageBox.Show(
					ex.ToString(),
					"Ошибка запуска",
					MessageBoxButton.OK,
					MessageBoxImage.Error);

				Shutdown();
			}
		}

		// ─────────────────────────────────────────────────────────────
		// GLOBAL EXCEPTION HANDLERS
		// ─────────────────────────────────────────────────────────────

		private void App_DispatcherUnhandledException(
			object sender,
			DispatcherUnhandledExceptionEventArgs e)
		{
			WriteLog("DispatcherUnhandledException", e.Exception);

			MessageBox.Show(
				$"Ошибка:\n\n{e.Exception.Message}\n\n" +
				$"Подробности:\n{CrashLog}",
				"Ошибка",
				MessageBoxButton.OK,
				MessageBoxImage.Error);

			e.Handled = true;
		}

		private void CurrentDomain_UnhandledException(
			object sender,
			UnhandledExceptionEventArgs e)
		{
			WriteLog(
				"UnhandledException",
				e.ExceptionObject as Exception);
		}

		// ─────────────────────────────────────────────────────────────
		// ЗАГРУЗКА ПОДКЛЮЧЕНИЯ
		// ─────────────────────────────────────────────────────────────

		private static void TryLoadSavedConnection()
		{
			try
			{
				// 1. Проверяем сохранённое подключение
				if (File.Exists(SettingsPath))
				{
					string saved =
						File.ReadAllText(SettingsPath).Trim();

					if (!string.IsNullOrWhiteSpace(saved)
						&& DB.Test(saved))
					{
						DB.ConnStr = saved;
						return;
					}
				}

				// 2. Используем подключение по умолчанию
				if (DB.Test(DefaultConnStr))
				{
					DB.ConnStr = DefaultConnStr;

					SaveConnection(DefaultConnStr);
				}
				else
				{
					throw new Exception(
						"Не удалось подключиться к базе данных.");
				}
			}
			catch (Exception ex)
			{
				WriteLog("ConnectionError", ex);

				MessageBox.Show(
					ex.ToString(),
					"Ошибка подключения к БД",
					MessageBoxButton.OK,
					MessageBoxImage.Error);
			}
		}

		// ─────────────────────────────────────────────────────────────
		// СОХРАНЕНИЕ ПОДКЛЮЧЕНИЯ
		// ─────────────────────────────────────────────────────────────

		public static void SaveConnection(string cs)
		{
			try
			{
				string? dir =
					Path.GetDirectoryName(SettingsPath);

				if (!string.IsNullOrEmpty(dir))
				{
					Directory.CreateDirectory(dir);
				}

				File.WriteAllText(SettingsPath, cs);
			}
			catch (Exception ex)
			{
				WriteLog("SaveConnectionError", ex);
			}
		}

		public static void ClearSavedConnection()
		{
			try
			{
				if (File.Exists(SettingsPath))
				{
					File.Delete(SettingsPath);
				}
			}
			catch (Exception ex)
			{
				WriteLog("ClearConnectionError", ex);
			}
		}

		// ─────────────────────────────────────────────────────────────
		// ЛОГИРОВАНИЕ
		// ─────────────────────────────────────────────────────────────

		private static void WriteLog(
			string kind,
			Exception? ex)
		{
			try
			{
				File.AppendAllText(
					CrashLog,

					$"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {kind}\n" +
					$"Message : {ex?.Message}\n" +
					$"Type    : {ex?.GetType().FullName}\n" +
					$"Stack   :\n{ex?.StackTrace}\n" +
					$"Inner   : {ex?.InnerException?.Message}\n" +
					new string('-', 70) + "\n");
			}
			catch
			{
			}
		}
	}

	/*public partial class App : Application
	{
		public static readonly string SettingsPath = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
			"OnlineStoreApp",
			"connection.cfg");

		private static readonly string CrashLog = Path.Combine(
			AppDomain.CurrentDomain.BaseDirectory,
			"crash.log");

		private static string DefaultConnStr =>
			"Server=.\\SQLEXPRESS02;" +
			"Database=online_store;" +
			"Trusted_Connection=True;" +
			"TrustServerCertificate=True;" +
			"Encrypt=False;";

		protected override void OnStartup(StartupEventArgs e)
		{
			Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;

			AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
			DispatcherUnhandledException += App_DispatcherUnhandledException;

			base.OnStartup(e);

			TryLoadConnection();
		}

		// ───────────────────────────────
		// CONNECTION
		// ───────────────────────────────

		private static void TryLoadConnection()
		{
			try
			{
				string connStr = DefaultConnStr;

				// пробуем файл
				if (File.Exists(SettingsPath))
				{
					string saved = File.ReadAllText(SettingsPath).Trim();
					if (!string.IsNullOrWhiteSpace(saved))
						connStr = saved;
				}

				// проверяем подключение
				TestConnection(connStr);

				DB.ConnStr = connStr;
				SaveConnection(connStr);

				MessageBox.Show("База данных подключена успешно", "OK");
			}
			catch (Exception ex)
			{
				WriteLog("DB_CONNECTION_ERROR", ex);

				MessageBox.Show(
					ex.ToString(),
					"Ошибка подключения к БД",
					MessageBoxButton.OK,
					MessageBoxImage.Error);

			}
		}

		// ───────────────────────────────
		// REAL TEST (БЕЗ ГЛУШЕНИЯ ОШИБОК)
		// ───────────────────────────────

		private static void TestConnection(string cs)
		{
			using var conn = new SqlConnection(cs);
			conn.Open(); // если ошибка — она ВСПЛЫВЁТ
		}

		// ───────────────────────────────
		// SAVE / CLEAR
		// ───────────────────────────────

		public static void SaveConnection(string cs)
		{
			try
			{
				string? dir = Path.GetDirectoryName(SettingsPath);
				if (!string.IsNullOrEmpty(dir))
					Directory.CreateDirectory(dir);

				File.WriteAllText(SettingsPath, cs);
			}
			catch (Exception ex)
			{
				WriteLog("SAVE_CONNECTION_ERROR", ex);
			}
		}

		public static void ClearSavedConnection()
		{
			try
			{
				if (File.Exists(SettingsPath))
					File.Delete(SettingsPath);
			}
			catch (Exception ex)
			{
				WriteLog("CLEAR_CONNECTION_ERROR", ex);
			}
		}

		// ───────────────────────────────
		// LOGGING
		// ───────────────────────────────

		private static void WriteLog(string kind, Exception ex)
		{
			try
			{
				File.AppendAllText(CrashLog,
					$"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {kind}\n" +
					$"{ex}\n" +
					new string('-', 70) + "\n");
			}
			catch { }
		}

		// ───────────────────────────────
		// GLOBAL ERRORS
		// ───────────────────────────────

		private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
		{
			WriteLog("UI_ERROR", e.Exception);

			MessageBox.Show(e.Exception.ToString(), "UI ERROR");

			e.Handled = true;
		}

		private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			WriteLog("FATAL_ERROR", e.ExceptionObject as Exception);
		}
	}*/
}