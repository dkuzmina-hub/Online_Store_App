using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Wpf;

namespace OnlineStoreApp.Services
{
    class WebViewPdfService
    {
		public async Task ShowPdfAsync(WebView2 webView, string pdfFilePath)
		{
			if (webView == null)
				throw new ArgumentNullException(nameof(webView));

			if (string.IsNullOrWhiteSpace(pdfFilePath))
				throw new ArgumentException("Путь к PDF не указан.", nameof(pdfFilePath));

			if (!File.Exists(pdfFilePath))
				throw new FileNotFoundException("PDF файл не найден.", pdfFilePath);

			await webView.EnsureCoreWebView2Async();
			webView.Source = new Uri(new Uri(pdfFilePath).AbsoluteUri);
		}
	}
}

