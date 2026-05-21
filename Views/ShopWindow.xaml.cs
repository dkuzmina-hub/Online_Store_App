using System;
using System.IO;
using System.Windows.Media.Imaging;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Data.SqlClient;
using OnlineStoreApp.Data;

namespace OnlineStoreApp.Views
{
	public partial class ShopWindow : Window
	{
		private readonly List<CartItem> _cart = new();
		private bool _cartOpen = false;

		public ShopWindow()
		{
			InitializeComponent();
			TxtUser.Text = Session.UserName;
			Loaded += (_, _) => { LoadCategories(); LoadProducts(); };
		}

		private void LoadCategories()
		{
			CboCat.Items.Clear();
			CboCat.Items.Add(new ComboBoxItem { Content = "Все категории", IsSelected = true });
			var dt = DB.Query("SELECT id_category, name_category FROM category ORDER BY name_category");
			foreach (DataRow r in dt.Rows)
				CboCat.Items.Add(new ComboBoxItem
				{
					Content = r["name_category"].ToString(),
					Tag = r["id_category"]
				});
			CboCat.SelectedIndex = 0;
		}

		private void LoadProducts(string search = "", object? catId = null)
		{
			try
			{
				var dt = DatabaseHelper.ExecuteStoredProcedureWithResult("sp_GetProducts", new[]
				{
					new Microsoft.Data.SqlClient.SqlParameter("@search_field",  string.IsNullOrEmpty(search) ? (object)DBNull.Value : "name"),
					new Microsoft.Data.SqlClient.SqlParameter("@search_value",  string.IsNullOrEmpty(search) ? (object)DBNull.Value : search),
					new Microsoft.Data.SqlClient.SqlParameter("@id_category",   catId ?? (object)DBNull.Value),
					new Microsoft.Data.SqlClient.SqlParameter("@in_stock_only", 1)
				});

				CardsPanel.Children.Clear();
				TxtCount.Text = $"Товаров: {dt.Rows.Count}";

				if (dt.Rows.Count == 0)
				{
					CardsPanel.Children.Add(new TextBlock
					{
						Text = "Товары не найдены",
						FontSize = 16,
						Foreground = Brushes.Gray,
						FontFamily = new FontFamily("Segoe UI"),
						Margin = new Thickness(40, 60, 0, 0)
					});
					return;
				}

				foreach (DataRow r in dt.Rows)
					CardsPanel.Children.Add(BuildCard(r));
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Ошибка загрузки товаров: {ex.Message}",
					"Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
			}
		}

		private UIElement BuildCard(DataRow r)
		{
			string name = r["name_prod"].ToString()!;
			string desc = r["description"]?.ToString() ?? "";
			decimal price = Convert.ToDecimal(r["price"]);
			string category = r["name_category"].ToString()!;
			string sizeType = r.Table.Columns.Contains("size_type")
				? r["size_type"]?.ToString() ?? "none"
				: "none";
			int prodId = Convert.ToInt32(r["id_product"]);

			int totalStock = r.Table.Columns.Contains("total_stock")
				? Convert.ToInt32(r["total_stock"])
				: Convert.ToInt32(r["stock"]);

			bool hasSizes = sizeType is "clothing" or "shoes";
			var accent = CategoryColor(category);

			var card = new Border
			{
				Width = 210,
				Height = 380,
				Margin = new Thickness(0, 0, 14, 14),
				Background = Brushes.White,
				CornerRadius = new CornerRadius(12),
				BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
				BorderThickness = new Thickness(1),
				Cursor = Cursors.Hand,
				Effect = new System.Windows.Media.Effects.DropShadowEffect
				{ Color = Colors.Black, Opacity = 0.06, BlurRadius = 12, ShadowDepth = 2 }
			};

			card.MouseEnter += (_, _) => card.BorderBrush = new SolidColorBrush(Color.FromRgb(37, 99, 235));
			card.MouseLeave += (_, _) => card.BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240));
			card.MouseLeftButtonUp += (_, _) =>
			{
				var detail = new ProductDetailWindow(r) { Owner = this };
				detail.AddToCartRequested += (pid, pname, pprice, qty) =>
				{
					string? size = null;
					string cleanName = pname;
					const string sep = "||SIZE||";
					int sepIdx = pname.IndexOf(sep, StringComparison.Ordinal);
					if (sepIdx >= 0)
					{
						cleanName = pname[..sepIdx];
						size = pname[(sepIdx + sep.Length)..];
					}

					if (size != null)
					{
						string key = $"{pid}_{size}";
						var existing = _cart.FirstOrDefault(c => c.CartKey == key);
						if (existing != null)
						{
							existing.Qty = Math.Min(existing.Qty + qty, existing.MaxStock);
						}
						else
						{
							int sizeStock = GetSizeStock(pid, size);
							_cart.Add(new CartItem
							{
								ProductId = pid,
								Name = cleanName,
								Price = pprice,
								Qty = qty,
								MaxStock = sizeStock,
								Size = size
							});
						}
						RefreshCartUI();
						if (!_cartOpen) OpenCart();
					}
					else
					{
						var existing = _cart.FirstOrDefault(c => c.CartKey == $"{pid}");
						if (existing != null)
						{
							existing.Qty = Math.Min(existing.Qty + qty, existing.MaxStock);
						}
						else
						{
							_cart.Add(new CartItem
							{
								ProductId = pid,
								Name = cleanName,
								Price = pprice,
								Qty = qty,
								MaxStock = totalStock,
								Size = null
							});
						}
						RefreshCartUI();
						if (!_cartOpen) OpenCart();
					}
				};
				detail.ShowDialog();
			};

