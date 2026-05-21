using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Data.SqlClient;
using OnlineStoreApp.Data;
using OxyPlot;


namespace OnlineStoreApp.Views
{
	public partial class DashboardWindow : Window
	{

		private List<(string Label, double Value)> _barData = new();
		private List<(string Label, double Value)> _pieData = new();
		private List<(DateTime Date, double Value)> _lineData = new();
		private List<(string Label, double Value)> _histData = new();


		private static readonly string[] PieColors =
			{ "#16A34A", "#2563EB", "#D97706", "#DC2626", "#7C3AED", "#0891B2" };

		public DashboardWindow()
		{
			InitializeComponent();
			Loaded += DashboardWindow_Loaded;
		}

		private void DashboardWindow_Loaded(object sender, RoutedEventArgs e)
		{
			LoadCategories();
			SetDefaultDates();
			ApplyFilters();
		}


		private void SetDefaultDates()
		{
			DpFrom.SelectedDate = new DateTime(DateTime.Today.Year, 1, 1);
			DpTo.SelectedDate = DateTime.Today;
		}

		private void LoadCategories()
		{
			try
			{
				var dt = DatabaseHelper.ExecuteQuery(
					"EXEC sp_GetCategories NULL");

				CboCategory.Items.Clear();
				CboCategory.Items.Add(new ComboBoxItem { Content = "Все", IsSelected = true });
				foreach (DataRow r in dt.Rows)
					CboCategory.Items.Add(new ComboBoxItem
					{
						Content = r["name_category"].ToString(),
						Tag = r["id_category"]
					});
				CboCategory.SelectedIndex = 0;
			}
			catch (Exception ex) { ShowError($"Ошибка загрузки категорий: {ex.Message}"); }
		}


		private void CanvasBar_Loaded(object sender, RoutedEventArgs e) => DrawBar();
		private void CanvasPie_Loaded(object sender, RoutedEventArgs e) => DrawPie();
		private void CanvasLine_Loaded(object sender, RoutedEventArgs e) => DrawLine();
		private void CanvasHist_Loaded(object sender, RoutedEventArgs e) => DrawHist();


		private Microsoft.Data.SqlClient.SqlParameter[] BuildSpParams()
		{
			var catItem = CboCategory.SelectedItem as System.Windows.Controls.ComboBoxItem;
			var transItem = CboTransStatus.SelectedItem as System.Windows.Controls.ComboBoxItem;
			var paymItem = CboPaymStatus.SelectedItem as System.Windows.Controls.ComboBoxItem;

			string transVal = transItem?.Content?.ToString() ?? "Все";
			string paymVal = paymItem?.Content?.ToString() ?? "Все";
			string client = TxtClientName.Text.Trim();

			decimal.TryParse(TxtPriceMin.Text.Trim(), out decimal priceMin);
			decimal.TryParse(TxtPriceMax.Text.Trim(), out decimal priceMax);

			return new[]
			{
				new Microsoft.Data.SqlClient.SqlParameter("@date_from",
					DpFrom.SelectedDate.HasValue ? (object)DpFrom.SelectedDate.Value.Date : DBNull.Value),
				new Microsoft.Data.SqlClient.SqlParameter("@date_to",
					DpTo.SelectedDate.HasValue   ? (object)DpTo.SelectedDate.Value.Date   : DBNull.Value),
				new Microsoft.Data.SqlClient.SqlParameter("@id_category",
					catItem?.Tag != null ? catItem.Tag : DBNull.Value),
				new Microsoft.Data.SqlClient.SqlParameter("@trans_status",
					transVal != "Все" ? (object)transVal : DBNull.Value),
				new Microsoft.Data.SqlClient.SqlParameter("@paym_status",
					paymVal != "Все" ? (object)paymVal : DBNull.Value),
				new Microsoft.Data.SqlClient.SqlParameter("@price_min",
					TxtPriceMin.Text.Trim() != "" ? (object)priceMin : DBNull.Value),
				new Microsoft.Data.SqlClient.SqlParameter("@price_max",
					TxtPriceMax.Text.Trim() != "" ? (object)priceMax : DBNull.Value),
				new Microsoft.Data.SqlClient.SqlParameter("@client_name",
					!string.IsNullOrEmpty(client) ? (object)client : DBNull.Value),
			};
		}

