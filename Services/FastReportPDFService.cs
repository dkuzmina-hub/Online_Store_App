using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FastReport;
using FastReport.Export.PdfSimple;

namespace OnlineStoreApp.Services
{
    class FastReportPDFService
    {
		public string ExportPdfFromDataTable(
		  string reportPath,
		  DataTable dataTable,
		  string dataSourceName,
		  string outputDirectory,
		  string outputFileNameWithoutExtension)
		{
			if (string.IsNullOrWhiteSpace(reportPath))
				throw new ArgumentException("Не указан путь к отчету.", nameof(reportPath));

			if (!File.Exists(reportPath))
				throw new FileNotFoundException("Файл отчета не найден.", reportPath);

			if (dataTable == null)
				throw new ArgumentNullException(nameof(dataTable));

			if (string.IsNullOrWhiteSpace(dataSourceName))
				throw new ArgumentException("Не указано имя источника данных.", nameof(dataSourceName));

			Directory.CreateDirectory(outputDirectory);

			string outputFile = Path.Combine(outputDirectory, outputFileNameWithoutExtension + ".pdf");

			using (Report report = new Report())
			{
				report.Load(reportPath);
				report.RegisterData(dataTable, dataSourceName);

				var dataSource = report.GetDataSource(dataSourceName);
				if (dataSource != null)
					dataSource.Enabled = true;

				report.Prepare();

				using (PDFSimpleExport pdfExport = new PDFSimpleExport())
				using (FileStream fs = new FileStream(outputFile, FileMode.Create, FileAccess.Write))
				{
					pdfExport.Export(report, fs);
				}
			}

			return outputFile;
		}
	}
}

