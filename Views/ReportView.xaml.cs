using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using FastReport;
using FastReport.Data;
using FastReport.Export.PdfSimple;
using Microsoft.Data.SqlClient;
using OnlineStoreApp.Data;
namespace OnlineStoreApp.Views;
using global::OnlineStoreApp.Services;


	public partial class ReportView : Window
	{

	private int _activeTab = 1;
	private readonly WebViewPdfService _webViewService = new();

	public ReportView()
	{
		InitializeComponent();
		FastReport.Utils.Config.WebMode = false;
		Loaded += async (_, _) => await GenerateReportAsync();
	}

	private async void BtnTab1_Click(object sender, RoutedEventArgs e) => await SwitchTabAsync(1);
	private async void BtnTab2_Click(object sender, RoutedEventArgs e) => await SwitchTabAsync(2);
	private async void BtnTab3_Click(object sender, RoutedEventArgs e) => await SwitchTabAsync(3);

	private async System.Threading.Tasks.Task SwitchTabAsync(int tab)
	{
		_activeTab = tab;
		BtnTab1.Style = (System.Windows.Style)FindResource(tab == 1 ? "TabBtnActive" : "TabBtn");
		BtnTab2.Style = (System.Windows.Style)FindResource(tab == 2 ? "TabBtnActive" : "TabBtn");
		BtnTab3.Style = (System.Windows.Style)FindResource(tab == 3 ? "TabBtnActive" : "TabBtn");

		PanelFilters.Visibility = tab == 1 ? Visibility.Collapsed : Visibility.Visible;
		PanelStatusFilters.Visibility = tab == 2 ? Visibility.Visible : Visibility.Collapsed;

		await GenerateReportAsync();
	}

	private async void BtnGenerate_Click(object sender, RoutedEventArgs e) => await GenerateReportAsync();
	private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();


	private async System.Threading.Tasks.Task GenerateReportAsync()
	{
		try
		{
			switch (_activeTab)
			{
				case 1: await ShowReport1Async(); break;
				case 2: await ShowReport2Async(); break;
				case 3: await ShowReport3Async(); break;
			}
		}
		catch (Exception ex)
		{
			MessageBox.Show($"Ошибка формирования отчёта:\n{ex.Message}",
				"Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
		}
	}

	private async System.Threading.Tasks.Task ShowReport1Async()
	{
		DataTable dt = DatabaseHelper.ExecuteStoredProcedureWithResult("rpt_AllOrders");
		dt.TableName = "AllOrders";
		RenameColumns(dt, new Dictionary<string, string>
		{
			["№ заказа"] = "order_id",
			["Клиент"] = "client",
			["Email"] = "email",
			["Дата"] = "date",
			["Статус доставки"] = "trans_status",
			["Статус оплаты"] = "paym_status",
			["Позиций"] = "items_count",
			["Сумма (MDL)"] = "total_sum"
		});

		decimal total = dt.AsEnumerable()
			.Sum(r => r.Field<decimal>("total_sum"));

		using var report = new Report();
		report.Load(GetFrxPath("rpt_AllOrders.frx"));
		report.RegisterData(dt, "AllOrders");
		report.GetDataSource("AllOrders").Enabled = true;

		report.SetParameterValue("ParamCount", dt.Rows.Count.ToString());
		report.SetParameterValue("ParamTotal", $"{total:N2}");

		report.Prepare();

		string pdfPath = ExportReportToPdf(report, "rpt_AllOrders");
		await _webViewService.ShowPdfAsync(PdfWebView, pdfPath);
	}


	private async System.Threading.Tasks.Task ShowReport2Async()
	{
		DataTable dt = DatabaseHelper.ExecuteStoredProcedureWithResult(
			"rpt_OrdersByFilter", BuildFilterParams());
		dt.TableName = "FilteredOrders";
		RenameColumns(dt, new Dictionary<string, string>
		{
			["№ заказа"] = "order_id",
			["Клиент"] = "client",
			["Дата"] = "date",
			["Товар"] = "product",
			["Категория"] = "category",
			["Кол-во"] = "quantity",
			["Цена (MDL)"] = "price",
			["Сумма (MDL)"] = "total_sum",
			["Статус доставки"] = "trans_status",
			["Статус оплаты"] = "paym_status"
		});

		decimal total = dt.AsEnumerable()
			.Sum(r => r.Field<decimal>("total_sum"));

		using var report = new Report();
		report.Load(GetFrxPath("rpt_OrdersByFilter.frx"));
		report.RegisterData(dt, "FilteredOrders");
		report.GetDataSource("FilteredOrders").Enabled = true;

		report.SetParameterValue("ParamCount", dt.Rows.Count.ToString());
		report.SetParameterValue("ParamTotal", $"{total:N2}");
		report.SetParameterValue("ParamDateFrom",
			DpFrom.SelectedDate.HasValue ? DpFrom.SelectedDate.Value.ToString("dd.MM.yyyy") : "—");
		report.SetParameterValue("ParamDateTo",
			DpTo.SelectedDate.HasValue ? DpTo.SelectedDate.Value.ToString("dd.MM.yyyy") : "—");
		report.SetParameterValue("ParamTrans", GetComboValue(CboTrans));
		report.SetParameterValue("ParamPaym", GetComboValue(CboPaym));

		report.Prepare();

		string pdfPath = ExportReportToPdf(report, "rpt_OrdersByFilter");
		await _webViewService.ShowPdfAsync(PdfWebView, pdfPath);
	}

	private async System.Threading.Tasks.Task ShowReport3Async()
	{
		DataTable dt = DatabaseHelper.ExecuteStoredProcedureWithResult(
			"rpt_SalesByCategory", BuildDateParams().ToArray());
		dt.TableName = "SalesCat";
		RenameColumns(dt, new Dictionary<string, string>
		{
			["Категория"] = "category",
			["Заказов"] = "orders_count",
			["Продано (шт.)"] = "units_sold",
			["Средняя цена"] = "avg_price",
			["Мин. цена"] = "min_price",
			["Макс. цена"] = "max_price",
			["Выручка (MDL)"] = "revenue"
		});

		decimal totalRevenue = dt.AsEnumerable()
			.Sum(r => r.Field<decimal>("revenue"));
		int totalOrders = dt.AsEnumerable()
			.Sum(r => r.Field<int>("orders_count"));
		int totalUnits = dt.AsEnumerable()
			.Sum(r => r.Field<int>("units_sold"));

		using var report = new Report();
		report.Load(GetFrxPath("rpt_SalesByCategory.frx"));
		report.RegisterData(dt, "SalesCat");
		report.GetDataSource("SalesCat").Enabled = true;

		report.SetParameterValue("ParamCount", dt.Rows.Count.ToString());
		report.SetParameterValue("ParamTotal", $"{totalRevenue:N2}");
		report.SetParameterValue("ParamTotalOrders", totalOrders.ToString());
		report.SetParameterValue("ParamTotalUnits", totalUnits.ToString());
		report.SetParameterValue("ParamDateFrom",
			DpFrom.SelectedDate.HasValue ? DpFrom.SelectedDate.Value.ToString("dd.MM.yyyy") : "Начало");
		report.SetParameterValue("ParamDateTo",
			DpTo.SelectedDate.HasValue ? DpTo.SelectedDate.Value.ToString("dd.MM.yyyy") : "Конец");

		report.Prepare();

		string pdfPath = ExportReportToPdf(report, "rpt_SalesByCategory");
		await _webViewService.ShowPdfAsync(PdfWebView, pdfPath);
	}

	private static string ExportReportToPdf(Report report, string outputName)
	{
		string folder = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
			"OnlineStoreApp", "Reports");
		Directory.CreateDirectory(folder);

		string path = Path.Combine(folder, $"{outputName}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
		using var pdf = new PDFSimpleExport();
		using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
		pdf.Export(report, fs);
		return path;
	}

	private static void RenameColumns(DataTable dt, Dictionary<string, string> map)
	{
		foreach (var kv in map)
			if (dt.Columns.Contains(kv.Key))
				dt.Columns[kv.Key]!.ColumnName = kv.Value;
	}

	private SqlParameter[] BuildFilterParams()
	{
		var list = BuildDateParams();
		string trans = GetComboValue(CboTrans);
		string paym = GetComboValue(CboPaym);
		list.Add(new SqlParameter("@trans_status", trans == "Все" ? DBNull.Value : (object)trans));
		list.Add(new SqlParameter("@paym_status", paym == "Все" ? DBNull.Value : (object)paym));
		return list.ToArray();
	}

	private List<SqlParameter> BuildDateParams() => new()
		{
			new SqlParameter("@date_from",
				DpFrom.SelectedDate.HasValue ? (object)DpFrom.SelectedDate.Value.Date : DBNull.Value),
			new SqlParameter("@date_to",
				DpTo.SelectedDate.HasValue   ? (object)DpTo.SelectedDate.Value.Date   : DBNull.Value)
		};

	private static string GetComboValue(ComboBox cbo) =>
		(cbo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Все";

	private static string GetFrxPath(string fileName)
	{
		string[] candidates =
		{
				Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports", fileName),
				Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName),
				Path.Combine(Directory.GetCurrentDirectory(), "Reports", fileName)
			};
		foreach (string p in candidates)
			if (File.Exists(p)) return p;

		throw new FileNotFoundException(
			$"Шаблон '{fileName}' не найден.\n" +
			"Убедитесь что папка Reports/ скопирована рядом с .exe.", fileName);
	}
}


	




