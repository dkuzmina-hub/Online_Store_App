using System;
using System.Collections.Generic;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using OnlineStoreApp.Data;

namespace OnlineStoreApp.Views
{
	public partial class OrderFormWindow : Window
	{
		private readonly DataRowView? _row;
		private readonly bool _isEdit;

		public OrderFormWindow(DataRowView? row)
		{
			InitializeComponent();
			_row = row;
			_isEdit = row != null;
			TxtTitle.Text = _isEdit ? "Редактировать заказ" : "Новый заказ";
			LoadUsers();
			DpDate.SelectedDate = DateTime.Today;

			if (_isEdit && row != null)
			{
				string userName = row.Row["username"]?.ToString() ?? "";
				foreach (var item in CboUser.Items)
					if (item is ComboItem ci && ci.Name == userName) { CboUser.SelectedItem = ci; break; }

				if (DateTime.TryParse(row.Row["date"]?.ToString(), out DateTime dt))
					DpDate.SelectedDate = dt;

				SetComboByContent(CboTransStatus, row.Row["trans_status"]?.ToString());
				SetComboByContent(CboPaymStatus, row.Row["paym_status"]?.ToString());
			}
			else
			{
				CboTransStatus.SelectedIndex = 0;
				CboPaymStatus.SelectedIndex = 0;
			}
		}

		private void LoadUsers()
		{
			try
			{
				// sp_GetUsersForCombo — SELECT id_user, name FROM users ORDER BY name
				var dt = DatabaseHelper.ExecuteQuery("sp_GetUsersForCombo");
				var list = new List<ComboItem>();
				foreach (DataRow r in dt.Rows)
					list.Add(new ComboItem((int)r["id_user"], r["name"].ToString()!));
				CboUser.ItemsSource = list;
				if (list.Count > 0) CboUser.SelectedIndex = 0;
			}
			catch (Exception ex) { ShowError($"Ошибка загрузки пользователей: {ex.Message}"); }
		}

		private static void SetComboByContent(ComboBox cbo, string? value)
		{
			if (string.IsNullOrEmpty(value)) return;
			foreach (ComboBoxItem item in cbo.Items)
				if (item.Content?.ToString() == value) { cbo.SelectedItem = item; return; }
		}

		private void BtnSave_Click(object sender, RoutedEventArgs e)
		{
			HideError();
			if (CboUser.SelectedItem is not ComboItem user) { ShowError("Выберите пользователя."); return; }
			if (DpDate.SelectedDate == null) { ShowError("Выберите дату."); return; }
			string trans = (CboTransStatus.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
			string paym = (CboPaymStatus.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";

			try
			{
				if (_isEdit && _row != null)
				{
					// sp_UpdateOrderFull — обновляет все поля заказа одним вызовом
					DatabaseHelper.ExecuteStoredProcedure("sp_UpdateOrderFull", new[]
					{
						new SqlParameter("@id_order",     _row.Row["id_order"]),
						new SqlParameter("@id_user",      user.Id),
						new SqlParameter("@date",         DpDate.SelectedDate.Value),
						new SqlParameter("@trans_status", trans),
						new SqlParameter("@paym_status",  paym)
					});
				}
				else
				{
					// CreateOrder — INSERT + возвращает id нового заказа
					DatabaseHelper.ExecuteStoredProcedure("CreateOrder", new[]
					{
						new SqlParameter("@id_user",      user.Id),
						new SqlParameter("@date",         DpDate.SelectedDate.Value),
						new SqlParameter("@trans_status", trans),
						new SqlParameter("@paym_status",  paym)
					});
				}
				DialogResult = true;
				Close();
			}
			catch (Exception ex) { ShowError($"Ошибка: {ex.Message}"); }
		}

		private void BtnCancel_Click(object sender, RoutedEventArgs e) => Close();
		private void ShowError(string msg) { TxtError.Text = msg; PanelError.Visibility = Visibility.Visible; }
		private void HideError() => PanelError.Visibility = Visibility.Collapsed;
	}
}