			var stack = new StackPanel();

			string? imgPath = r.Table.Columns.Contains("image_path")
				? r["image_path"]?.ToString() : null;

			UIElement bannerChild;
			if (!string.IsNullOrEmpty(imgPath) && File.Exists(imgPath))
			{
				try
				{
					var bmp = new BitmapImage();
					bmp.BeginInit();
					bmp.UriSource = new Uri(imgPath, UriKind.Absolute);
					bmp.CacheOption = BitmapCacheOption.OnLoad;
					bmp.DecodePixelWidth = 420;
					bmp.EndInit();
					bannerChild = new Image { Source = bmp, Stretch = Stretch.Uniform, MaxHeight = 140 };
				}
				catch { bannerChild = EmojiBlock(category); }
			}
			else { bannerChild = EmojiBlock(category); }

			stack.Children.Add(new Border
			{
				Background = string.IsNullOrEmpty(imgPath) || !File.Exists(imgPath ?? "")
					? accent : Brushes.White,
				CornerRadius = new CornerRadius(11, 11, 0, 0),
				Height = 140,
				ClipToBounds = true,
				Child = bannerChild
			});

			var content = new StackPanel { Margin = new Thickness(14, 10, 14, 10) };

			content.Children.Add(new Border
			{
				Background = accent,
				CornerRadius = new CornerRadius(4),
				Padding = new Thickness(6, 2, 6, 2),
				HorizontalAlignment = HorizontalAlignment.Left,
				Margin = new Thickness(0, 0, 0, 5),
				Child = new TextBlock
				{
					Text = category,
					FontSize = 10,
					FontWeight = FontWeights.SemiBold,
					FontFamily = new FontFamily("Segoe UI"),
					Foreground = Brushes.White
				}
			});

			if (hasSizes)
			{
				string sizeBadge = sizeType == "clothing"
					? "📏  XS–3XL"
					: "📏  35–45 EU";
				content.Children.Add(new Border
				{
					Background = new SolidColorBrush(Color.FromRgb(239, 246, 255)),
					CornerRadius = new CornerRadius(4),
					Padding = new Thickness(6, 2, 6, 2),
					HorizontalAlignment = HorizontalAlignment.Left,
					Margin = new Thickness(0, 0, 0, 5),
					Child = new TextBlock
					{
						Text = sizeBadge,
						FontSize = 10,
						FontFamily = new FontFamily("Segoe UI"),
						Foreground = new SolidColorBrush(Color.FromRgb(37, 99, 235))
					}
				});
			}

			content.Children.Add(new TextBlock
			{
				Text = name,
				FontSize = 13,
				FontWeight = FontWeights.SemiBold,
				FontFamily = new FontFamily("Segoe UI"),
				Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
				TextWrapping = TextWrapping.Wrap,
				MaxHeight = 38,
				Margin = new Thickness(0, 0, 0, 3)
			});

