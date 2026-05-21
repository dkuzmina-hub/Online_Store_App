using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace OnlineStoreApp.Views
{
    public partial class ReportPreviewWindow : Window
    {
		private string? _currentPdfPath;

		public ReportPreviewWindow()
		{
			InitializeComponent();

			RefreshButton.Click += RefreshButton_Click;
			OpenFolderButton.Click += OpenFolderButton_Click;
			PrintButton.Click += PrintButton_Click;
		}

		public async Task LoadPdfAsync(string pdfPath)
		{
			if (string.IsNullOrWhiteSpace(pdfPath))
				throw new ArgumentException("Путь к PDF не указан.", nameof(pdfPath));

			if (!File.Exists(pdfPath))
				throw new FileNotFoundException("PDF файл не найден.", pdfPath);

			_currentPdfPath = pdfPath;

			await Browser.EnsureCoreWebView2Async();
			Browser.Source = new Uri(new Uri(pdfPath).AbsoluteUri);
		}

		private async void RefreshButton_Click(object sender, RoutedEventArgs e)
		{
			if (!string.IsNullOrWhiteSpace(_currentPdfPath) && File.Exists(_currentPdfPath))
				await LoadPdfAsync(_currentPdfPath);
		}

		private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
		{
			if (!string.IsNullOrWhiteSpace(_currentPdfPath) && File.Exists(_currentPdfPath))
				Process.Start("explorer.exe", $"/select,\"{_currentPdfPath}\"");
		}

		private async void PrintButton_Click(object sender, RoutedEventArgs e)
		{
			if (Browser.CoreWebView2 != null)
				await Browser.CoreWebView2.ExecuteScriptAsync("window.print();");
		}
	}
}
