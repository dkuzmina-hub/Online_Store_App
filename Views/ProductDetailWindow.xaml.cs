using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OnlineStoreApp.Data;

namespace OnlineStoreApp.Views
{
	public partial class ProductDetailWindow : Window
	{
		private readonly int _productId;
		private readonly string _productName;
		private readonly decimal _price;
		private int _maxStock;       // меняется когда выбирается размер
		private int _qty = 1;

		// Тип размера: "clothing", "shoes", "none"
		private string _sizeType = "none";

		// Выбранный размер (null = не выбран)
		private string? _selectedSize = null;

		// Остатки по размерам: label → stock
		private readonly Dictionary<string, int> _sizeStock = new();

		// Кнопка текущего выбранного размера
		private Button? _activeSizeBtn;

		// Callback для ShopWindow
		public event Action<int, string, decimal, int>? AddToCartRequested;

		public ProductDetailWindow(DataRow product)
		{
			InitializeComponent();
			_productId = Convert.ToInt32(product["id_product"]);
			_productName = product["name_prod"].ToString()!;
			_price = Convert.ToDecimal(product["price"]);

			// total_stock — суммарный остаток (может быть из sp_GetProducts)
			_maxStock = product.Table.Columns.Contains("total_stock")
				? Convert.ToInt32(product["total_stock"])
				: Convert.ToInt32(product["stock"]);

			// Тип размера уже пришёл из ShopWindow (поле size_type)
			_sizeType = product.Table.Columns.Contains("size_type")
				? product["size_type"]?.ToString() ?? "none"
				: "none";

			LoadProduct(product);
			LoadSizes();
			LoadReviews();
		}

		// ── Load product info ─────────────────────────────────────────────────
		private void LoadProduct(DataRow r)
		{
			string name = r["name_prod"].ToString()!;
			string desc = r["description"]?.ToString() ?? "Описание не указано.";
			string category = r["name_category"].ToString()!;
			string? imgPath = r.Table.Columns.Contains("image_path")
				? r["image_path"]?.ToString() : null;

			TxtWindowTitle.Text = name;
			TxtName.Text = name;
			TxtDesc.Text = desc;
			TxtCategory.Text = category;
			TxtPrice.Text = $"{_price:N2} MDL";
			UpdateStockLabel();
			BtnAddCart.IsEnabled = _maxStock > 0;

			var accent = CategoryColor(category);
			BadgeCat.Background = accent;
			TxtEmoji.Text = CategoryEmoji(category);
			TxtEmoji.Foreground = Brushes.White;

			if (!string.IsNullOrEmpty(imgPath) && File.Exists(imgPath))
			{
				try
				{
					var bmp = new BitmapImage();
					bmp.BeginInit();
					bmp.UriSource = new Uri(imgPath);
					bmp.CacheOption = BitmapCacheOption.OnLoad;
					bmp.DecodePixelWidth = 600;
					bmp.EndInit();
					ImgProduct.Source = bmp;
					ImgProduct.Stretch = Stretch.Uniform;
					ImgProduct.HorizontalAlignment = HorizontalAlignment.Center;
					ImgProduct.VerticalAlignment = VerticalAlignment.Center;
					ImgProduct.Visibility = Visibility.Visible;
					TxtEmoji.Visibility = Visibility.Collapsed;
				}
				catch { }
			}

			BuildCharacteristics(name, category, _price, _maxStock);
		}

		private void UpdateStockLabel()
		{
			if (_sizeType is "clothing" or "shoes")
			{
				// Пока размер не выбран — показываем суммарный остаток
				TxtStock.Text = _selectedSize == null
					? $"Доступно размеров: {_sizeStock.Count}"
					: (_maxStock > 0 ? $"Размер {_selectedSize}: {_maxStock} шт." : "Нет в наличии");
			}
			else
			{
				TxtStock.Text = _maxStock > 0
					? $"В наличии: {_maxStock} шт."
					: "Нет в наличии";
			}
		}