			if (!string.IsNullOrEmpty(desc))
				content.Children.Add(new TextBlock
				{
					Text = desc,
					FontSize = 10,
					FontFamily = new FontFamily("Segoe UI"),
					Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
					TextWrapping = TextWrapping.Wrap,
					MaxHeight = 28,
					Margin = new Thickness(0, 0, 0, 6)
				});

			var priceRow = new Grid { Margin = new Thickness(0, 4, 0, 8) };
			priceRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			priceRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

			var priceBlock = new TextBlock
			{
				Text = $"{price:N2} MDL",
				FontSize = 15,
				FontWeight = FontWeights.Bold,
				FontFamily = new FontFamily("Segoe UI"),
				Foreground = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
				VerticalAlignment = VerticalAlignment.Center
			};
			Grid.SetColumn(priceBlock, 0);

			var stockBlock = new TextBlock
			{
				Text = $"Остаток: {totalStock}",
				FontSize = 10,
				FontFamily = new FontFamily("Segoe UI"),
				Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
				VerticalAlignment = VerticalAlignment.Center
			};
			Grid.SetColumn(stockBlock, 1);

			priceRow.Children.Add(priceBlock);
			priceRow.Children.Add(stockBlock);
			content.Children.Add(priceRow);

			string btnText = hasSizes ? "📏  Выбрать размер" : "＋  В корзину";
			var btnAdd = new Button
			{
				Content = btnText,
				Height = 34,
				FontSize = 12,
				FontWeight = FontWeights.SemiBold,
				FontFamily = new FontFamily("Segoe UI"),
				Background = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
				Foreground = Brushes.White,
				BorderThickness = new Thickness(0),
				Cursor = Cursors.Hand
			};
			btnAdd.Click += (_, ev) =>
			{
				ev.Handled = true;  
				TryAddToCart(prodId, name, price, totalStock, sizeType);
			};

