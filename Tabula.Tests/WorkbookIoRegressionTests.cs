using System;
using System.IO;
using Tabula.IO;
using Tabula.Values;
using Xunit;

namespace Tabula.Tests;

public sealed class WorkbookIoRegressionTests
{
    [Fact]
    public void ReportsUnsupportedMissingAndMalformedWorkbookFiles()
    {
        using var temp = new TemporaryDirectory();
        string unsupportedPath = temp.File("not-a-workbook.xlsx");
        Assert.False(WorkbookIO.TryRead(unsupportedPath, out _, out string unsupportedError));
        Assert.Contains("Unsupported", unsupportedError);

        string missingPath = temp.File("missing.xml");
        Assert.False(WorkbookIO.TryRead(missingPath, out _, out string missingError));
        Assert.False(string.IsNullOrEmpty(missingError));
        Assert.Throws<IOException>(() => WorkbookIO.Read(unsupportedPath));

        string malformedPath = temp.File("malformed.xml");
        File.WriteAllText(malformedPath, "<Workbook>");
        Assert.False(WorkbookIO.TryRead(malformedPath, out _, out _));
    }

    [Fact]
    public void LoadsXmlssWorkbookWithR1C1Formula()
    {
        using var temp = new TemporaryDirectory();
        string path = temp.File("r1c1.xml");
        File.WriteAllText(path, """
			<?xml version="1.0"?>
			<Workbook xmlns="urn:schemas-microsoft-com:office:spreadsheet" xmlns:ss="urn:schemas-microsoft-com:office:spreadsheet">
			  <Worksheet ss:Name="Data"><Table ss:ExpandedColumnCount="3" ss:ExpandedRowCount="1">
			    <Row><Cell><Data ss:Type="Number">1</Data></Cell><Cell><Data ss:Type="Number">2</Data></Cell><Cell ss:Formula="=RC[-1]+RC[-2]"><Data ss:Type="Number">3</Data></Cell></Row>
			  </Table></Worksheet>
			</Workbook>
			""");

        Assert.True(WorkbookIO.TryRead(path, out Workbook workbook, out string error), error);
        Value result = workbook[0][2, 0].Eval(workbook[0], 2, 0);
        Assert.True(result is NumberValue, result?.ToString());
        Assert.Equal(3, ((NumberValue)result).Value);
    }

    [Fact]
    public void TreatsAtPrefixedSheetsAsOrdinaryAndUnknownCallsAsNameErrors()
    {
        using var temp = new TemporaryDirectory();
        string path = temp.File("sdf.xml");
        File.WriteAllText(path, """
			<?xml version="1.0"?>
			<Workbook xmlns="urn:schemas-microsoft-com:office:spreadsheet" xmlns:ss="urn:schemas-microsoft-com:office:spreadsheet">
			  <Worksheet ss:Name="@Functions"><Table ss:ExpandedColumnCount="1" ss:ExpandedRowCount="1">
			    <Row><Cell><Data ss:Type="Number">4</Data></Cell></Row>
			  </Table></Worksheet>
			  <Worksheet ss:Name="Data"><Table ss:ExpandedColumnCount="1" ss:ExpandedRowCount="1">
			    <Row><Cell ss:Formula="=DEFINE(&quot;ADD2&quot;,A1)"><Data ss:Type="String">unknown call</Data></Cell></Row>
			  </Table></Worksheet>
			</Workbook>
			""");

        Assert.True(WorkbookIO.TryRead(path, out Workbook workbook, out string error), error);
        Assert.Equal("@Functions", workbook[0].Name);
        Assert.Equal(4, Assert.IsType<NumberValue>(workbook[0][0, 0].Eval(workbook[0], 0, 0)).Value);
        Assert.Same(ErrorValue.NameError, workbook[1][0, 0].Eval(workbook[1], 0, 0));
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), "tabula-tests-" + Guid.NewGuid());

        public TemporaryDirectory() => Directory.CreateDirectory(_path);

        public string File(string name) => Path.Combine(_path, name);

        public void Dispose()
        {
            if (Directory.Exists(_path))
                Directory.Delete(_path, recursive: true);
        }
    }
}
