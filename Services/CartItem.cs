namespace OnlineStoreApp.Views
{
    public class CartItem
    {
			public int ProductId { get; set; }
			public string Name { get; set; } = "";
			public decimal Price { get; set; }
			public int Qty { get; set; }
			public int MaxStock { get; set; }
			public string? Size { get; set; }   // null = товар без размера

			// Ключ корзины: товары одного продукта разных размеров — отдельные позиции
			public string CartKey => Size != null ? $"{ProductId}_{Size}" : $"{ProductId}";

			// Отображаемое название с размером: "Nike Air Max  (42)"
			public string DisplayName => Size != null ? $"{Name}  ({Size})" : Name;

			public decimal Subtotal => Price * Qty;
		}
}

