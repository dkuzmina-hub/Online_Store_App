using System;
using System.Collections.Generic;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OnlineStoreApp.Data;

namespace OnlineStoreApp.Views
{
	public partial class SizePickerWindow : Window
	{
		// Результат — выбранный размер и количество
		public string? SelectedSize { get; private set; }
		public int SelectedQty { get; private set; } = 1;

		private readonly int _productId;
		private readonly int _maxStock;     // макс. остаток для выбранного размера
		private int _qty = 1;

		// stock по каждому размеру: label → stock
		private readonly Dictionary<string, int> _sizeStock = new();
		private Button? _activeBtn;

		public SizePickerWindow(int productId, string productName, string sizeType)
		{
			InitializeComponent();
			_productId = productId;

			// Очищаем имя от кода размера, если он уже был встроен
			const string sizeSep = "||SIZE||";
			int si = productName.IndexOf(sizeSep, StringComparison.Ordinal);
			TxtProductName.Text = si >= 0 ? productName[..si] : productName;
			TxtSizeType.Text = sizeType == "clothing"
				? "Одежда — выберите размер: XS / S / M / L / XL / XXL"
				: "Обувь — выберите размер (EU)";

			LoadSizes();
		}

		private void LoadSizes()
		{
			// EXEC sp_GetProductSizes @id_product — возвращает size_label, stock
			var dt = DB.Query("EXEC sp_GetProductSizes @id_product",
				("@id_product", _productId));

			if (dt.Rows.Count == 0)
			{
				SizesPanel.Children.Add(new TextBlock
				{
					Text = "Нет доступных размеров.",
					FontSize = 12,
					Foreground = Brushes.Gray,
					FontFamily = new FontFamily("Segoe UI")
				});
				return;
			}

			foreach (DataRow row in dt.Rows)
			{
				string label = row["size_label"].ToString()!;
				int stock = Convert.ToInt32(row["stock"]);
				_sizeStock[label] = stock;

				var btn = new Button
				{
					Content = label,
					Width = 52,
					Height = 44,
					Margin = new Thickness(0, 0, 8, 8),
					FontSize = 13,
					FontFamily = new FontFamily("Segoe UI"),
					FontWeight = FontWeights.SemiBold,
					Background = Brushes.White,
					Foreground = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
					BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
					BorderThickness = new Thickness(1.5),
					Cursor = System.Windows.Input.Cursors.Hand,
					ToolTip = $"Остаток: {stock} шт.",
					Tag = label
				};

				btn.Click += SizeBtn_Click;
				SizesPanel.Children.Add(btn);
			}
		}

		private void SizeBtn_Click(object sender, RoutedEventArgs e)
		{
			if (sender is not Button btn) return;

			// Сброс предыдущей выбранной кнопки
			if (_activeBtn != null)
			{
				_activeBtn.Background = Brushes.White;
				_activeBtn.Foreground = new SolidColorBrush(Color.FromRgb(30, 41, 59));
				_activeBtn.BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225));
				_activeBtn.BorderThickness = new Thickness(1.5);
			}

			// Выделяем новую
			btn.Background = new SolidColorBrush(Color.FromRgb(37, 99, 235));
			btn.Foreground = Brushes.White;
			btn.BorderBrush = new SolidColorBrush(Color.FromRgb(37, 99, 235));
			btn.BorderThickness = new Thickness(2);
			_activeBtn = btn;

			SelectedSize = btn.Tag?.ToString();

			// Ограничиваем количество остатком
			int maxForSize = _sizeStock.TryGetValue(SelectedSize!, out int s) ? s : 1;
			if (_qty > maxForSize) { _qty = maxForSize; TxtQty.Text = _qty.ToString(); }

			PanelErr.Visibility = Visibility.Collapsed;
		}

		private void BtnMinus_Click(object sender, RoutedEventArgs e)
		{
			if (_qty > 1) { _qty--; TxtQty.Text = _qty.ToString(); }
		}

		private void BtnPlus_Click(object sender, RoutedEventArgs e)
		{
			int maxForSize = SelectedSize != null && _sizeStock.TryGetValue(SelectedSize, out int s) ? s : 99;
			if (_qty < maxForSize) { _qty++; TxtQty.Text = _qty.ToString(); }
			else ShowErr($"Максимум {maxForSize} шт. в наличии для размера {SelectedSize}.");
		}

		private void BtnAdd_Click(object sender, RoutedEventArgs e)
		{
			if (SelectedSize == null)
			{
				ShowErr("Пожалуйста, выберите размер.");
				return;
			}
			SelectedQty = _qty;
			DialogResult = true;
			Close();
		}

		private void BtnCancel_Click(object sender, RoutedEventArgs e) => Close();

		private void ShowErr(string m) { TxtErr.Text = m; PanelErr.Visibility = Visibility.Visible; }
	}
}