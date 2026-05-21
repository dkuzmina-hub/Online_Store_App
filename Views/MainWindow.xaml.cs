using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using OnlineStoreApp.Data;
using OnlineStoreApp.Services;

namespace OnlineStoreApp.Views
{
	public partial class MainWindow : Window
	{
		private string _currentTable = "Users";
		private DataRowView? _selectedRow;

		public MainWindow()
		{
			InitializeComponent();
			LoadTable("Users");
		}

		#region Navigation

		private void NavButton_Click(object sender, RoutedEventArgs e)
		{
			if (sender is not Button btn) return;
			string table = btn.Tag?.ToString() ?? "Users";
			SetActiveNav(table);
			LoadTable(table);
		}

		private void SetActiveNav(string table)
		{
			var navButtons = new[] { BtnNavUsers, BtnNavCategories, BtnNavProducts, BtnNavOrders, BtnNavOrderItems, BtnNavDashboard };
			var tags = new[] { "Users", "Categories", "Products", "Orders", "OrderItems", "Dashboard" };

			for (int i = 0; i < navButtons.Length; i++)
				navButtons[i].Style = (Style)FindResource(tags[i] == table ? "NavButtonActive" : "NavButton");
		}

		private void BtnNavDashboard_Click(object sender, RoutedEventArgs e)
		{
			SetActiveNav("Dashboard");
			new DashboardWindow { Owner = this }.ShowDialog();
			SetActiveNav(_currentTable);
		}

		private void BtnDisconnect_Click(object sender, RoutedEventArgs e)
		{
			Session.Clear();
			new LoginWindow().Show();
			Close();
		}

		private void BtnNavBackup_Click(object sender, RoutedEventArgs e)
			=> new BackupWindow { Owner = this }.ShowDialog();

		#endregion

		#region Load Table

		private void LoadTable(string table)
		{
			_currentTable = table;
			_selectedRow = null;
			BtnEdit.IsEnabled = false;
			BtnDelete.IsEnabled = false;
			HideError();
			ConfigureSearchFields(table);

			switch (table)
			{
				case "Users": SetPageHeader("Пользователи", "Управление пользователями"); break;
				case "Categories": SetPageHeader("Категории", "Управление категориями товаров"); break;
				case "Products": SetPageHeader("Товары", "Управление каталогом товаров"); break;
				case "Orders": SetPageHeader("Заказы", "Управление заказами"); break;
				case "OrderItems": SetPageHeader("Состав заказов", "Позиции в заказах"); break;
			}

			RefreshGrid();
		}

		private void SetPageHeader(string title, string subtitle)
		{
			TxtPageTitle.Text = title;
			TxtPageSubtitle.Text = subtitle;
		}

		private void ConfigureSearchFields(string table)
		{
			CboSearchField.Items.Clear();
			TxtSearch.Text = "";

			switch (table)
			{
				case "Users":
					CboSearchField.Items.Add("Имя");
					CboSearchField.Items.Add("Email");
					CboSearchField.Items.Add("Телефон");
					CboSearchField.Items.Add("Адрес");
					break;
				case "Categories":
					CboSearchField.Items.Add("Название");
					break;
				case "Products":
					CboSearchField.Items.Add("Название");
					CboSearchField.Items.Add("Описание");
					CboSearchField.Items.Add("Категория");
					break;
				case "Orders":
					CboSearchField.Items.Add("Пользователь");
					CboSearchField.Items.Add("Статус транзакции");
					CboSearchField.Items.Add("Статус оплаты");
					CboSearchField.Items.Add("Дата");
					break;
				case "OrderItems":
					CboSearchField.Items.Add("Название товара");
					CboSearchField.Items.Add("№ заказа");
					break;
			}
			CboSearchField.SelectedIndex = 0;
		}

		private void RefreshGrid(string searchField = "", string searchValue = "")
		{
			try
			{
				DataTable dt = GetTableData(_currentTable, searchField, searchValue);
				AddRowNumbers(dt);
				MainGrid.ItemsSource = dt.DefaultView;
				ConfigureColumns(_currentTable);
				TxtRowCount.Text = $"Записей: {dt.Rows.Count}";
			}
			catch (Exception ex)
			{
				ShowError($"Ошибка загрузки данных: {ex.Message}");
			}
		}

