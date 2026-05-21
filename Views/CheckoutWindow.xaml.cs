using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using Microsoft.Data.SqlClient;
using OnlineStoreApp.Data;

namespace OnlineStoreApp.Views
{
	public partial class CheckoutWindow : Window
	{
		private readonly List<CartItem> _cart;
		private bool _payByCard = false;
		public int CreatedOrderId { get; private set; }

		public CheckoutWindow(List<CartItem> cart)
		{
			InitializeComponent();
			_cart = cart;
			decimal total = 0; int qty = 0;
			foreach (var c in cart) { total += c.Subtotal; qty += c.Qty; }
			TxtTotal.Text = $"Итого: {total:N2} MDL  •  {qty} поз.";
		}

		private void SelectCash_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) => SetMethod(false);
		private void SelectCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) => SetMethod(true);

		private void SetMethod(bool card)
		{
			_payByCard = card;
			BdrCash.BorderBrush = card ? new SolidColorBrush(Color.FromRgb(226, 232, 240)) : new SolidColorBrush(Color.FromRgb(37, 99, 235));
			BdrCash.Background = card ? System.Windows.Media.Brushes.White : new SolidColorBrush(Color.FromRgb(239, 246, 255));
			DotCash.Fill = card ? new SolidColorBrush(Color.FromRgb(226, 232, 240)) : new SolidColorBrush(Color.FromRgb(37, 99, 235));
			BdrCard.BorderBrush = card ? new SolidColorBrush(Color.FromRgb(37, 99, 235)) : new SolidColorBrush(Color.FromRgb(226, 232, 240));
			BdrCard.Background = card ? new SolidColorBrush(Color.FromRgb(239, 246, 255)) : System.Windows.Media.Brushes.White;
			DotCard.Fill = card ? new SolidColorBrush(Color.FromRgb(37, 99, 235)) : new SolidColorBrush(Color.FromRgb(226, 232, 240));
			PanelCardForm.Visibility = card ? Visibility.Visible : Visibility.Collapsed;
			PanelCashInfo.Visibility = card ? Visibility.Collapsed : Visibility.Visible;
			PanelErr.Visibility = Visibility.Collapsed;
		}

		private bool _formattingCard = false;
		private void CardNumber_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
		{
			if (_formattingCard) return;
			_formattingCard = true;
			string digits = Regex.Replace(TxtCardNumber.Text, @"\D", "");
			if (digits.Length > 16) digits = digits[..16];
			var parts = new List<string>();
			for (int i = 0; i < digits.Length; i += 4)
				parts.Add(digits.Substring(i, Math.Min(4, digits.Length - i)));
			TxtCardNumber.Text = string.Join(" ", parts);
			TxtCardNumber.CaretIndex = TxtCardNumber.Text.Length;
			CardPlaceholder.Visibility = TxtCardNumber.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
			_formattingCard = false;
		}

		private bool _formattingExpiry = false;
		private void Expiry_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
		{
			if (_formattingExpiry) return;
			_formattingExpiry = true;
			string digits = Regex.Replace(TxtExpiry.Text, @"\D", "");
			if (digits.Length > 4) digits = digits[..4];
			TxtExpiry.Text = digits.Length > 2 ? digits[..2] + "/" + digits[2..] : digits;
			TxtExpiry.CaretIndex = TxtExpiry.Text.Length;
			_formattingExpiry = false;
		}

		private void BtnConfirm_Click(object sender, RoutedEventArgs e)
		{
			PanelErr.Visibility = Visibility.Collapsed;
			if (_payByCard && !ValidateCard()) return;

			try
			{
				string paymStatus = _payByCard ? "paid" : "unpaid";

				// 1. Создаём заказ через CreateOrder — возвращает NewOrderID
				object? oid = DB.Scalar(
					"EXEC CreateOrder @id_user, @date, @trans_status, @paym_status",
					("@id_user", Session.UserId),
					("@date", DateTime.Today),
					("@trans_status", "pending"),
					("@paym_status", paymStatus));

				if (oid == null || oid == DBNull.Value)
					throw new Exception("Не удалось создать заказ.");

				CreatedOrderId = Convert.ToInt32(oid);

				// 2. Добавляем позиции через sp_AddOrderItem (уменьшает остатки на складе)
				foreach (var item in _cart)
				{
					if (item.Size != null)
					{
						// Товар с размером — передаём @size_label
						DB.Exec(
							"EXEC sp_AddOrderItem @id_order, @id_product, @quantity, @price, @size_label",
							("@id_order", CreatedOrderId),
							("@id_product", item.ProductId),
							("@quantity", item.Qty),
							("@price", item.Price),
							("@size_label", item.Size));
					}
					else
					{
						// Товар без размера — @size_label не передаём (DEFAULT NULL в процедуре)
						DB.Exec(
							"EXEC sp_AddOrderItem @id_order, @id_product, @quantity, @price",
							("@id_order", CreatedOrderId),
							("@id_product", item.ProductId),
							("@quantity", item.Qty),
							("@price", item.Price));
					}
				}
				string msg = _payByCard
					? $"✓ Заказ №{CreatedOrderId} оформлен!\n\nОплата картой принята. Статус: оплачен."
					: $"✓ Заказ №{CreatedOrderId} оформлен!\n\nОплатите наличными при получении.";

				MessageBox.Show(msg, "Заказ оформлен", MessageBoxButton.OK, MessageBoxImage.Information);

				DialogResult = true;
				Close();
			}
			catch (Exception ex) { ShowErr($"Ошибка: {ex.Message}"); }
		}
		private bool ValidateCard()
		{
			string number = Regex.Replace(TxtCardNumber.Text, @"\s", "");
			string holder = TxtCardHolder.Text.Trim();
			string expiry = TxtExpiry.Text.Trim();
			string cvv = PwdCvv.Password.Trim();

			if (number.Length < 16) { ShowErr("Введите полный номер карты (16 цифр)."); return false; }
			if (!LuhnCheck(number)) { ShowErr("Номер карты недействителен."); return false; }
			if (string.IsNullOrEmpty(holder) || holder.Length < 3) { ShowErr("Введите имя владельца карты."); return false; }
			if (!Regex.IsMatch(expiry, @"^\d{2}/\d{2}$")) { ShowErr("Введите срок действия в формате ММ/ГГ."); return false; }
			int month = int.Parse(expiry[..2]);
			int year = int.Parse(expiry[3..]) + 2000;
			if (month < 1 || month > 12) { ShowErr("Месяц срока действия должен быть от 01 до 12."); return false; }
			if (new DateTime(year, month, 1).AddMonths(1) <= DateTime.Now)
			{ ShowErr("Срок действия карты истёк."); return false; }
			if (!Regex.IsMatch(cvv, @"^\d{3}$")) { ShowErr("CVV должен содержать 3 цифры."); return false; }
			return true;
		}

		private static bool LuhnCheck(string number)
		{
			int sum = 0; bool alt = false;
			for (int i = number.Length - 1; i >= 0; i--)
			{
				if (!char.IsDigit(number[i])) return false;
				int n = number[i] - '0';
				if (alt) { n *= 2; if (n > 9) n -= 9; }
				sum += n; alt = !alt;
			}
			return sum % 10 == 0;
		}

		private void BtnCancel_Click(object sender, RoutedEventArgs e) => Close();
		private void ShowErr(string m) { TxtErr.Text = m; PanelErr.Visibility = Visibility.Visible; }
	}
}