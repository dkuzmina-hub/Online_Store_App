using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Windows;
using Microsoft.Data.SqlClient;
using Microsoft.Win32;
using OnlineStoreApp.Data;

namespace OnlineStoreApp.Views
{
	public partial class BackupWindow : Window
	{
		private readonly string _backupDir;
		private string _sqlServiceAccount = "";

		public BackupWindow()
		{
			InitializeComponent();
			_backupDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Backups");
			Directory.CreateDirectory(_backupDir);
			Loaded += BackupWindow_Loaded;
		}

		private void BackupWindow_Loaded(object sender, RoutedEventArgs e)
		{
			TxtBackupName.Text = $"online_store_{DateTime.Now:yyyyMMdd_HHmmss}.bak";
			TxtSqlDataPath.Text = $"Папка назначения: {_backupDir}";
			DetectServiceAccountAndGrantAccess();
		}

		// Узнаём под каким аккаунтом работает SQL Server,
		// затем даём ему права на папку Backups\
		private void DetectServiceAccountAndGrantAccess()
		{
			try
			{
				// xp_cmdshell может быть отключен — используем sys.dm_server_services
				const string sql = @"
                    SELECT service_account 
                    FROM sys.dm_server_services 
                    WHERE servicename LIKE N'SQL Server (%';";

				using var con = new SqlConnection(DB.ConnStr);
				con.Open();
				using var cmd = new SqlCommand(sql, con);
				_sqlServiceAccount = cmd.ExecuteScalar()?.ToString() ?? "";
			}
			catch { _sqlServiceAccount = ""; }

			if (!string.IsNullOrEmpty(_sqlServiceAccount))
			{
				try { GrantFolderAccess(_backupDir, _sqlServiceAccount); }
				catch { /* Если не получилось — будем пробовать писать напрямую */ }

				Dispatcher.Invoke(() =>
					TxtSqlDataPath.Text =
						$"Папка: {_backupDir}\nАккаунт SQL Server: {_sqlServiceAccount}");
			}
			else
			{
				// Не смогли определить аккаунт — даём права всем локальным сервисам
				try { GrantFolderAccess(_backupDir, "NT AUTHORITY\\NETWORK SERVICE"); }
				catch { }
				try { GrantFolderAccess(_backupDir, "NT AUTHORITY\\LOCAL SERVICE"); }
				catch { }
				try { GrantFolderAccess(_backupDir, "NT SERVICE\\MSSQL$SQLEXPRESS"); }
				catch { }
				try { GrantFolderAccess(_backupDir, "NT SERVICE\\MSSQLSERVER"); }
				catch { }
			}
		}

		// Выдаём аккаунту права FullControl на папку
		private static void GrantFolderAccess(string folder, string account)
		{
			var dirInfo = new DirectoryInfo(folder);
			var security = dirInfo.GetAccessControl();
			var rule = new FileSystemAccessRule(
				account,
				FileSystemRights.FullControl,
				InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
				PropagationFlags.None,
				AccessControlType.Allow);
			security.AddAccessRule(rule);
			dirInfo.SetAccessControl(security);
		}

		// ── Создание резервной копии ──────────────────────────────────────
		private void BtnBackup_Click(object sender, RoutedEventArgs e)
		{
			string fileName = TxtBackupName.Text.Trim();
			if (string.IsNullOrEmpty(fileName))
			{
				Log("Введите имя файла.", isError: true); return;
			}
			if (!fileName.EndsWith(".bak", StringComparison.OrdinalIgnoreCase))
				fileName += ".bak";

			BtnBackup.IsEnabled = false;
			Log("Создание резервной копии...");

			try
			{
				string dbName = new SqlConnectionStringBuilder(DB.ConnStr).InitialCatalog;
				string finalPath = Path.Combine(_backupDir, fileName);
				string escaped = finalPath.Replace("'", "''");

				// SQL Server пишет напрямую в Backups\ — права уже выданы выше
				string sql = $@"BACKUP DATABASE [{dbName}]
                    TO DISK = N'{escaped}'
                    WITH FORMAT, INIT,
                         NAME = N'{dbName} backup {DateTime.Now:yyyy-MM-dd HH:mm}',
                         STATS = 10;";

				using (var con = new SqlConnection(DB.ConnStr))
				{
					con.Open();
					con.InfoMessage += (s, ev) => Dispatcher.Invoke(() => Log(ev.Message));
					using var cmd = new SqlCommand(sql, con) { CommandTimeout = 300 };
					cmd.ExecuteNonQuery();
				}

				long sizeKb = new FileInfo(finalPath).Length / 1024;
				Log($"Готово! {fileName}  ({sizeKb:N0} КБ)");
				Log($"Путь: {finalPath}");

				var open = MessageBox.Show(
					$"Резервная копия создана!\n\n{finalPath}\n\nРазмер: {sizeKb:N0} КБ\n\nОткрыть папку Backups?",
					"Готово", MessageBoxButton.YesNo, MessageBoxImage.Information);

				if (open == MessageBoxResult.Yes)
					System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{finalPath}\"");
			}
			catch (Exception ex)
			{
				Log($"Ошибка: {ex.Message}", isError: true);
				MessageBox.Show($"Ошибка создания резервной копии:\n\n{ex.Message}",
					"Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
			}
			finally { BtnBackup.IsEnabled = true; }
		}

