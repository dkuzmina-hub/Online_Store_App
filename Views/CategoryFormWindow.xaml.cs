using System;
using System.Data;
using System.Windows;
using Microsoft.Data.SqlClient;
using OnlineStoreApp.Data;

namespace OnlineStoreApp.Views
{
	public partial class CategoryFormWindow : Window
	{
		private readonly DataRowView? _row;
		private readonly bool _isEdit;

		public CategoryFormWindow(DataRowView? row)
		{
			InitializeComponent();
			_row = row;
			_isEdit = row != null;
			TxtTitle.Text = _isEdit ? "Редактировать категорию" : "Новая категория";
			if (_isEdit && row != null)
				TxtName.Text = row.Row["name_category"]?.ToString();
		}

		private void BtnSave_Click(object sender, RoutedEventArgs e)
		{
			HideError();
			string name = TxtName.Text.Trim();
			if (string.IsNullOrEmpty(name)) { ShowError("Введите название категории."); return; }
			if (name.Length > 50) { ShowError("Название не должно превышать 50 символов."); return; }

			try
			{
				if (_isEdit && _row != null)
				{
					DatabaseHelper.ExecuteStoredProcedure("sp_UpdateCategory", new[]
					{
						new SqlParameter("@id_category",   _row.Row["id_category"]),
						new SqlParameter("@name", name)
					});
				}
				else
				{
					DatabaseHelper.ExecuteStoredProcedure("sp_AddCategory", new[]
					{
						new SqlParameter("@name", name)
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
