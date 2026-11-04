using System.Globalization;
using System.Text;
using CsvHelper;
using Microsoft.AspNetCore.Mvc;

namespace CafePOS.Api.Utilities;

public static class CsvExport
{
    public static FileContentResult ToCsv<T>(IEnumerable<T> rows, string fileName)
    {
        using var memory = new MemoryStream();
        using (var writer = new StreamWriter(memory, new UTF8Encoding(true), leaveOpen: true))
        using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
        {
            csv.WriteRecords(rows);
        }
        return new FileContentResult(memory.ToArray(), "text/csv") { FileDownloadName = fileName };
    }
}