		// ── Load sizes inline ─────────────────────────────────────────────────
		private void LoadSizes()
		{
			if (_sizeType is not ("clothing" or "shoes"))
			{
				PanelSizes.Visibility = Visibility.Collapsed;
				return;
			}

			PanelSizes.Visibility = Visibility.Visible;

			// Загружаем доступные размеры из product_sizes
			try
			{
				var dt = DB.Query("EXEC sp_GetProductSizes @id_product",
					("@id_product", _productId));

				if (dt.Rows.Count == 0)
				{
					// Нет размеров — скрываем блок
					PanelSizes.Visibility = Visibility.Collapsed;
					return;
				}

				SizesPanel.Children.Clear();
				foreach (DataRow row in dt.Rows)
				{
					string label = row["size_label"].ToString()!;
					int stock = Convert.ToInt32(row["stock"]);
					_sizeStock[label] = stock;

					var btn = new Button
					{
						Content = label,
						Style = (Style)FindResource("SizeBtn"),
						Tag = label,
						ToolTip = $"Остаток: {stock} шт."
					};
					btn.Click += SizeBtn_Click;
					SizesPanel.Children.Add(btn);
				}

				// Пока размер не выбран — кнопку «В корзину» блокируем
				BtnAddCart.IsEnabled = false;
			}
			catch
			{
				// sp_GetProductSizes не найдена — скрываем блок
				PanelSizes.Visibility = Visibility.Collapsed;
			}
		}

		private void SizeBtn_Click(object sender, RoutedEventArgs e)
		{
			if (sender is not Button btn) return;

			// Сброс предыдущей кнопки
			if (_activeSizeBtn != null)
			{
				_activeSizeBtn.Background = Brushes.White;
				_activeSizeBtn.Foreground = new SolidColorBrush(Color.FromRgb(30, 41, 59));
				_activeSizeBtn.BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225));
				_activeSizeBtn.BorderThickness = new Thickness(1.5);
			}

			// Выделяем выбранную
			btn.Background = new SolidColorBrush(Color.FromRgb(37, 99, 235));
			btn.Foreground = Brushes.White;
			btn.BorderBrush = new SolidColorBrush(Color.FromRgb(37, 99, 235));
			btn.BorderThickness = new Thickness(2);
			_activeSizeBtn = btn;

			_selectedSize = btn.Tag?.ToString();

			// Обновляем остаток и метку
			_maxStock = _sizeStock.TryGetValue(_selectedSize!, out int s) ? s : 0;
			TxtSelectedSize.Text = _selectedSize;
			PanelSizeWarn.Visibility = Visibility.Collapsed;

			// Если выбранный размер закончился — ограничиваем qty
			if (_qty > _maxStock && _maxStock > 0) { _qty = _maxStock; TxtQty.Text = _qty.ToString(); }

