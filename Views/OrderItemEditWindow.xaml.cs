using System;
using System.Data;
using System.Windows;
using Microsoft.Data.SqlClient;
using OnlineStoreApp.Data;

namespace OnlineStoreApp.Views
{
    public partial class OrderItemEditWindow : Window
    {
        private readonly DataRowView _row;
        private readonly int _orderId;
        private readonly int _productId;
        private int _currentStock;

        public OrderItemEditWindow(DataRowView row)
        {
            InitializeComponent();
            _row = row;
            _orderId = Convert.ToInt32(row.Row["id_order"]);
            _productId = Convert.ToInt32(row.Row["id_product"]);

            TxtOrderId.Text = $"№{_orderId}";
            TxtProductName.Text = row.Row["name_prod"]?.ToString();
            TxtQuantity.Text = row.Row["quantity"]?.ToString();

            LoadStockInfo();
        }

        private void LoadStockInfo()
        {
            try
            {
                // stock в БД уже уменьшен на текущее количество в заказе,
                // поэтому реально доступно: stock + текущее кол-во в заказе
                var result = DatabaseHelper.ExecuteScalar(
                    "SELECT stock FROM products WHERE id_product = @id",
                    new[] { new SqlParameter("@id", _productId) });

                int stockInDb = result != null ? Convert.ToInt32(result) : 0;
                int currentQty = Convert.ToInt32(_row.Row["quantity"]);
                _currentStock = stockInDb + currentQty; // максимально допустимое количество

                TxtStockHint.Text = $"Доступно на складе: {_currentStock} (включая текущее количество в заказе)";
                LblQuantity.Content = $"Количество * (макс. {_currentStock})";
            }
            catch (Exception ex)
            {
                TxtStockHint.Text = $"Не удалось загрузить остаток: {ex.Message}";
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            HideError();

            if (!int.TryParse(TxtQuantity.Text.Trim(), out int newQty) || newQty <= 0)
            {
                ShowError("Введите корректное количество (больше 0).");
                TxtQuantity.Focus();
                return;
            }

            if (newQty > _currentStock)
            {
                ShowError($"Недостаточно товара на складе. Максимально доступно: {_currentStock}.");
                return;
            }

            try
            {
                int oldQty = Convert.ToInt32(_row.Row["quantity"]);
                int diff = newQty - oldQty; // >0 — берём со склада, <0 — возвращаем на склад

                // Обновляем количество в позиции заказа
                DatabaseHelper.ExecuteNonQuery(
                    "UPDATE OrderItems SET quantity = @q WHERE id_order = @o AND id_product = @p",
                    new[]
                    {
                        new SqlParameter("@q", newQty),
                        new SqlParameter("@o", _orderId),
                        new SqlParameter("@p", _productId)
                    });

                // Корректируем остаток на складе
                DatabaseHelper.ExecuteNonQuery(
                    "UPDATE products SET stock = stock - @diff WHERE id_product = @p",
                    new[]
                    {
                        new SqlParameter("@diff", diff),
                        new SqlParameter("@p", _productId)
                    });

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка сохранения: {ex.Message}");
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e) => Close();

        private void ShowError(string msg)
        {
            TxtError.Text = msg;
            PanelError.Visibility = Visibility.Visible;
        }

        private void HideError() => PanelError.Visibility = Visibility.Collapsed;
    }
}
