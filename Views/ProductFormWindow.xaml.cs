using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Data.SqlClient;
using Microsoft.Win32;
using OnlineStoreApp.Data;

namespace OnlineStoreApp.Views
{
	// Модель для строки размера в списке
	public class SizeEntry
	{
		public string Label { get; set; } = "";
		public int Stock { get; set; }
	}

	public partial class ProductFormWindow : Window
	{
		private readonly DataRowView? _row;
		private readonly bool _isEdit;
		private string? _imagePath;

		// Список размеров, отображаемый в ItemsControl
		private ObservableCollection<SizeEntry> _sizes = new();

	
		private static readonly string[] ClothingSizes = { "XS", "S", "M", "L", "XL", "XXL" };
		private static readonly string[] ShoesSizes =
			{ "35", "36", "37", "38", "39", "40", "41", "42", "43", "44", "45", "46" };

		public ProductFormWindow(DataRowView? row)
		{
			InitializeComponent();
			_row = row;
			_isEdit = row != null;
			TxtTitle.Text = _isEdit ? "Редактировать товар" : "Новый товар";
			LoadCategories();

			if (_isEdit && row != null)
			{
				TxtName.Text = row.Row["name_prod"]?.ToString();
				TxtDescription.Text = row.Row["description"]?.ToString();
				TxtPrice.Text = row.Row["price"]?.ToString();
				TxtStock.Text = row.Row["stock"]?.ToString();

				string? imgPath = row.Row.Table.Columns.Contains("image_path")
					? row.Row["image_path"]?.ToString() : null;
				if (!string.IsNullOrWhiteSpace(imgPath))
					SetImagePath(imgPath);

				string catName = row.Row["name_category"]?.ToString() ?? "";
				foreach (var item in CboCategory.Items)
				{
					if (item is ComboItem ci && ci.Name == catName)
					{ CboCategory.SelectedItem = ci; break; }
				}

				string sizeType = row.Row.Table.Columns.Contains("size_type")
					? row.Row["size_type"]?.ToString() ?? "none"
					: "none";
				SelectSizeType(sizeType);

	
				if (sizeType is "clothing" or "shoes")
					LoadExistingSizes(Convert.ToInt32(row.Row["id_product"]));
			}

			SizesList.ItemsSource = _sizes;
		}

		private void LoadCategories()
		{
			try
			{
				var dt = DatabaseHelper.ExecuteStoredProcedureWithResult(
					"sp_GetCategories", new[] { new SqlParameter("@search_value", DBNull.Value) });

				var list = new List<ComboItem>();
				foreach (DataRow r in dt.Rows)
					list.Add(new ComboItem((int)r["id_category"], r["name_category"].ToString()!));

				CboCategory.ItemsSource = list;
				if (list.Count > 0) CboCategory.SelectedIndex = 0;
			}
			catch (Exception ex) { ShowError($"Ошибка загрузки категорий: {ex.Message}"); }
		}

		private void LoadExistingSizes(int productId)
		{
			try
			{
				var dt = DatabaseHelper.ExecuteStoredProcedureWithResult(
					"sp_GetProductSizes", new[] { new SqlParameter("@id_product", productId) });

				_sizes.Clear();
				foreach (DataRow r in dt.Rows)
					_sizes.Add(new SizeEntry
					{
						Label = r["size_label"].ToString()!,
						Stock = Convert.ToInt32(r["stock"])
					});
			}
			catch (Exception ex) { ShowError($"Ошибка загрузки размеров: {ex.Message}"); }
		}

		// ── Выбор типа размера ─────────────────────────────────────────────────
		private void SelectSizeType(string sizeType)
		{
			foreach (ComboBoxItem item in CboSizeType.Items)
			{
				if (item.Tag?.ToString() == sizeType)
				{ CboSizeType.SelectedItem = item; return; }
			}
			CboSizeType.SelectedIndex = 0;
		}

		private string GetSelectedSizeType()
		{
			if (CboSizeType.SelectedItem is ComboBoxItem cbi)
				return cbi.Tag?.ToString() ?? "none";
			return "none";
		}

		private void CboSizeType_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (PanelSizes == null || CboNewSize == null) return;

			string st = GetSelectedSizeType();
			bool hasSizes = st is "clothing" or "shoes";
			PanelSizes.Visibility = hasSizes ? Visibility.Visible : Visibility.Collapsed;

			if (!hasSizes)
			{
				_sizes.Clear();
				return;
			}

			
			string[] available = st == "clothing" ? ClothingSizes : ShoesSizes;
			CboNewSize.Items.Clear();
			foreach (string s in available)
				CboNewSize.Items.Add(s);
			if (CboNewSize.Items.Count > 0)
				CboNewSize.SelectedIndex = 0;

			if (!_isEdit)
				_sizes.Clear();
		}

		// ── Добавить размер ────────────────────────────────────────────────────
		private void BtnAddSize_Click(object sender, RoutedEventArgs e)
		{
			string label = CboNewSize.SelectedItem?.ToString() ?? "";
			if (string.IsNullOrEmpty(label)) return;

			if (_sizes.Any(s => s.Label == label))
			{ ShowError($"Размер {label} уже добавлен."); return; }

			if (!int.TryParse(TxtNewStock.Text.Trim(), out int stock) || stock < 0)
			{ ShowError("Введите корректный остаток (0 или больше)."); return; }

			HideError();
			_sizes.Add(new SizeEntry { Label = label, Stock = stock });
			TxtNewStock.Text = "0";
		}

		// ── Удалить размер ─────────────────────────────────────────────────────
		private void BtnRemoveSize_Click(object sender, RoutedEventArgs e)
		{
			if (sender is Button btn && btn.Tag is string label)
			{
				var entry = _sizes.FirstOrDefault(s => s.Label == label);
				if (entry != null) _sizes.Remove(entry);
			}
		}

