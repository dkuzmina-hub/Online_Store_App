using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OnlineStoreApp.Data;

namespace OnlineStoreApp.Views
{
	public partial class MyOrdersWindow : Window
	{
		public MyOrdersWindow()
		{
			InitializeComponent();
			Loaded += (_, _) => LoadOrders();
		}

		private void LoadOrders()
		{
			OrdersPanel.Children.Clear();

			try
			{
				var dtOrders = DB.Query(
					"EXEC sp_GetUserOrders @id_user",
					("@id_user", Session.UserId));

				if (dtOrders.Rows.Count == 0)
				{
					OrdersPanel.Children.Add(new TextBlock
					{
						Text = "У вас пока нет заказов.",
						FontSize = 15,
						Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
						FontFamily = new FontFamily("Segoe UI"),
						HorizontalAlignment = HorizontalAlignment.Center,
						Margin = new Thickness(0, 60, 0, 0)
					});
					TxtSubtitle.Text = "нет заказов";
					TxtTotal.Text = "";
					return;
				}

				decimal grandTotal = 0;

				foreach (DataRow order in dtOrders.Rows)
				{
					int orderId = Convert.ToInt32(order["id_order"]);
					string dateStr = Convert.ToDateTime(order["date"]).ToString("dd.MM.yyyy");
					string transStatus = order["trans_status"]?.ToString() ?? "";
					string paymStatus = order["paym_status"]?.ToString() ?? "";

					var dtItems = DB.Query(
						"EXEC sp_GetOrderItems @id_order",
						("@id_order", orderId));

					decimal orderTotal = 0;
					foreach (DataRow item in dtItems.Rows)
						orderTotal += Convert.ToDecimal(item["subtotal"]);
					grandTotal += orderTotal;

					OrdersPanel.Children.Add(BuildOrderCard(
						orderId, dateStr, transStatus, paymStatus, orderTotal, dtItems));
				}

				TxtSubtitle.Text = $"{dtOrders.Rows.Count} заказов";
				TxtTotal.Text = $"Итого по всем заказам: {grandTotal:N2} MDL";
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Ошибка загрузки заказов: {ex.Message}",
					"Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		private static UIElement BuildOrderCard(
			int orderId, string date, string transStatus, string paymStatus,
			decimal total, DataTable items)
		{
			var card = new Border
			{
				Background = Brushes.White,
				CornerRadius = new CornerRadius(12),
				BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
				BorderThickness = new Thickness(1),
				Margin = new Thickness(0, 0, 0, 14),
				Effect = new System.Windows.Media.Effects.DropShadowEffect
				{ Color = Colors.Black, Opacity = 0.04, BlurRadius = 10, ShadowDepth = 2 }
			};

			var outer = new StackPanel();

			// ── Шапка карточки ───────────────────────────────────────────────
			var header = new Border
			{
				Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
				CornerRadius = new CornerRadius(12, 12, 0, 0),
				Padding = new Thickness(20, 14, 20, 14),
				BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
				BorderThickness = new Thickness(0, 0, 0, 1)
			};
			var headerGrid = new Grid();
			headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

			var leftInfo = new StackPanel { Orientation = Orientation.Horizontal };
			leftInfo.Children.Add(new TextBlock
			{
				Text = $"Заказ №{orderId}",
				FontSize = 15,
				FontWeight = FontWeights.Bold,
				FontFamily = new FontFamily("Segoe UI"),
				Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
				VerticalAlignment = VerticalAlignment.Center,
				Margin = new Thickness(0, 0, 16, 0)
			});
			leftInfo.Children.Add(new TextBlock
			{
				Text = $"🗓 {date}",
				FontSize = 12,
				FontFamily = new FontFamily("Segoe UI"),
				Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
				VerticalAlignment = VerticalAlignment.Center,
				Margin = new Thickness(0, 0, 16, 0)
			});
			leftInfo.Children.Add(StatusBadge(transStatus, isDelivery: true));
			leftInfo.Children.Add(StatusBadge(paymStatus, isDelivery: false));
			Grid.SetColumn(leftInfo, 0);

			var totalBlock = new TextBlock
			{
				Text = $"{total:N2} MDL",
				FontSize = 17,
				FontWeight = FontWeights.Bold,
				FontFamily = new FontFamily("Segoe UI"),
				Foreground = new SolidColorBrush(Color.FromRgb(22, 163, 74)),
				VerticalAlignment = VerticalAlignment.Center
			};
			Grid.SetColumn(totalBlock, 1);

			headerGrid.Children.Add(leftInfo);
			headerGrid.Children.Add(totalBlock);
			header.Child = headerGrid;
			outer.Children.Add(header);

			// ── Позиции заказа ────────────────────────────────────────────────
			var itemsPanel = new StackPanel { Margin = new Thickness(20, 12, 20, 12) };

			if (items.Rows.Count == 0)
			{
				itemsPanel.Children.Add(new TextBlock
				{
					Text = "Позиции не найдены",
					FontSize = 12,
					FontFamily = new FontFamily("Segoe UI"),
					Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184))
				});
			}
			else
			{
				bool hasSize = items.Columns.Contains("size_label");

				for (int i = 0; i < items.Rows.Count; i++)
				{
					DataRow item = items.Rows[i];

					string name = item["name_prod"].ToString()!;
					int qty = Convert.ToInt32(item["quantity"]);
					decimal price = Convert.ToDecimal(item["price"]);
					decimal subtotal = Convert.ToDecimal(item["subtotal"]);

					// Размер — если есть колонка и значение не пустое
					string? sizeLabel = hasSize ? item["size_label"]?.ToString() : null;
					bool hasThisSize = !string.IsNullOrWhiteSpace(sizeLabel);

					// Строка с названием и, если нужно, бейджем размера
					var namePanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
					namePanel.Children.Add(new TextBlock
					{
						Text = $"  {name}",
						FontSize = 12,
						FontFamily = new FontFamily("Segoe UI"),
						Foreground = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
						VerticalAlignment = VerticalAlignment.Center
					});

					if (hasThisSize)
					{
						namePanel.Children.Add(new Border
						{
							Background = new SolidColorBrush(Color.FromRgb(239, 246, 255)),
							BorderBrush = new SolidColorBrush(Color.FromRgb(191, 219, 254)),
							BorderThickness = new Thickness(1),
							CornerRadius = new CornerRadius(4),
							Padding = new Thickness(6, 2, 6, 2),
							Margin = new Thickness(8, 0, 0, 0),
							VerticalAlignment = VerticalAlignment.Center,
							Child = new TextBlock
							{
								Text = $"📏 {sizeLabel}",
								FontSize = 10,
								FontWeight = FontWeights.SemiBold,
								FontFamily = new FontFamily("Segoe UI"),
								Foreground = new SolidColorBrush(Color.FromRgb(37, 99, 235))
							}
						});
					}

					var itemRow = new Grid { Margin = new Thickness(0, 3, 0, 3) };
					itemRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
					itemRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
					itemRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110, GridUnitType.Pixel) });