		// ── Кнопка «Сохранить в…» ────────────────────────────────────────
		private void BtnBrowseBackup_Click(object sender, RoutedEventArgs e)
		{
			var dialog = new SaveFileDialog
			{
				Title = "Имя файла резервной копии",
				Filter = "SQL Server Backup (*.bak)|*.bak",
				FileName = TxtBackupName.Text,
				InitialDirectory = _backupDir
			};
			if (dialog.ShowDialog() == true)
				TxtBackupName.Text = Path.GetFileName(dialog.FileName);
		}

		// ── Выбор файла .bak для восстановления ──────────────────────────
		private void BtnBrowseRestore_Click(object sender, RoutedEventArgs e)
		{
			var dialog = new OpenFileDialog
			{
				Title = "Выберите файл резервной копии (.bak)",
				Filter = "SQL Server Backup (*.bak)|*.bak|Все файлы (*.*)|*.*",
				InitialDirectory = _backupDir
			};
			if (dialog.ShowDialog() == true)
			{
				TxtRestorePath.Text = dialog.FileName;
				Log($"Выбран файл: {Path.GetFileName(dialog.FileName)}");
			}
		}

		// ── Восстановление ────────────────────────────────────────────────
		private void BtnRestore_Click(object sender, RoutedEventArgs e)
		{
			string bakFile = TxtRestorePath.Text.Trim();
			if (string.IsNullOrEmpty(bakFile) || !File.Exists(bakFile))
			{
				Log("Выберите существующий файл резервной копии.", isError: true); return;
			}

			string dbName = new SqlConnectionStringBuilder(DB.ConnStr).InitialCatalog;

			var confirm = MessageBox.Show(
				$"ВНИМАНИЕ!\n\nВсе данные базы «{dbName}» будут заменены данными из:\n\n{bakFile}\n\nПродолжить?",
				"Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning);
			if (confirm != MessageBoxResult.Yes) return;

			Log($"Восстановление базы «{dbName}»...");

			try
			{
				var masterCs = new SqlConnectionStringBuilder(DB.ConnStr) { InitialCatalog = "master" };
				using var con = new SqlConnection(masterCs.ConnectionString);
				con.Open();
				con.InfoMessage += (s, ev) => Dispatcher.Invoke(() => Log(ev.Message));

				Log("Закрытие соединений с БД...");
				using (var cmd = new SqlCommand(
					$"ALTER DATABASE [{dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;",
					con)
				{ CommandTimeout = 30 })
					cmd.ExecuteNonQuery();

				Log("Восстановление...");
				string escaped = bakFile.Replace("'", "''");
				using (var cmd = new SqlCommand(
					$"RESTORE DATABASE [{dbName}] FROM DISK = N'{escaped}' WITH REPLACE, RECOVERY, STATS = 10;",
					con)
				{ CommandTimeout = 600 })
					cmd.ExecuteNonQuery();

				using (var cmd = new SqlCommand(
					$"ALTER DATABASE [{dbName}] SET MULTI_USER;", con)
				{ CommandTimeout = 30 })
					cmd.ExecuteNonQuery();

				Log("Восстановление завершено успешно!");
				MessageBox.Show($"База данных «{dbName}» восстановлена!",
					"Готово", MessageBoxButton.OK, MessageBoxImage.Information);
			}
			catch (Exception ex)
			{
				Log($"Ошибка: {ex.Message}", isError: true);
				try
				{
					var mb = new SqlConnectionStringBuilder(DB.ConnStr) { InitialCatalog = "master" };
					using var c2 = new SqlConnection(mb.ConnectionString); c2.Open();
					new SqlCommand($"ALTER DATABASE [{dbName}] SET MULTI_USER;", c2).ExecuteNonQuery();
				}
				catch { }
				MessageBox.Show($"Ошибка восстановления:\n\n{ex.Message}",
					"Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		// ── Лог ──────────────────────────────────────────────────────────
		private void Log(string message, bool isError = false)
		{
			Dispatcher.Invoke(() =>
			{
				string ts = DateTime.Now.ToString("HH:mm:ss");
				bool isPlaceholder = TxtLog.Text.StartsWith("Готов.");
				TxtLog.Text = (isPlaceholder ? "" : TxtLog.Text + "\n") + $"[{ts}] {message}";
				TxtLog.Foreground = isError
					? System.Windows.Media.Brushes.OrangeRed
					: System.Windows.Media.Brushes.LightGreen;
				LogScroll.ScrollToBottom();
			});
		}
	}
}