		// ── Выбор изображения ──────────────────────────────────────────────────
		private void BtnBrowseImage_Click(object sender, RoutedEventArgs e)
		{
			var dlg = new OpenFileDialog
			{
				Title = "Выберите изображение товара",
				Filter = "Изображения|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.webp|Все файлы|*.*"
			};
			if (dlg.ShowDialog() == true)
				SetImagePath(dlg.FileName);
		}

		private void BtnClearImage_Click(object sender, RoutedEventArgs e)
		{
			_imagePath = null;
			TxtImagePath.Text = "(не выбрано)";
			ImgPreview.Source = null;
			ImgPreview.Visibility = Visibility.Collapsed;
			TxtNoImage.Visibility = Visibility.Visible;
		}

		private void SetImagePath(string path)
		{
			_imagePath = path;
			TxtImagePath.Text = path;
			try
			{
				var bmp = new BitmapImage();
				bmp.BeginInit();
				bmp.UriSource = new Uri(path, UriKind.Absolute);
				bmp.CacheOption = BitmapCacheOption.OnLoad;
				bmp.DecodePixelWidth = 400;
				bmp.EndInit();
				ImgPreview.Source = bmp;
				ImgPreview.Visibility = Visibility.Visible;
				TxtNoImage.Visibility = Visibility.Collapsed;
			}
			catch
			{
				ImgPreview.Source = null;
				ImgPreview.Visibility = Visibility.Collapsed;
				TxtNoImage.Visibility = Visibility.Visible;
			}
		}

		// ── Сохранение ────────────────────────────────────────────────────────
		private void BtnSave_Click(object sender, RoutedEventArgs e)
		{
			HideError();
			string name = TxtName.Text.Trim();
			string desc = TxtDescription.Text.Trim();
			string priceStr = TxtPrice.Text.Trim();
			string stockStr = TxtStock.Text.Trim();
			string sizeType = GetSelectedSizeType();

			if (string.IsNullOrEmpty(name))
			{ ShowError("Введите название товара."); return; }
			if (!decimal.TryParse(priceStr, out decimal price) || price <= 0)
			{ ShowError("Введите корректную цену (больше 0)."); TxtPrice.Focus(); return; }
			if (!int.TryParse(stockStr, out int stock) || stock < 0)
			{ ShowError("Введите корректное количество (0 или больше)."); TxtStock.Focus(); return; }
			if (CboCategory.SelectedItem is not ComboItem cat)
			{ ShowError("Выберите категорию."); return; }

			// Если товар с размерами — суммарный остаток берём из размеров
			if (sizeType is "clothing" or "shoes" && _sizes.Count > 0)
				stock = _sizes.Sum(s => s.Stock);

			object imgParam = string.IsNullOrWhiteSpace(_imagePath)
				? DBNull.Value : (object)_imagePath;

			try
			{
				int productId;
				if (_isEdit && _row != null)
				{
					productId = Convert.ToInt32(_row.Row["id_product"]);
					DatabaseHelper.ExecuteStoredProcedure("UpdateProduct", new[]
					{
						new SqlParameter("@id_product",  productId),
						new SqlParameter("@name",        name),
						new SqlParameter("@description", string.IsNullOrEmpty(desc) ? DBNull.Value : (object)desc),
						new SqlParameter("@price",       price),
						new SqlParameter("@stock",       stock),
						new SqlParameter("@id_category", cat.Id),
						new SqlParameter("@image_path",  imgParam)
					});
				}
				else
				{
					// Новый товар — получаем id через sp_AddProductGetId или AddProductSafe
					// Используем scalar для получения нового id
					object? newId = DB.Scalar(
						"EXEC AddProductSafe @name, @description, @price, @stock, @id_category, @image_path",
						("@name", (object)name),
						("@description", string.IsNullOrEmpty(desc) ? DBNull.Value : (object)desc),
						("@price", (object)price),
						("@stock", (object)stock),
						("@id_category", (object)cat.Id),
						("@image_path", imgParam));

					productId = newId != null && newId != DBNull.Value
						? Convert.ToInt32(newId) : 0;
				}

				// Обновляем size_type у товара
				DB.Exec(
					"UPDATE products SET size_type = @st WHERE id_product = @id",
					("@st", (object)sizeType),
					("@id", (object)(_isEdit && _row != null
						? Convert.ToInt32(_row.Row["id_product"])
						: productId)));

				// Сохраняем размеры
				if (sizeType is "clothing" or "shoes" && productId > 0)
					SaveSizes(productId);

				DialogResult = true;
				Close();
			}
			catch (Exception ex) { ShowError($"Ошибка: {ex.Message}"); }
		}

		// ── Сохранение размеров: полная перезапись ────────────────────────────
		private void SaveSizes(int productId)
		{
			// Удаляем старые размеры
			DB.Exec(
				"DELETE FROM product_sizes WHERE id_product = @id",
				("@id", (object)productId));

			// Вставляем актуальные
			foreach (var s in _sizes)
			{
				DB.Exec(
					"INSERT INTO product_sizes (id_product, size_label, stock) VALUES (@id, @label, @stock)",
					("@id", (object)productId),
					("@label", (object)s.Label),
					("@stock", (object)s.Stock));
			}
		}

		private void BtnCancel_Click(object sender, RoutedEventArgs e) => Close();
		private void ShowError(string msg) { TxtError.Text = msg; PanelError.Visibility = Visibility.Visible; }
		private void HideError() => PanelError.Visibility = Visibility.Collapsed;
	}

	public class ComboItem
	{
		public int Id { get; }
		public string Name { get; }
		public ComboItem(int id, string name) { Id = id; Name = name; }
		public override string ToString() => Name;
	}
}
