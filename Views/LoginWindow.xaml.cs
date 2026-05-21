using System;
using System.Windows;
using System.Windows.Media;
using OnlineStoreApp.Data;

namespace OnlineStoreApp.Views
{
	public partial class LoginWindow : Window
	{
		public LoginWindow()
		{
			InitializeComponent();
			Loaded += LoginWindow_Loaded;
		}

		
		private void LoginWindow_Loaded(object sender, RoutedEventArgs e)
		{
			UpdateConnectionStatus();
		}

		private void UpdateConnectionStatus()
		{
			bool connected = !string.IsNullOrEmpty(DB.ConnStr);

			if (connected)
			{
				
				ParseConnStr(DB.ConnStr, out string srv, out string db);
				TxtServer.Text = srv;
				TxtDb.Text = db;

				TxtConnStatus.Text = "✅  Подключено к БД";
				TxtConnStatus.Foreground = new SolidColorBrush(Color.FromRgb(22, 163, 74));
			}
			else
			{
				TxtConnStatus.Text = "⚙  Настройки подключения";
				TxtConnStatus.Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139));
			}
		}

		
		private void Expander_Changed(object sender, RoutedEventArgs e)
		{
			Height = ExpanderConn.IsExpanded ? 600 : 480;
		}

	
		private void BtnTestConn_Click(object sender, RoutedEventArgs e)
		{
			HideErr();
			string cs = BuildConnStr();
			if (cs == null!) return;

			if (DB.Test(cs))
			{
				DB.ConnStr = cs;
				App.SaveConnection(cs);
				UpdateConnectionStatus();
				ExpanderConn.IsExpanded = false;
			}
			else
			{
				ShowErr("Не удалось подключиться. Проверьте сервер и имя базы данных.");
			}
		}

		
		private void BtnLogin_Click(object sender, RoutedEventArgs e)
		{
			HideErr();

			string email = TxtEmail.Text.Trim();
			string pwd = PwdPass.Password;

			if (string.IsNullOrEmpty(email)) { ShowErr("Введите email."); return; }
			if (string.IsNullOrEmpty(pwd)) { ShowErr("Введите пароль."); return; }

			
			if (string.IsNullOrEmpty(DB.ConnStr))
			{
				string cs = BuildConnStr();
				if (cs == null!) return;

				if (!DB.Test(cs))
				{
					ShowErr("Не удалось подключиться к базе данных.\n" +
							"Разверните «Настройки подключения» и проверьте параметры.");
					ExpanderConn.IsExpanded = true;
					return;
				}
				DB.ConnStr = cs;
				App.SaveConnection(cs);
				UpdateConnectionStatus();
			}

			try
			{
				
				var dt = DatabaseHelper.ExecuteStoredProcedureWithResult("sp_Login", new[]
				{
					new Microsoft.Data.SqlClient.SqlParameter("@email",    email),
					new Microsoft.Data.SqlClient.SqlParameter("@password", pwd)
				});

				if (dt.Rows.Count == 0) { ShowErr("Неверный email или пароль."); return; }

				string role = dt.Rows[0]["role_name"]?.ToString() ?? "";

				Session.UserId = Convert.ToInt32(dt.Rows[0]["id_user"]);
				Session.UserName = dt.Rows[0]["name"]?.ToString() ?? email;
				Session.Role = role;

				
				if (role == "admin")
					new MainWindow().Show();
				else
					new ShopWindow().Show();

				Close();
			}
			catch (Exception ex) { ShowErr($"Ошибка: {ex.Message}"); }
		}

	
		private void BtnRegister_Click(object sender, RoutedEventArgs e)
		{
		
			if (string.IsNullOrEmpty(DB.ConnStr))
			{
				string cs = BuildConnStr();
				if (cs == null!) return;

				if (!DB.Test(cs))
				{
					ShowErr("Нет подключения к БД. Разверните «Настройки подключения».");
					ExpanderConn.IsExpanded = true;
					return;
				}
				DB.ConnStr = cs;
				App.SaveConnection(cs);
				UpdateConnectionStatus();
			}

			new RegisterWindow { Owner = this }.ShowDialog();
		}

		
		private string? BuildConnStr()
		{
			string server = TxtServer.Text.Trim();
			string db = TxtDb.Text.Trim();

			if (string.IsNullOrEmpty(server) || string.IsNullOrEmpty(db))
			{
				ShowErr("Заполните поля «Сервер» и «База данных».");
				ExpanderConn.IsExpanded = true;
				return null;
			}

			return $"Server={server};Database={db};" +
				   "Integrated Security=True;TrustServerCertificate=True;";
		}

		
		private static void ParseConnStr(string cs, out string server, out string db)
		{
			server = "";
			db = "";
			foreach (string part in cs.Split(';'))
			{
				int idx = part.IndexOf('=');
				if (idx < 0) continue;
				string key = part[..idx].Trim().ToLower();
				string val = part[(idx + 1)..].Trim();
				if (key is "server" or "data source") server = val;
				if (key is "database" or "initial catalog") db = val;
			}
		}

		private void ShowErr(string m) { TxtErr.Text = m; PanelErr.Visibility = Visibility.Visible; }
		private void HideErr() => PanelErr.Visibility = Visibility.Collapsed;
	}
}