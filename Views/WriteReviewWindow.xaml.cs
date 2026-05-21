using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OnlineStoreApp.Data;

namespace OnlineStoreApp.Views
{
	public partial class WriteReviewWindow : Window
	{
		private readonly int _productId;
		private int _selectedRating = 0;
		private readonly Button[] _stars;

		private static readonly string[] RatingLabels =
		{
			"", "Ужасно 😞", "Плохо 😕", "Нормально 😐", "Хорошо 😊", "Отлично! 🤩"
		};

		public WriteReviewWindow(int productId, string productName)
		{
			InitializeComponent();
			_productId = productId;
			TxtProductName.Text = productName;
			_stars = new[] { Star1, Star2, Star3, Star4, Star5 };
			TxtComment.TextChanged += (_, _) =>
				TxtCharCount.Text = $"{TxtComment.Text.Length} / 500";
		}

		// ── Star interaction ──────────────────────────────────────────────────
		private void Star_Click(object sender, RoutedEventArgs e)
		{
			_selectedRating = Convert.ToInt32(((Button)sender).Tag);
			UpdateStars(_selectedRating);
			TxtRatingLabel.Text = RatingLabels[_selectedRating];
			TxtRatingLabel.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11));
		}

		private void Star_Hover(object sender, System.Windows.Input.MouseEventArgs e)
		{
			int hovered = Convert.ToInt32(((Button)sender).Tag);
			UpdateStars(hovered, hover: true);
		}

		private void Star_Leave(object sender, System.Windows.Input.MouseEventArgs e)
		{
			UpdateStars(_selectedRating);
		}

		private void UpdateStars(int count, bool hover = false)
		{
			var filled = hover
				? new SolidColorBrush(Color.FromRgb(251, 191, 36))
				: new SolidColorBrush(Color.FromRgb(245, 158, 11));
			var empty = new SolidColorBrush(Color.FromRgb(203, 213, 225));

			for (int i = 0; i < 5; i++)
			{
				_stars[i].Content = i < count ? "★" : "☆";
				_stars[i].Foreground = i < count ? filled : empty;
			}
		}

		// ── Submit ────────────────────────────────────────────────────────────
		private void BtnSubmit_Click(object sender, RoutedEventArgs e)
		{
			PanelErr.Visibility = Visibility.Collapsed;

			if (_selectedRating == 0)
			{ ShowErr("Выберите оценку от 1 до 5 звёзд."); return; }

			string comment = TxtComment.Text.Trim();

			try
			{
				var checkDt = DB.Query("EXEC sp_CheckReviewExists @id_product, @id_user",
					("@id_product", _productId), ("@id_user", Session.UserId));
				int existing = checkDt.Rows.Count > 0 ? Convert.ToInt32(checkDt.Rows[0]["cnt"]) : 0;

				if (existing > 0)
				{ ShowErr("Вы уже оставили отзыв на этот товар."); return; }

				DB.Exec("EXEC sp_AddReview @id_product, @id_user, @rating, @comment",
					("@id_product", _productId),
					("@id_user", Session.UserId),
					("@rating", _selectedRating),
					("@comment", string.IsNullOrEmpty(comment) ? null : (object)comment));

				DialogResult = true;
				Close();
			}
			catch (Exception ex) { ShowErr($"Ошибка: {ex.Message}"); }
		}

		private void BtnCancel_Click(object sender, RoutedEventArgs e) => Close();
		private void ShowErr(string m) { TxtErr.Text = m; PanelErr.Visibility = Visibility.Visible; }
	}
}