					Grid.SetColumn(namePanel, 0);

					var qtyTb = new TextBlock
					{
						Text = $"× {qty}  @  {price:N2}",
						FontSize = 11,
						FontFamily = new FontFamily("Segoe UI"),
						Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
						VerticalAlignment = VerticalAlignment.Center,
						Margin = new Thickness(0, 0, 12, 0)
					};
					Grid.SetColumn(qtyTb, 1);

					var subTb = new TextBlock
					{
						Text = $"{subtotal:N2} MDL",
						FontSize = 12,
						FontWeight = FontWeights.SemiBold,
						FontFamily = new FontFamily("Segoe UI"),
						Foreground = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
						HorizontalAlignment = HorizontalAlignment.Right,
						VerticalAlignment = VerticalAlignment.Center
					};
					Grid.SetColumn(subTb, 2);

					itemRow.Children.Add(namePanel);
					itemRow.Children.Add(qtyTb);
					itemRow.Children.Add(subTb);
					itemsPanel.Children.Add(itemRow);

					if (i < items.Rows.Count - 1)
						itemsPanel.Children.Add(new Border
						{
							Height = 1,
							Background = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
							Margin = new Thickness(0, 2, 0, 2)
						});
				}
			}

			outer.Children.Add(itemsPanel);
			card.Child = outer;
			return card;
		}

		private static Border StatusBadge(string status, bool isDelivery)
		{
			var (bg, fg, label) = isDelivery
				? status switch
				{
					"pending" => ("#FEF3C7", "#92400E", "⏳ Ожидание"),
					"shipped" => ("#DBEAFE", "#1E40AF", "🚚 Отправлен"),
					"completed" => ("#DCFCE7", "#166534", "✅ Выполнен"),
					"cancelled" => ("#FEE2E2", "#991B1B", "❌ Отменён"),
					_ => ("#F1F5F9", "#475569", status)
				}
				: status switch
				{
					"unpaid" => ("#FEF3C7", "#92400E", "💳 Не оплачен"),
					"paid" => ("#DCFCE7", "#166534", "✅ Оплачен"),
					"refunded" => ("#F3E8FF", "#6B21A8", "↩ Возврат"),
					_ => ("#F1F5F9", "#475569", status)
				};

			return new Border
			{
				Background = (Brush)new BrushConverter().ConvertFromString(bg)!,
				CornerRadius = new CornerRadius(5),
				Padding = new Thickness(8, 3, 8, 3),
				Margin = new Thickness(0, 0, 8, 0),
				VerticalAlignment = VerticalAlignment.Center,
				Child = new TextBlock
				{
					Text = label,
					FontSize = 11,
					FontWeight = FontWeights.SemiBold,
					FontFamily = new FontFamily("Segoe UI"),
					Foreground = (Brush)new BrushConverter().ConvertFromString(fg)!
				}
			};
		}

		private void BtnRefresh_Click(object sender, RoutedEventArgs e) => LoadOrders();
		private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
	}
}