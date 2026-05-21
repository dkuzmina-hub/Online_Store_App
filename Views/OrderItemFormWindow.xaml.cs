using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Data.SqlClient;
using OnlineStoreApp.Data;

namespace OnlineStoreApp.Views
{
    public partial class OrderItemFormWindow : Window
    {
        private int _selectedProductStock = 0;

        public OrderItemFormWindow()
        {
            InitializeComponent();
            LoadOrders();
            LoadProducts();
        }

		private void LoadOrders()
		{
			try
			{
				var dt = DatabaseHelper.ExecuteStoredProcedureWithResult(
					"sp_GetOrdersForCombo", Array.Empty<SqlParameter>());

				var list = new List<ComboItem>();
				foreach (DataRow r in dt.Rows)
					list.Add(new ComboItem((int)r["id_order"], r["display"].ToString()!));

				CboOrder.ItemsSource = list;
				if (list.Count > 0) CboOrder.SelectedIndex = 0;
			}
			catch (Exception ex) { ShowError($"Ошибка загрузки заказов: {ex.Message}"); }
		}

		private void LoadProducts()
		{
			try
			{
				var dt = DatabaseHelper.ExecuteStoredProcedureWithResult(
					"sp_GetProductsInStock", Array.Empty<SqlParameter>());

				var list = new List<ProductComboItem>();
				foreach (DataRow r in dt.Rows)
					list.Add(new ProductComboItem(
						(int)r["id_product"],
						r["name_prod"].ToString()!,
						(int)r["stock"]));

				CboProduct.ItemsSource = list;
				if (list.Count > 0) CboProduct.SelectedIndex = 0;
			}
			catch (Exception ex) { ShowError($"Ошибка загрузки товаров: {ex.Message}"); }
		}
		

        private void CboProduct_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CboProduct.SelectedItem is ProductComboItem p)
            {
                _selectedProductStock = p.Stock;
                LblStock.Content = $"Количество * (на складе: {p.Stock})";
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            HideError();

            if (CboOrder.SelectedItem is not ComboItem order)
            { ShowError("Выберите заказ."); return; }

            if (CboProduct.SelectedItem is not ProductComboItem product)
            { ShowError("Выберите товар."); return; }

            if (!int.TryParse(TxtQuantity.Text.Trim(), out int qty) || qty <= 0)
            { ShowError("Введите корректное количество (больше 0)."); TxtQuantity.Focus(); return; }

            if (qty > _selectedProductStock)
            { ShowError($"Недостаточно товара на складе. Доступно: {_selectedProductStock}."); return; }

            try
            {
                DatabaseHelper.ExecuteStoredProcedure("AddOrderItem", new[]
                {
                    new SqlParameter("@id_order", order.Id),
                    new SqlParameter("@id_product", product.Id),
                    new SqlParameter("@quantity", qty)
                });

                DialogResult = true;
                Close();
            }
            catch (SqlException ex)
            {
                string msg = ex.Message;
                if (msg.Contains("50004")) msg = "Заказ не существует.";
                else if (msg.Contains("50005")) msg = "Товар не существует.";
                else if (msg.Contains("50006")) msg = "Недостаточно товара на складе.";
                ShowError($"Ошибка: {msg}");
            }
            catch (Exception ex) { ShowError($"Ошибка: {ex.Message}"); }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => Close();
        private void ShowError(string msg) { TxtError.Text = msg; PanelError.Visibility = Visibility.Visible; }
        private void HideError() => PanelError.Visibility = Visibility.Collapsed;
    }

    public class ProductComboItem : ComboItem
    {
        public int Stock { get; }
        public ProductComboItem(int id, string name, int stock) : base(id, name)
        {
            Stock = stock;
        }
    }
}