		private DataTable GetTableData(string table, string searchField, string searchValue)
		{
			switch (table)
			{
				case "Users":
					{
						string? field = string.IsNullOrEmpty(searchValue) ? null : searchField switch
						{
							"Email" => "email",
							"Телефон" => "phone",
							"Адрес" => "address",
							_ => "name"
						};

						return DatabaseHelper.ExecuteStoredProcedureWithResult("sp_GetUsers", new[]
						{
				new SqlParameter("@search_field", (object?)field ?? DBNull.Value),
				new SqlParameter("@search_value",
					string.IsNullOrEmpty(searchValue)
						? (object)DBNull.Value
						: searchValue)
			});
					}

				case "Categories":
					{
						return DatabaseHelper.ExecuteStoredProcedureWithResult("sp_GetCategories", new[]
						{
				new SqlParameter("@search_value",
					string.IsNullOrEmpty(searchValue)
						? (object)DBNull.Value
						: searchValue)
			});
					}

				case "Products":
					{
						string? field = string.IsNullOrEmpty(searchValue) ? null : searchField switch
						{
							"Описание" => "description",
							"Категория" => "category",
							_ => "name"
						};

						return DatabaseHelper.ExecuteStoredProcedureWithResult("sp_GetProducts", new[]
						{
				new SqlParameter("@search_field",  (object?)field ?? DBNull.Value),
				new SqlParameter("@search_value",
					string.IsNullOrEmpty(searchValue)
						? (object)DBNull.Value
						: searchValue),
				new SqlParameter("@id_category", DBNull.Value),
				new SqlParameter("@in_stock_only", 0)
			});
					}

				case "Orders":
					{
						string? field = string.IsNullOrEmpty(searchValue) ? null : searchField switch
						{
							"Статус транзакции" => "trans_status",
							"Статус оплаты" => "paym_status",
							"Дата" => "date",
							_ => "username"
						};

						return DatabaseHelper.ExecuteStoredProcedureWithResult("sp_GetOrders", new[]
						{
				new SqlParameter("@search_field", (object?)field ?? DBNull.Value),
				new SqlParameter("@search_value",
					string.IsNullOrEmpty(searchValue)
						? (object)DBNull.Value
						: searchValue)
			});
					}

				case "OrderItems":
					{
						string? field = string.IsNullOrEmpty(searchValue) ? null : searchField switch
						{
							"№ заказа" => "id_order",
							_ => "name_prod"
						};

						return DatabaseHelper.ExecuteStoredProcedureWithResult("sp_GetOrderItemsList", new[]  // ← FIXED
						{
		new SqlParameter("@search_field", (object?)field ?? DBNull.Value),
		new SqlParameter("@search_value",
			string.IsNullOrEmpty(searchValue)
				? (object)DBNull.Value
				: searchValue)
	});
					}

				default:
					return new DataTable();
			}
		}

		private static void AddRowNumbers(DataTable dt)
		{
			dt.Columns.Add("№", typeof(int)).SetOrdinal(0);
			for (int i = 0; i < dt.Rows.Count; i++)
				dt.Rows[i]["№"] = i + 1;
		}

		private void ConfigureColumns(string table)
		{
			MainGrid.Columns.Clear();
			switch (table)
			{
				case "Users":
					AddCol("№", "№", 45);
					AddCol("Имя", "name", 150);
					AddCol("Email", "email", 180);
					AddCol("Телефон", "phone", 120);
					AddCol("Адрес", "address", 150);
					break;
				case "Categories":
					AddCol("№", "№", 45);
					AddCol("Название категории", "name_category", 300);
					break;
				case "Products":
					AddCol("№", "№", 45);
					AddCol("Название", "name_prod", 150);
					AddCol("Описание", "description", 150);
					AddCol("Цена (MDL)", "price", 90);
					AddCol("Остаток", "stock", 70);
					AddCol("Категория", "name_category", 110);
					AddCol("Размеры", "size_type", 90);
					break;
				case "Orders":
					AddCol("№", "№", 45);
					AddCol("Пользователь", "username", 150);
					AddCol("Дата", "date", 110);
					AddCol("Статус транзакции", "trans_status", 140);
					AddCol("Статус оплаты", "paym_status", 120);
					break;
				case "OrderItems":
					AddCol("№ заказа", "id_order", 90);
					AddCol("Товар", "name_prod", 180);
					AddCol("Размер", "size_label", 80);
					AddCol("Количество", "quantity", 90);
					AddCol("Цена (MDL)", "price", 110);
					AddCol("Сумма (MDL)", "total", 110);
					break;
			}
		}

		private void AddCol(string header, string binding, int width)
		{
			MainGrid.Columns.Add(new DataGridTextColumn
			{
				Header = header,
				Binding = new System.Windows.Data.Binding(binding),
				Width = width
			});
		}

		#endregion

		#region Selection / Actions

		private void MainGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			_selectedRow = MainGrid.SelectedItem as DataRowView;
			bool hasSelection = _selectedRow != null;
			BtnEdit.IsEnabled = hasSelection;
			BtnDelete.IsEnabled = hasSelection;
		}

		private void BtnAdd_Click(object sender, RoutedEventArgs e) => OpenForm(null);

		private void BtnEdit_Click(object sender, RoutedEventArgs e)
		{
			if (_selectedRow == null) return;
			OpenForm(_selectedRow);
		}