		private void ApplyFilters()
		{
			HideError();
			try
			{
				LoadKpis();
				LoadDetailGrid();
				LoadChartData();
				RedrawAllCharts();
			}
			catch (Exception ex)
			{
				ShowError($"Ошибка загрузки данных: {ex.Message}");
			}
		}

		private void LoadKpis()
		{
			var dt = DatabaseHelper.ExecuteStoredProcedureWithResult("sp_DashboardKpi", BuildSpParams());
			if (dt.Rows.Count > 0)
			{
				KpiRevenue.Text = $"{Convert.ToDecimal(dt.Rows[0]["total_revenue"]):N0}";
				KpiOrders.Text = $"{Convert.ToInt32(dt.Rows[0]["order_count"]):N0}";
				KpiAvg.Text = $"{Convert.ToDecimal(dt.Rows[0]["avg_order"]):N0}";
				KpiMin.Text = $"{Convert.ToDecimal(dt.Rows[0]["min_order"]):N0}";
				KpiMax.Text = $"{Convert.ToDecimal(dt.Rows[0]["max_order"]):N0}";
			}
		}

		private void LoadDetailGrid()
		{
			
			var dt = DatabaseHelper.ExecuteStoredProcedureWithResult("sp_DashboardGrid", BuildSpParams());
			DashGrid.ItemsSource = dt.DefaultView;
			TxtGridCount.Text = $"Записей: {dt.Rows.Count:N0}";
		}

		private void LoadChartData()
		{
			
			var dtBar = DatabaseHelper.ExecuteStoredProcedureWithResult("sp_DashboardBarChart", BuildSpParams());
			_barData = dtBar.Rows.Cast<DataRow>()
				.Select(r => (r["name_category"].ToString()!, Convert.ToDouble(r["revenue"])))
				.ToList();

			var dtPie = DatabaseHelper.ExecuteStoredProcedureWithResult("sp_DashboardPieChart", BuildSpParams());
			_pieData = dtPie.Rows.Cast<DataRow>()
				.Select(r => (r["trans_status"].ToString()!, Convert.ToDouble(r["cnt"])))
				.ToList();
	
			var dtLine = DatabaseHelper.ExecuteStoredProcedureWithResult("sp_DashboardLineChart", BuildSpParams());
			_lineData = dtLine.Rows.Cast<DataRow>()
				.Where(r => r["date"] != DBNull.Value && DateTime.TryParse(r["date"].ToString(), out _))
				.Select(r => (DateTime.Parse(r["date"].ToString()!), Convert.ToDouble(r["daily_revenue"])))
				.ToList();

			var dtHist = DatabaseHelper.ExecuteStoredProcedureWithResult("sp_DashboardHistogram", BuildSpParams());
			_histData = dtHist.Rows.Cast<DataRow>()
				.Select(r => (r["month_label"].ToString()!, Convert.ToDouble(r["order_count"])))
				.ToList();
		}

		private void RedrawAllCharts()
		{
			DrawBar();
			DrawPie();
			DrawLine();
			DrawHist();
		}


