using System;
using System.Windows;
using Microsoft.Data.SqlClient;
using OnlineStoreApp.Data;

namespace OnlineStoreApp.Views
{
	public partial class RegisterWindow : Window
	{
		public RegisterWindow() => InitializeComponent();

		private void BtnReg_Click(object sender, RoutedEventArgs e)
		{
			PanelErr.Visibility = Visibility.Collapsed;
			string name = TxtName.Text.Trim();
			string email = TxtEmail.Text.Trim();
			string pwd = PwdPass.Password;
			string phone = TxtPhone.Text.Trim();
			string addr = TxtAddr.Text.Trim();

			if (string.IsNullOrEmpty(name)) { ShowErr("Введите имя."); return; }
			if (string.IsNullOrEmpty(email)) { ShowErr("Введите email."); return; }
			if (!email.Contains('@')) { ShowErr("Некорректный email."); return; }
			if (string.IsNullOrEmpty(pwd)) { ShowErr("Введите пароль."); return; }
			if (pwd.Length < 4) { ShowErr("Пароль — минимум 4 символа."); return; }

			try
			{
				// sp_RegisterUser — проверяет дубликат email и создаёт пользователя
				DatabaseHelper.ExecuteStoredProcedure("sp_RegisterUser", new[]
				{
					new SqlParameter("@name",     name),
					new SqlParameter("@email",    email),
					new SqlParameter("@password", pwd),
					new SqlParameter("@phone",    string.IsNullOrEmpty(phone) ? DBNull.Value : (object)phone),
					new SqlParameter("@address",  string.IsNullOrEmpty(addr)  ? DBNull.Value : (object)addr)
				});

				MessageBox.Show("Аккаунт создан! Теперь вы можете войти.",
					"Регистрация успешна", MessageBoxButton.OK, MessageBoxImage.Information);
				Close();
			}
			catch (SqlException ex)
			{
				ShowErr(ex.Message.Contains("существует")
					? "Пользователь с таким email уже существует."
					: $"Ошибка: {ex.Message}");
			}
			catch (Exception ex) { ShowErr($"Ошибка: {ex.Message}"); }
		}

		private void ShowErr(string m) { TxtErr.Text = m; PanelErr.Visibility = Visibility.Visible; }
	}
}