			content.Children.Add(btnAdd);
			stack.Children.Add(content);
			card.Child = stack;
			return card;
		}

		private void TryAddToCart(int productId, string name, decimal price, int totalStock, string sizeType)
		{
			if (sizeType is "clothing" or "shoes")
			{
			
				var picker = new SizePickerWindow(productId, name, sizeType) { Owner = this };
				if (picker.ShowDialog() != true) return;

				string size = picker.SelectedSize!;
				int qty = picker.SelectedQty;

				
				var existing = _cart.FirstOrDefault(c => c.CartKey == $"{productId}_{size}");
				if (existing != null)
				{
					existing.Qty = Math.Min(existing.Qty + qty, existing.MaxStock);
				}
				else
				{
					int sizeStock = GetSizeStock(productId, size);
					_cart.Add(new CartItem
					{
						ProductId = productId,
						Name = name,
						Price = price,
						Qty = qty,
						MaxStock = sizeStock,
						Size = size
					});
				}
			}
			else
			{
				
				var existing = _cart.FirstOrDefault(c => c.CartKey == $"{productId}");
				if (existing != null)
				{
					if (existing.Qty >= existing.MaxStock)
					{
						MessageBox.Show($"Максимум {existing.MaxStock} шт. в наличии.",
							"Корзина", MessageBoxButton.OK, MessageBoxImage.Information);
						return;
					}
					existing.Qty++;
				}
				else
				{
					_cart.Add(new CartItem
					{
						ProductId = productId,
						Name = name,
						Price = price,
						Qty = 1,
						MaxStock = totalStock,
						Size = null
					});
				}
			}

			RefreshCartUI();
			if (!_cartOpen) OpenCart();
		}

		private static int GetSizeStock(int productId, string size)
		{
			var dt = DB.Query(
				"SELECT stock FROM product_sizes WHERE id_product=@p AND size_label=@s",
				("@p", productId), ("@s", size));
			return dt.Rows.Count > 0 ? Convert.ToInt32(dt.Rows[0]["stock"]) : 1;
		}

		private void RefreshCartUI()
		{
			CartPanel.Children.Clear();

			if (_cart.Count == 0)
			{
				CartPanel.Children.Add(new TextBlock
				{
					Text = "Корзина пуста",
					FontSize = 13,
					Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
					FontFamily = new FontFamily("Segoe UI"),
					HorizontalAlignment = HorizontalAlignment.Center,
					Margin = new Thickness(0, 40, 0, 0)
				});
				TxtTotal.Text = "0 MDL";
				TxtItemsCount.Text = "";
				CartBadge.Visibility = Visibility.Collapsed;
				return;
			}

			foreach (var item in _cart.ToList())
			{
				var row = new Border
				{
					Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
					CornerRadius = new CornerRadius(8),
					Margin = new Thickness(12, 4, 12, 4),
					Padding = new Thickness(12, 10, 12, 10),
					BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
					BorderThickness = new Thickness(1)
				};

				var grid = new Grid();
				grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
				grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

				// Name + size + remove button
				var nameRow = new Grid();
				nameRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
				nameRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

				// DisplayName уже включает размер: "Nike Air Max  (42)"
				var nameTb = new TextBlock
				{
					Text = item.DisplayName,
					FontSize = 12,
					FontWeight = FontWeights.SemiBold,
					FontFamily = new FontFamily("Segoe UI"),
					Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
					TextWrapping = TextWrapping.Wrap
				};
				Grid.SetColumn(nameTb, 0);

				var capturedItem = item;
				var btnRemove = new Button
				{
					Content = "✕",
					Width = 22,
					Height = 22,
					FontSize = 11,
					Background = Brushes.Transparent,
					Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
					BorderThickness = new Thickness(0),
					Cursor = Cursors.Hand
				};
				btnRemove.Click += (_, _) => { _cart.Remove(capturedItem); RefreshCartUI(); };
				Grid.SetColumn(btnRemove, 1);

				nameRow.Children.Add(nameTb);
				nameRow.Children.Add(btnRemove);
				Grid.SetRow(nameRow, 0);
				grid.Children.Add(nameRow);

				// Qty controls
				var qtyRow = new Grid { Margin = new Thickness(0, 6, 0, 0) };
				qtyRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
				qtyRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

				var qtyPanel = new StackPanel { Orientation = Orientation.Horizontal };
				var btnMinus = MakeQtyBtn("−", Color.FromRgb(226, 232, 240), Color.FromRgb(51, 65, 85));
				var qtyLabel = new TextBlock
				{
					Text = item.Qty.ToString(),
					Width = 32,
					FontSize = 13,
					FontWeight = FontWeights.SemiBold,
					FontFamily = new FontFamily("Segoe UI"),
					TextAlignment = TextAlignment.Center,
					VerticalAlignment = VerticalAlignment.Center
				};
				var btnPlus = MakeQtyBtn("＋", Color.FromRgb(37, 99, 235), Colors.White);

				btnMinus.Click += (_, _) =>
				{
					if (capturedItem.Qty > 1) { capturedItem.Qty--; RefreshCartUI(); }
					else { _cart.Remove(capturedItem); RefreshCartUI(); }
				};
				btnPlus.Click += (_, _) =>
				{
					if (capturedItem.Qty < capturedItem.MaxStock) { capturedItem.Qty++; RefreshCartUI(); }
					else MessageBox.Show($"Максимум {capturedItem.MaxStock} шт.",
						"Корзина", MessageBoxButton.OK, MessageBoxImage.Information);
				};

				qtyPanel.Children.Add(btnMinus);
				qtyPanel.Children.Add(qtyLabel);
				qtyPanel.Children.Add(btnPlus);
				Grid.SetColumn(qtyPanel, 0);

				var subtotalTb = new TextBlock
				{
					Text = $"{item.Subtotal:N2} MDL",
					FontSize = 12,
					FontWeight = FontWeights.Bold,
					FontFamily = new FontFamily("Segoe UI"),
					Foreground = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
					HorizontalAlignment = HorizontalAlignment.Right,
					VerticalAlignment = VerticalAlignment.Center
				};
				Grid.SetColumn(subtotalTb, 1);

				qtyRow.Children.Add(qtyPanel);
				qtyRow.Children.Add(subtotalTb);
				Grid.SetRow(qtyRow, 1);
				grid.Children.Add(qtyRow);

				row.Child = grid;
				CartPanel.Children.Add(row);
			}

			decimal total = _cart.Sum(c => c.Subtotal);
			int count = _cart.Sum(c => c.Qty);
			TxtTotal.Text = $"{total:N2} MDL";
			TxtItemsCount.Text = $"{count} поз. в корзине";
			TxtCartCount.Text = count.ToString();
			CartBadge.Visibility = Visibility.Visible;
		}

		private static Button MakeQtyBtn(string text, Color bg, Color fg) => new()
		{
			Content = text,
			Width = 26,
			Height = 26,
			FontSize = 14,
			Background = new SolidColorBrush(bg),
			Foreground = new SolidColorBrush(fg),
			BorderThickness = new Thickness(0),
			Cursor = Cursors.Hand
		};

		
		private void BtnCheckout_Click(object sender, RoutedEventArgs e)
		{
			if (_cart.Count == 0)
			{
				MessageBox.Show("Корзина пуста.", "Оформление заказа",
					MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}
			var checkout = new CheckoutWindow(_cart) { Owner = this };
			if (checkout.ShowDialog() == true)
			{
				_cart.Clear();
				RefreshCartUI();
				LoadProducts();
			}
		}

		private void BtnCart_Click(object sender, RoutedEventArgs e)
		{
			if (_cartOpen) CloseCart(); else OpenCart();
		}

		private void BtnCloseCart_Click(object sender, RoutedEventArgs e) => CloseCart();

		private void OpenCart() { CartColumn.Width = new GridLength(320); _cartOpen = true; RefreshCartUI(); }
		private void CloseCart() { CartColumn.Width = new GridLength(0); _cartOpen = false; }

		private void BtnClearCart_Click(object sender, RoutedEventArgs e)
		{
			if (_cart.Count == 0) return;
			if (MessageBox.Show("Очистить корзину?", "Корзина",
					MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
			{ _cart.Clear(); RefreshCartUI(); }
		}

		private void BtnMyOrders_Click(object sender, RoutedEventArgs e)
			=> new MyOrdersWindow { Owner = this }.ShowDialog();

		private void BtnSearch_Click(object sender, RoutedEventArgs e)
		{
			var catItem = CboCat.SelectedItem as ComboBoxItem;
			LoadProducts(TxtSearch.Text.Trim(), catItem?.Tag);
		}

		private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
		{
			var catItem = CboCat.SelectedItem as ComboBoxItem;
			LoadProducts(TxtSearch.Text.Trim(), catItem?.Tag);
		}


		private void BtnReset_Click(object sender, RoutedEventArgs e)
		{
			TxtSearch.Text = "";
			CboCat.SelectedIndex = 0;
			LoadProducts();
		}

		private void BtnLogout_Click(object sender, RoutedEventArgs e)
		{
			Session.Clear();
			new LoginWindow().Show();
			Close();
		}

		
		private static TextBlock EmojiBlock(string cat) => new()
		{
			Text = CategoryEmoji(cat),
			FontSize = 38,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(0, 10, 0, 0)
		};

		private static Brush CategoryColor(string cat) => cat switch
		{
			"Clothing" or "Одежда" or "Îmbrăcăminte" =>
				new SolidColorBrush(Color.FromRgb(99, 102, 241)),
			"Shoes" or "Обувь" or "Încălțăminte" =>
				new SolidColorBrush(Color.FromRgb(236, 72, 153)),
			"Accessories" => new SolidColorBrush(Color.FromRgb(245, 158, 11)),
			"Electronics" => new SolidColorBrush(Color.FromRgb(37, 99, 235)),
			"Beauty" => new SolidColorBrush(Color.FromRgb(217, 70, 239)),
			"Home" => new SolidColorBrush(Color.FromRgb(20, 184, 166)),
			"Sports" => new SolidColorBrush(Color.FromRgb(16, 185, 129)),
			"Kids" => new SolidColorBrush(Color.FromRgb(251, 146, 60)),
			"Books" => new SolidColorBrush(Color.FromRgb(139, 92, 246)),
			"Food" => new SolidColorBrush(Color.FromRgb(34, 197, 94)),
			_ => new SolidColorBrush(Color.FromRgb(100, 116, 139))
		};

		private static string CategoryEmoji(string cat) => cat switch
		{
			"Clothing" or "Одежда" or "Îmbrăcăminte" => "👕",
			"Shoes" or "Обувь" or "Încălțăminte" => "👟",
			"Accessories" => "⌚",
			"Electronics" => "📱",
			"Beauty" => "💄",
			"Home" => "🏠",
			"Sports" => "⚽",
			"Kids" => "🧸",
			"Books" => "📚",
			"Food" => "🍎",
			_ => "📦"
		};
	}
}