		private static SolidColorBrush Brush(string hex) =>
			new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));


		private void DrawBar()
		{
			CanvasBar.Children.Clear();
			if (_barData.Count == 0) { DrawEmpty(CanvasBar, "Нет данных"); return; }

			double w = CanvasBar.ActualWidth;
			double h = CanvasBar.ActualHeight;
			if (w < 10 || h < 10) return;

			double padL = 50, padR = 10, padT = 10, padB = 40;
			double chartW = w - padL - padR;
			double chartH = h - padT - padB;

			double maxVal = _barData.Max(d => d.Value);
			if (maxVal == 0) maxVal = 1;

			int n = _barData.Count;
			double gap = 6;
			double barW = Math.Max(4, (chartW - gap * (n + 1)) / n);


			for (int i = 0; i <= 4; i++)
			{
				double yVal = maxVal * i / 4;
				double y = padT + chartH - chartH * i / 4;

				var line = new Line
				{
					X1 = padL,
					Y1 = y,
					X2 = padL + chartW,
					Y2 = y,
					Stroke = Brush("#E2E8F0"),
					StrokeThickness = 1
				};
				CanvasBar.Children.Add(line);

				var lbl = new TextBlock
				{
					Text = FormatNumber(yVal),
					FontSize = 9,
					Foreground = Brush("#94A3B8"),
					FontFamily = new FontFamily("Segoe UI")
				};
				Canvas.SetLeft(lbl, 0);
				Canvas.SetTop(lbl, y - 7);
				CanvasBar.Children.Add(lbl);
			}


			for (int i = 0; i < n; i++)
			{
				double barH = chartH * _barData[i].Value / maxVal;
				double x = padL + gap + i * (barW + gap);
				double y = padT + chartH - barH;

				var rect = new Rectangle
				{
					Width = barW,
					Height = Math.Max(1, barH),
					Fill = Brush("#2563EB"),
					RadiusX = 3,
					RadiusY = 3
				};
				Canvas.SetLeft(rect, x);
				Canvas.SetTop(rect, y);
				CanvasBar.Children.Add(rect);


				var cat = new TextBlock
				{
					Text = TruncateLabel(_barData[i].Label, 10),
					FontSize = 9,
					Foreground = Brush("#64748B"),
					FontFamily = new FontFamily("Segoe UI"),
					TextAlignment = TextAlignment.Center,
					Width = barW + gap
				};
				Canvas.SetLeft(cat, x - gap / 2);
				Canvas.SetTop(cat, padT + chartH + 4);
				CanvasBar.Children.Add(cat);


				if (barH > 18)
				{
					var val = new TextBlock
					{
						Text = FormatNumber(_barData[i].Value),
						FontSize = 8,
						Foreground = Brush("#FFFFFF"),
						FontFamily = new FontFamily("Segoe UI")
					};
					Canvas.SetLeft(val, x + 2);
					Canvas.SetTop(val, y + 3);
					CanvasBar.Children.Add(val);
				}
			}
		}



		private void DrawPie()
		{
			CanvasPie.Children.Clear();
			PieLegend.Items.Clear();

			if (_pieData.Count == 0) { DrawEmpty(CanvasPie, "Нет данных"); return; }

			double w = CanvasPie.ActualWidth;
			double h = CanvasPie.ActualHeight;
			if (w < 10 || h < 10) return;

			double cx = w / 2;
			double cy = h / 2;
			double radius = Math.Min(cx, cy) - 10;
			double total = _pieData.Sum(d => d.Value);
			if (total == 0) { DrawEmpty(CanvasPie, "Нет данных"); return; }

			double startAngle = -Math.PI / 2;

			for (int i = 0; i < _pieData.Count; i++)
			{
				double sweep = 2 * Math.PI * _pieData[i].Value / total;
				double endAngle = startAngle + sweep;
				var color = Brush(PieColors[i % PieColors.Length]);


				if (_pieData.Count == 1 || Math.Abs(sweep - 2 * Math.PI) < 1e-9)
				{
					var circle = new Ellipse
					{
						Width = radius * 2,
						Height = radius * 2,
						Fill = color,
						Stroke = Brush("#FFFFFF"),
						StrokeThickness = 2
					};
					Canvas.SetLeft(circle, cx - radius);
					Canvas.SetTop(circle, cy - radius);
					CanvasPie.Children.Add(circle);
				}
				else
				{
					var path = new System.Windows.Shapes.Path
					{
						Fill = color,
						Stroke = Brush("#FFFFFF"),
						StrokeThickness = 2
					};

					bool isLarge = sweep > Math.PI;
					var startP = new System.Windows.Point(cx + radius * Math.Cos(startAngle),
														   cy + radius * Math.Sin(startAngle));
					var endP = new System.Windows.Point(cx + radius * Math.Cos(endAngle),
														   cy + radius * Math.Sin(endAngle));

					var geo = new PathGeometry();
					var fig = new PathFigure { StartPoint = new System.Windows.Point(cx, cy), IsClosed = true };
					fig.Segments.Add(new LineSegment(startP, true));
					fig.Segments.Add(new ArcSegment(endP,
						new Size(radius, radius), 0,
						isLarge,
						SweepDirection.Clockwise, true));
					geo.Figures.Add(fig);
					path.Data = geo;
					CanvasPie.Children.Add(path);
				}

				if (sweep > 0.25)
				{
					double midAngle = startAngle + sweep / 2;
					double pct = _pieData[i].Value / total * 100;
					var lbl = new TextBlock
					{
						Text = $"{pct:0}%",
						FontSize = 10,
						Foreground = Brush("#FFFFFF"),
						FontFamily = new FontFamily("Segoe UI")
					};
					Canvas.SetLeft(lbl, cx + (radius * 0.6) * Math.Cos(midAngle) - 12);
					Canvas.SetTop(lbl, cy + (radius * 0.6) * Math.Sin(midAngle) - 7);
					CanvasPie.Children.Add(lbl);
				}

				startAngle = endAngle;


				var legendItem = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 10, 2) };
				legendItem.Children.Add(new Rectangle
				{
					Width = 10,
					Height = 10,
					Fill = Brush(PieColors[i % PieColors.Length]),
					Margin = new Thickness(0, 0, 4, 0)
				});
				legendItem.Children.Add(new TextBlock
				{
					Text = $"{_pieData[i].Label} ({_pieData[i].Value:0})",
					FontSize = 10,
					Foreground = Brush("#475569"),
					FontFamily = new FontFamily("Segoe UI")
				});
				PieLegend.Items.Add(legendItem);
			}
		}


		private void DrawLine()
		{
			CanvasLine.Children.Clear();
			if (_lineData.Count == 0) { DrawEmpty(CanvasLine, "Нет данных"); return; }

			double w = CanvasLine.ActualWidth;
			double h = CanvasLine.ActualHeight;
			if (w < 10 || h < 10) return;

			double padL = 55, padR = 10, padT = 10, padB = 30;
			double chartW = w - padL - padR;
			double chartH = h - padT - padB;

			double maxVal = _lineData.Max(d => d.Value);
			if (maxVal == 0) maxVal = 1;

			double minT = _lineData.Min(d => d.Date.Ticks);
			double maxT = _lineData.Max(d => d.Date.Ticks);
			double rangeT = maxT - minT;
			if (rangeT == 0) rangeT = 1;


			for (int i = 0; i <= 4; i++)
			{
				double yVal = maxVal * i / 4;
				double y = padT + chartH - chartH * i / 4;
				CanvasLine.Children.Add(new Line
				{
					X1 = padL,
					Y1 = y,
					X2 = padL + chartW,
					Y2 = y,
					Stroke = Brush("#E2E8F0"),
					StrokeThickness = 1
				});
				var lbl = new TextBlock
				{
					Text = FormatNumber(yVal),
					FontSize = 9,
					Foreground = Brush("#94A3B8"),
					FontFamily = new FontFamily("Segoe UI")
				};
				Canvas.SetLeft(lbl, 0); Canvas.SetTop(lbl, y - 7);
				CanvasLine.Children.Add(lbl);
			}


			var fillPoints = new PointCollection();
			fillPoints.Add(new System.Windows.Point(
				padL + chartW * (_lineData[0].Date.Ticks - minT) / rangeT,
				padT + chartH));

			foreach (var (date, val) in _lineData)
			{
				double x = padL + chartW * (date.Ticks - minT) / rangeT;
				double y = padT + chartH - chartH * val / maxVal;
				fillPoints.Add(new System.Windows.Point(x, y));
			}

			fillPoints.Add(new System.Windows.Point(
				padL + chartW * (_lineData[^1].Date.Ticks - minT) / rangeT,
				padT + chartH));

			var fill = new Polygon
			{
				Points = fillPoints,
				Fill = new SolidColorBrush(Color.FromArgb(30, 37, 99, 235)),
				Stroke = Brushes.Transparent
			};
			CanvasLine.Children.Add(fill);

			for (int i = 1; i < _lineData.Count; i++)
			{
				var (d1, v1) = _lineData[i - 1];
				var (d2, v2) = _lineData[i];
				double x1 = padL + chartW * (d1.Ticks - minT) / rangeT;
				double y1 = padT + chartH - chartH * v1 / maxVal;
				double x2 = padL + chartW * (d2.Ticks - minT) / rangeT;
				double y2 = padT + chartH - chartH * v2 / maxVal;

				CanvasLine.Children.Add(new Line
				{
					X1 = x1,
					Y1 = y1,
					X2 = x2,
					Y2 = y2,
					Stroke = Brush("#2563EB"),
					StrokeThickness = 2.5
				});
			}

			int skip = Math.Max(1, _lineData.Count / 6);
			for (int i = 0; i < _lineData.Count; i++)
			{
				var (date, val) = _lineData[i];
				double x = padL + chartW * (date.Ticks - minT) / rangeT;
				double y = padT + chartH - chartH * val / maxVal;

				var dot = new Ellipse
				{
					Width = 7,
					Height = 7,
					Fill = Brush("#2563EB"),
					Stroke = Brush("#FFFFFF"),
					StrokeThickness = 1.5
				};
				Canvas.SetLeft(dot, x - 3.5); Canvas.SetTop(dot, y - 3.5);
				CanvasLine.Children.Add(dot);

				if (i % skip == 0)
				{
					var dateLbl = new TextBlock
					{
						Text = date.ToString("dd.MM"),
						FontSize = 8,
						Foreground = Brush("#94A3B8"),
						FontFamily = new FontFamily("Segoe UI")
					};
					Canvas.SetLeft(dateLbl, x - 12); Canvas.SetTop(dateLbl, padT + chartH + 4);
					CanvasLine.Children.Add(dateLbl);
				}
			}
		}



		private void DrawHist()
		{
			CanvasHist.Children.Clear();
			if (_histData.Count == 0) { DrawEmpty(CanvasHist, "Нет данных"); return; }

			double w = CanvasHist.ActualWidth;
			double h = CanvasHist.ActualHeight;
			if (w < 10 || h < 10) return;

			double padL = 35, padR = 10, padT = 10, padB = 38;
			double chartW = w - padL - padR;
			double chartH = h - padT - padB;

			double maxVal = _histData.Max(d => d.Value);
			if (maxVal == 0) maxVal = 1;

			int n = _histData.Count;
			double gap = 5;
			double barW = Math.Max(4, (chartW - gap * (n + 1)) / n);

			
			int gridLines = (int)Math.Ceiling(maxVal);
			gridLines = Math.Min(gridLines, 5);
			for (int i = 0; i <= gridLines; i++)
			{
				double y = padT + chartH - chartH * i / gridLines;
				CanvasHist.Children.Add(new Line
				{
					X1 = padL,
					Y1 = y,
					X2 = padL + chartW,
					Y2 = y,
					Stroke = Brush("#E2E8F0"),
					StrokeThickness = 1
				});
				var lbl = new TextBlock
				{
					Text = ((int)(maxVal * i / gridLines)).ToString(),
					FontSize = 9,
					Foreground = Brush("#94A3B8"),
					FontFamily = new FontFamily("Segoe UI")
				};
				Canvas.SetLeft(lbl, 0); Canvas.SetTop(lbl, y - 7);
				CanvasHist.Children.Add(lbl);
			}

			
			for (int i = 0; i < n; i++)
			{
				double barH = chartH * _histData[i].Value / maxVal;
				double x = padL + gap + i * (barW + gap);
				double y = padT + chartH - barH;

				var rect = new Rectangle
				{
					Width = barW,
					Height = Math.Max(1, barH),
					Fill = Brush("#10B981"),
					RadiusX = 3,
					RadiusY = 3
				};
				Canvas.SetLeft(rect, x); Canvas.SetTop(rect, y);
				CanvasHist.Children.Add(rect);

				if (barH > 14)
				{
					var val = new TextBlock
					{
						Text = ((int)_histData[i].Value).ToString(),
						FontSize = 9,
						Foreground = Brush("#FFFFFF"),
						FontFamily = new FontFamily("Segoe UI")
					};
					Canvas.SetLeft(val, x + 3); Canvas.SetTop(val, y + 2);
					CanvasHist.Children.Add(val);
				}

				var monthLbl = new TextBlock
				{
					Text = _histData[i].Label,
					FontSize = 8,
					Foreground = Brush("#64748B"),
					FontFamily = new FontFamily("Segoe UI"),
					Width = barW + gap,
					TextAlignment = TextAlignment.Center
				};
				Canvas.SetLeft(monthLbl, x - gap / 2);
				Canvas.SetTop(monthLbl, padT + chartH + 4);
				CanvasHist.Children.Add(monthLbl);
			}
		}



		private static void DrawEmpty(Canvas canvas, string text)
		{
			var lbl = new TextBlock
			{
				Text = text,
				FontSize = 13,
				Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
				FontFamily = new FontFamily("Segoe UI")
			};
			Canvas.SetLeft(lbl, (canvas.ActualWidth - 60) / 2);
			Canvas.SetTop(lbl, (canvas.ActualHeight - 20) / 2);
			canvas.Children.Add(lbl);
		}



		private void BtnExportTxt_Click(object sender, RoutedEventArgs e)
		{
			if (DashGrid.ItemsSource is not DataView dv || dv.Count == 0)
			{
				ShowError("Нет данных для экспорта.");
				return;
			}

			var dlg = new Microsoft.Win32.SaveFileDialog
			{
				Filter = "Text файл (*.txt)|*.txt",
				FileName = $"dashboard_{DateTime.Today:yyyy-MM-dd}.txt"
			};
			if (dlg.ShowDialog() != true) return;

			try
			{
				var sb = new StringBuilder();
				var dt = dv.Table!;

				sb.AppendLine(string.Join("\t",
					dt.Columns.Cast<DataColumn>().Select(c => c.ColumnName)));

				foreach (DataRowView row in dv)
					sb.AppendLine(string.Join("\t",
						row.Row.ItemArray.Select(v => v?.ToString() ?? "")));

				System.IO.File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
				MessageBox.Show($"Экспорт завершён:\n{dlg.FileName}",
					"Экспорт TXT", MessageBoxButton.OK, MessageBoxImage.Information);
			}
			catch (Exception ex)
			{
				ShowError($"Ошибка экспорта: {ex.Message}");
			}
		}



		private void BtnApply_Click(object sender, RoutedEventArgs e)
		{
			if (!ValidateFilters()) return;
			ApplyFilters();
		}

		private void BtnResetFilters_Click(object sender, RoutedEventArgs e)
		{
			SetDefaultDates();
			CboCategory.SelectedIndex = 0;
			CboTransStatus.SelectedIndex = 0;
			CboPaymStatus.SelectedIndex = 0;
			TxtPriceMin.Text = "0";
			TxtPriceMax.Text = "999999";
			TxtClientName.Text = "";
			HideError();
			ApplyFilters();
		}

		private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();



		private bool ValidateFilters()
		{
			if (DpFrom.SelectedDate.HasValue && DpTo.SelectedDate.HasValue
				&& DpFrom.SelectedDate > DpTo.SelectedDate)
			{
				ShowError("Дата «с» не может быть позже даты «по».");
				return false;
			}
			if (!string.IsNullOrWhiteSpace(TxtPriceMin.Text) &&
				!decimal.TryParse(TxtPriceMin.Text.Trim(), out _))
			{
				ShowError("Введите корректное число в поле «Цена от».");
				return false;
			}
			if (!string.IsNullOrWhiteSpace(TxtPriceMax.Text) &&
				!decimal.TryParse(TxtPriceMax.Text.Trim(), out _))
			{
				ShowError("Введите корректное число в поле «Цена до».");
				return false;
			}
			if (decimal.TryParse(TxtPriceMin.Text.Trim(), out decimal mn) &&
				decimal.TryParse(TxtPriceMax.Text.Trim(), out decimal mx) && mn > mx)
			{
				ShowError("«Цена от» не может быть больше «Цена до».");
				return false;
			}
			return true;
		}


		private static string FormatNumber(double v)
		{
			if (v >= 1_000_000) return $"{v / 1_000_000:0.#}M";
			if (v >= 1_000) return $"{v / 1_000:0.#}K";
			return $"{v:0}";
		}

		private static string TruncateLabel(string s, int max) =>
			s.Length <= max ? s : s[..max] + "…";

		private void ShowError(string msg)
		{
			TxtError.Text = msg;
			PanelError.Visibility = Visibility.Visible;
		}

		private void HideError() =>
			PanelError.Visibility = Visibility.Collapsed;
	}
}
