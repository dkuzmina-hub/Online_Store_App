using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using OnlineStoreApp.Data;

namespace OnlineStoreApp.Views
{
	public partial class UserFormWindow : Window
	{
		private readonly DataRowView? _row;
		private readonly bool _isEdit;

		public UserFormWindow(DataRowView? row)
		{
			InitializeComponent();
			_row = row;
			_isEdit = row != null;
			TxtTitle.Text = _isEdit ? "Редактировать пользователя" : "Новый пользователь";

			if (_isEdit && row != null)
			{
				TxtName.Text = row.Row["name"]?.ToString();
				TxtEmail.Text = row.Row["email"]?.ToString();
				TxtPhone.Text = row.Row["phone"]?.ToString();
				TxtAddress.Text = row.Row["address"]?.ToString();
			}
		}

		private void BtnSave_Click(object sender, RoutedEventArgs e)
		{
			HideError();

			string name = TxtName.Text.Trim();
			string email = TxtEmail.Text.Trim();
			string pwd = PwdPassword.Password;
			string phone = TxtPhone.Text.Trim();
			string address = TxtAddress.Text.Trim();

			if (string.IsNullOrEmpty(name)) { ShowError("Введите имя."); TxtName.Focus(); return; }
			if (string.IsNullOrEmpty(email)) { ShowError("Введите email."); TxtEmail.Focus(); return; }
			if (!email.Contains("@")) { ShowError("Некорректный формат email."); TxtEmail.Focus(); return; }
			if (!_isEdit && string.IsNullOrEmpty(pwd)) { ShowError("Введите пароль."); return; }
			if (!_isEdit && pwd.Length < 4) { ShowError("Пароль должен содержать не менее 4 символов."); return; }

			try
			{
				if (_isEdit && _row != null)
				{
					// Если пароль не введён — оставляем текущий через sp_GetUserPassword
					string actualPwd = string.IsNullOrEmpty(pwd)
						? GetCurrentPassword(Convert.ToInt32(_row.Row["id_user"]))
						: pwd;

					DatabaseHelper.ExecuteStoredProcedure("UpdateUser", new[]
					{
						new SqlParameter("@id_user",  _row.Row["id_user"]),
						new SqlParameter("@name",     name),
						new SqlParameter("@email",    email),
						new SqlParameter("@password", actualPwd),
						new SqlParameter("@phone",    (object?)phone    ?? DBNull.Value),
						new SqlParameter("@address",  (object?)address  ?? DBNull.Value)
					});
				}
				else
				{
					DatabaseHelper.ExecuteStoredProcedure("AddUser", new[]
					{
						new SqlParameter("@name",     name),
						new SqlParameter("@email",    email),
						new SqlParameter("@password", pwd),
						new SqlParameter("@phone",    string.IsNullOrEmpty(phone)   ? DBNull.Value : (object)phone),
						new SqlParameter("@address",  string.IsNullOrEmpty(address) ? DBNull.Value : (object)address)
					});
				}

				DialogResult = true;
				Close();
			}
			catch (SqlException ex)
			{
				ShowError(ex.Message.Contains("существует")
					? "Пользователь с таким email уже существует."
					: $"Ошибка: {ex.Message}");
			}
			catch (Exception ex) { ShowError($"Ошибка: {ex.Message}"); }
		}

		/// <summary>
		/// Получает текущий пароль через sp_GetUserPassword (без прямого SELECT).
		/// </summary>
		private static string GetCurrentPassword(int userId)
		{
			var result = DatabaseHelper.ExecuteQuery("sp_GetUserPassword", new[]
			{
				new SqlParameter("@id_user", userId)
			});
			return result.Rows.Count > 0 ? result.Rows[0]["password"]?.ToString() ?? "" : "";
		}

		private void BtnCancel_Click(object sender, RoutedEventArgs e) => Close();

		private void ShowError(string msg) { TxtError.Text = msg; PanelError.Visibility = Visibility.Visible; }
		private void HideError() => PanelError.Visibility = Visibility.Collapsed;
	}
}