			UpdateStockLabel();
			BtnAddCart.IsEnabled = _maxStock > 0;
		}

		// ── Characteristics ───────────────────────────────────────────────────
		private void BuildCharacteristics(string name, string category, decimal price, int stock)
		{
			CharGrid.RowDefinitions.Clear();
			CharGrid.ColumnDefinitions.Clear();
			CharGrid.Children.Clear();

			CharGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			CharGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

			var rows = new[]
			{
				("Категория",  category),
				("Цена",       $"{price:N2} MDL"),
				("На складе",  $"{stock} шт."),
				("Состояние",  "Новый"),
				("Доставка",   "По всей Молдове"),
				("Гарантия",   "14 дней возврата")
			};

			for (int i = 0; i < rows.Length; i++)
			{
				CharGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
				var bg = i % 2 == 0
					? new SolidColorBrush(Color.FromRgb(248, 250, 252))
					: Brushes.White;

				var lblCell = new Border { Background = bg, Padding = new Thickness(8, 6, 4, 6) };
				lblCell.Child = new TextBlock
				{
					Text = rows[i].Item1,
					FontSize = 12,
					FontFamily = new FontFamily("Segoe UI"),
					Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
					FontWeight = FontWeights.SemiBold
				};
				Grid.SetRow(lblCell, i); Grid.SetColumn(lblCell, 0);

				var valCell = new Border { Background = bg, Padding = new Thickness(4, 6, 8, 6) };
				valCell.Child = new TextBlock
				{
					Text = rows[i].Item2,
					FontSize = 12,
					FontFamily = new FontFamily("Segoe UI"),
					Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
					TextWrapping = TextWrapping.Wrap
				};
				Grid.SetRow(valCell, i); Grid.SetColumn(valCell, 1);

				CharGrid.Children.Add(lblCell);
				CharGrid.Children.Add(valCell);
			}
		}

		// ── Reviews ───────────────────────────────────────────────────────────
		private void LoadReviews()
		{
			ReviewsPanel.Children.Clear();
			try
			{
				var dt = DB.Query("EXEC sp_GetReviews @id_product",
					("@id_product", _productId));

				if (dt.Rows.Count == 0)
				{
					NoReviews.Visibility = Visibility.Visible;
					TxtStars.Text = "☆☆☆☆☆";
					TxtRatingNum.Text = "0.0";
					TxtReviewCount.Text = "(нет отзывов)";
					return;
				}

				NoReviews.Visibility = Visibility.Collapsed;
				double sum = 0;
				foreach (DataRow r in dt.Rows) sum += Convert.ToInt32(r["rating"]);
				double avg = sum / dt.Rows.Count;
				TxtStars.Text = RatingToStars(avg);
				TxtRatingNum.Text = $"{avg:F1}";
				TxtReviewCount.Text = $"({dt.Rows.Count} {ReviewWord(dt.Rows.Count)})";

				foreach (DataRow r in dt.Rows)
					ReviewsPanel.Children.Add(BuildReviewCard(r));
			}
			catch
			{
				NoReviews.Visibility = Visibility.Visible;
				TxtStars.Text = "☆☆☆☆☆";
				TxtRatingNum.Text = "—";
				TxtReviewCount.Text = "(таблица отзывов не создана)";
			}
		}

		private UIElement BuildReviewCard(DataRow r)
		{
			int rating = Convert.ToInt32(r["rating"]);
			string comment = r["comment"]?.ToString() ?? "";
			string username = r["username"].ToString()!;
			string date = r["rev_date"].ToString()!;
			int reviewId = Convert.ToInt32(r["id_review"]);
			int authorId = Convert.ToInt32(r["id_user"]);

			var card = new Border
			{
				Background = Brushes.White,
				BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
				BorderThickness = new Thickness(1),
				CornerRadius = new CornerRadius(8),
				Padding = new Thickness(14, 12, 14, 12),
				Margin = new Thickness(0, 0, 0, 8)
			};
			var stack = new StackPanel();

			var topRow = new Grid();
			topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
			topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

			var avatar = new Border
			{
				Width = 36,
				Height = 36,
				CornerRadius = new CornerRadius(18),
				Background = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
				Margin = new Thickness(0, 0, 10, 0),
				VerticalAlignment = VerticalAlignment.Center
			};
			avatar.Child = new TextBlock
			{
				Text = username.Length > 0 ? username[0].ToString().ToUpper() : "?",
				FontSize = 15,
				FontWeight = FontWeights.Bold,
				FontFamily = new FontFamily("Segoe UI"),
				Foreground = Brushes.White,
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center
			};
			Grid.SetColumn(avatar, 0);

			var nameStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
			nameStack.Children.Add(new TextBlock
			{
				Text = username,
				FontSize = 13,
				FontWeight = FontWeights.SemiBold,
				FontFamily = new FontFamily("Segoe UI"),
				Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42))
			});
			nameStack.Children.Add(new TextBlock
			{
				Text = date,
				FontSize = 11,
				FontFamily = new FontFamily("Segoe UI"),
				Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139))
			});
			Grid.SetColumn(nameStack, 1);

			var btnPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Top };
			if (Session.IsAdmin || Session.UserId == authorId)
			{
				var btnDel = new Button
				{
					Content = "✕",
					FontSize = 12,
					Background = Brushes.Transparent,
					BorderThickness = new Thickness(0),
					Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
					Cursor = System.Windows.Input.Cursors.Hand,
					ToolTip = "Удалить отзыв",
					Tag = reviewId
				};
				btnDel.MouseEnter += (_, _) => btnDel.Foreground = new SolidColorBrush(Color.FromRgb(220, 38, 38));
				btnDel.MouseLeave += (_, _) => btnDel.Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225));
				btnDel.Click += DeleteReview_Click;
				btnPanel.Children.Add(btnDel);
			}
			Grid.SetColumn(btnPanel, 2);

			topRow.Children.Add(avatar);
			topRow.Children.Add(nameStack);
			topRow.Children.Add(btnPanel);
			stack.Children.Add(topRow);

			stack.Children.Add(new TextBlock { Text = RatingToStars(rating), FontSize = 16, Margin = new Thickness(0, 8, 0, 4) });

			if (!string.IsNullOrEmpty(comment))
				stack.Children.Add(new TextBlock
				{
					Text = comment,
					FontSize = 13,
					FontFamily = new FontFamily("Segoe UI"),
					Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
					TextWrapping = TextWrapping.Wrap
				});

			card.Child = stack;
			return card;
		}

		private void BtnWriteReview_Click(object sender, RoutedEventArgs e)
		{
			var w = new WriteReviewWindow(_productId, _productName) { Owner = this };
			if (w.ShowDialog() == true) LoadReviews();
		}

		private void DeleteReview_Click(object sender, RoutedEventArgs e)
		{
			int reviewId = (int)((Button)sender).Tag;
			if (MessageBox.Show("Удалить этот отзыв?", "Подтверждение",
				MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
			try
			{
				DB.Exec("EXEC sp_DeleteReview @id_review", ("@id_review", reviewId));
				LoadReviews();
			}
			catch (Exception ex) { ShowErr(ex.Message); }
		}

		// ── Quantity ──────────────────────────────────────────────────────────
		private void BtnQtyMinus_Click(object sender, RoutedEventArgs e)
		{
			if (_qty > 1) { _qty--; TxtQty.Text = _qty.ToString(); }
		}

		private void BtnQtyPlus_Click(object sender, RoutedEventArgs e)
		{
			if (_qty < _maxStock) { _qty++; TxtQty.Text = _qty.ToString(); }
			else MessageBox.Show($"Максимум {_maxStock} шт. в наличии.",
				"Количество", MessageBoxButton.OK, MessageBoxImage.Information);
		}

		// ── Add to cart ───────────────────────────────────────────────────────
		private void BtnAddCart_Click(object sender, RoutedEventArgs e)
		{
			// Если товар требует размер — проверяем что выбран
			if (_sizeType is "clothing" or "shoes" && _selectedSize == null)
			{
				PanelSizeWarn.Visibility = Visibility.Visible;
				return;
			}

			if (!int.TryParse(TxtQty.Text, out int qty) || qty < 1) qty = 1;
			if (qty > _maxStock) qty = _maxStock;

			// Передаём в ShopWindow: productId, name (с размером), price, qty
			// Размер кодируем через специальный разделитель чтобы ShopWindow его разобрал
			string nameWithSize = _selectedSize != null
				? $"{_productName}||SIZE||{_selectedSize}"
				: _productName;

			AddToCartRequested?.Invoke(_productId, nameWithSize, _price, qty);
			Close();
		}

		private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

		// ── Helpers ───────────────────────────────────────────────────────────
		private static string RatingToStars(double r)
		{
			int full = (int)Math.Round(r);
			return new string('★', full) + new string('☆', 5 - full);
		}

		private static string ReviewWord(int n) => n switch
		{
			1 => "отзыв",
			2 or 3 or 4 => "отзыва",
			_ => "отзывов"
		};

		private static Brush CategoryColor(string cat) => cat switch
		{
			"Clothing" or "Одежда" or "Îmbrăcăminte" => new SolidColorBrush(Color.FromRgb(99, 102, 241)),
			"Shoes" or "Обувь" or "Încălțăminte" => new SolidColorBrush(Color.FromRgb(236, 72, 153)),
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

		private void ShowErr(string m) { TxtErr.Text = m; PanelErr.Visibility = Visibility.Visible; }
	}
}