		private void OpenForm(DataRowView? row)
		{
			HideError();
			Window? form = _currentTable switch
			{
				"Users" => new UserFormWindow(row),
				"Categories" => new CategoryFormWindow(row),
				"Products" => new ProductFormWindow(row),
				"Orders" => new OrderFormWindow(row),
				"OrderItems" => row == null ? new OrderItemFormWindow() : new OrderItemEditWindow(row),
				_ => null
			};

			if (form == null) return;
			form.Owner = this;
			if (form.ShowDialog() == true)
				RefreshGrid();
		}

		private void BtnDelete_Click(object sender, RoutedEventArgs e)
		{
			if (_selectedRow == null) return;

			string itemDesc = _currentTable switch
			{
				"Users" => _selectedRow.Row["name"]?.ToString() ?? "",
				"Categories" => _selectedRow.Row["name_category"]?.ToString() ?? "",
				"Products" => _selectedRow.Row["name_prod"]?.ToString() ?? "",
				"Orders" => $"заказ №{_selectedRow.Row["id_order"]}",
				"OrderItems" => $"позицию товара '{_selectedRow.Row["name_prod"]}' в заказе №{_selectedRow.Row["id_order"]}",
				_ => "запись"
			};

			var result = MessageBox.Show(
				$"Вы уверены, что хотите удалить «{itemDesc}»?",
				"Подтверждение удаления",
				MessageBoxButton.YesNo,
				MessageBoxImage.Warning);

			if (result != MessageBoxResult.Yes) return;

			try
			{
				DeleteRow(_selectedRow);
				RefreshGrid();
			}
			catch (SqlException ex)
			{
				
				ShowError($"Ошибка удаления: {ex.Message}");
			}
			catch (Exception ex)
			{
				ShowError($"Ошибка удаления: {ex.Message}");
			}
		}

		private void DeleteRow(DataRowView row)
		{
			switch (_currentTable)
			{
				case "Users":
					DatabaseHelper.ExecuteStoredProcedure("sp_DeleteUser", new[]
					{
						new SqlParameter("@id_user", Convert.ToInt32(row.Row["id_user"]))
					});
					break;

				case "Categories":
					DatabaseHelper.ExecuteStoredProcedure("sp_DeleteCategory", new[]
					{
						new SqlParameter("@id_category", Convert.ToInt32(row.Row["id_category"]))
					});
					break;

				case "Products":
					DatabaseHelper.ExecuteStoredProcedure("sp_DeleteProduct", new[]
					{
						new SqlParameter("@id_product", Convert.ToInt32(row.Row["id_product"]))
					});
					break;

				case "Orders":
					DatabaseHelper.ExecuteStoredProcedure("sp_DeleteOrder", new[]
					{
						new SqlParameter("@id_order", Convert.ToInt32(row.Row["id_order"]))
					});
					break;

				case "OrderItems":
					DatabaseHelper.ExecuteStoredProcedure("sp_DeleteOrderItem", new[]
					{
						new SqlParameter("@id_order",   Convert.ToInt32(row.Row["id_order"])),
						new SqlParameter("@id_product", Convert.ToInt32(row.Row["id_product"]))
					});
					break;
			}
		}

		private void BtnNavReports_Click(object sender, RoutedEventArgs e)
		{
			try { new ReportView { Owner = this }.ShowDialog(); }
			catch (Exception ex) { MessageBox.Show("Ошибка при открытии отчётов:\n" + ex.Message); }
		}

		#endregion

		#region Search

		private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
		{
			HideError();
			string searchField = CboSearchField.SelectedItem?.ToString() ?? "";
			string searchValue = TxtSearch.Text.Trim();
			RefreshGrid(searchField, searchValue);
		}

		private void CboSearchField_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (string.IsNullOrWhiteSpace(TxtSearch?.Text)) return;
			HideError();
			string searchField = CboSearchField.SelectedItem?.ToString() ?? "";
			string searchValue = TxtSearch.Text.Trim();
			RefreshGrid(searchField, searchValue);
		}


		private void BtnSearch_Click(object sender, RoutedEventArgs e)
		{
			string searchField = CboSearchField.SelectedItem?.ToString() ?? "";
			string searchValue = TxtSearch.Text.Trim();
			if (string.IsNullOrEmpty(searchValue)) { ShowError("Введите текст для поиска."); return; }
			HideError();
			RefreshGrid(searchField, searchValue);
		}

		private void BtnReset_Click(object sender, RoutedEventArgs e)
		{
			TxtSearch.Text = "";
			HideError();
			RefreshGrid();
		}

		#endregion

		#region Error

		private void ShowError(string msg) { TxtError.Text = msg; PanelError.Visibility = Visibility.Visible; }
		private void HideError() => PanelError.Visibility = Visibility.Collapsed;

		#endregion
	}
}