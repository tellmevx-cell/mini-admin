using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using MiniAdmin.Application.Contracts.Common;

namespace MiniAdmin.Infrastructure.Common;

public sealed class XlsxWorkbookService : IWorkbookService
{
    private static readonly XNamespace SpreadsheetNamespace =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    public byte[] CreateWorkbook(IReadOnlyList<IReadOnlyList<string>> rows)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            WriteArchiveEntry(
                archive,
                "[Content_Types].xml",
                """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                  <Default Extension="xml" ContentType="application/xml"/>
                  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
                  <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
                  <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
                </Types>
                """);
            WriteArchiveEntry(
                archive,
                "_rels/.rels",
                """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
                </Relationships>
                """);
            WriteArchiveEntry(
                archive,
                "xl/workbook.xml",
                """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <sheets>
                    <sheet name="Sheet1" sheetId="1" r:id="rId1"/>
                  </sheets>
                </workbook>
                """);
            WriteArchiveEntry(
                archive,
                "xl/_rels/workbook.xml.rels",
                """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
                  <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
                </Relationships>
                """);
            WriteArchiveEntry(
                archive,
                "xl/styles.xml",
                """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
                  <fonts count="1"><font><sz val="11"/><name val="Calibri"/></font></fonts>
                  <fills count="1"><fill><patternFill patternType="none"/></fill></fills>
                  <borders count="1"><border/></borders>
                  <cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
                  <cellXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/></cellXfs>
                </styleSheet>
                """);
            WriteArchiveEntry(
                archive,
                "xl/worksheets/sheet1.xml",
                CreateWorksheetXml(rows));
        }

        return stream.ToArray();
    }

    public IReadOnlyList<IReadOnlyList<string>> ReadWorkbook(Stream stream)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, false);
        var sharedStrings = ReadSharedStrings(archive);
        var sheetEntry = archive.GetEntry("xl/worksheets/sheet1.xml") ??
            throw new InvalidOperationException("Excel 文件中未找到 sheet1.");

        using var sheetStream = sheetEntry.Open();
        var document = XDocument.Load(sheetStream);
        var rows = new List<IReadOnlyList<string>>();

        foreach (var rowElement in document.Descendants(SpreadsheetNamespace + "row"))
        {
            var values = new SortedDictionary<int, string>();
            foreach (var cellElement in rowElement.Elements(SpreadsheetNamespace + "c"))
            {
                var cellReference = cellElement.Attribute("r")?.Value;
                if (string.IsNullOrWhiteSpace(cellReference))
                {
                    continue;
                }

                values[GetColumnIndex(cellReference)] = ReadCellValue(cellElement, sharedStrings);
            }

            if (values.Count == 0)
            {
                rows.Add([]);
                continue;
            }

            var maxIndex = values.Keys.Max();
            var row = new string[maxIndex + 1];
            foreach (var (index, value) in values)
            {
                row[index] = value;
            }

            rows.Add(row);
        }

        return rows;
    }

    private static string CreateWorksheetXml(IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var sheetData = new XElement(SpreadsheetNamespace + "sheetData",
            rows.Select((row, rowIndex) => new XElement(
                SpreadsheetNamespace + "row",
                new XAttribute("r", rowIndex + 1),
                row.Select((value, columnIndex) => new XElement(
                    SpreadsheetNamespace + "c",
                    new XAttribute("r", $"{GetColumnName(columnIndex + 1)}{rowIndex + 1}"),
                    new XAttribute("t", "inlineStr"),
                    new XElement(
                        SpreadsheetNamespace + "is",
                        new XElement(SpreadsheetNamespace + "t", value ?? string.Empty)))))));

        var worksheet = new XDocument(
            new XDeclaration("1.0", "UTF-8", "yes"),
            new XElement(SpreadsheetNamespace + "worksheet", sheetData));

        return worksheet.ToString(SaveOptions.DisableFormatting);
    }

    private static IReadOnlyList<string> ReadSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null)
        {
            return [];
        }

        using var stream = entry.Open();
        var document = XDocument.Load(stream);

        return document
            .Descendants(SpreadsheetNamespace + "si")
            .Select(item => string.Concat(item.Descendants(SpreadsheetNamespace + "t").Select(x => x.Value)))
            .ToArray();
    }

    private static string ReadCellValue(XElement cellElement, IReadOnlyList<string> sharedStrings)
    {
        var type = cellElement.Attribute("t")?.Value;
        if (string.Equals(type, "inlineStr", StringComparison.OrdinalIgnoreCase))
        {
            return cellElement
                .Element(SpreadsheetNamespace + "is")?
                .Element(SpreadsheetNamespace + "t")?
                .Value ?? string.Empty;
        }

        var value = cellElement.Element(SpreadsheetNamespace + "v")?.Value ?? string.Empty;
        if (string.Equals(type, "s", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sharedStringIndex) &&
            sharedStringIndex >= 0 &&
            sharedStringIndex < sharedStrings.Count)
        {
            return sharedStrings[sharedStringIndex];
        }

        return value;
    }

    private static void WriteArchiveEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(content);
    }

    private static string GetColumnName(int columnNumber)
    {
        var dividend = columnNumber;
        var columnName = string.Empty;
        while (dividend > 0)
        {
            var modulo = (dividend - 1) % 26;
            columnName = Convert.ToChar('A' + modulo) + columnName;
            dividend = (dividend - modulo) / 26;
        }

        return columnName;
    }

    private static int GetColumnIndex(string cellReference)
    {
        var letters = new string(cellReference.TakeWhile(char.IsLetter).ToArray());
        var index = 0;
        foreach (var letter in letters)
        {
            index *= 26;
            index += char.ToUpperInvariant(letter) - 'A' + 1;
        }

        return Math.Max(index - 1, 0);
    }
}
