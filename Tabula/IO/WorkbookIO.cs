using System;
using System.IO;
using System.Xml;

namespace Tabula.IO;

/// <summary>Loads supported workbook formats without UI dependencies.</summary>
public static class WorkbookIO
{
    public static bool TryRead(string path, out Workbook workbook, out string error)
    {
        workbook = null;
        error = null;
        if (string.IsNullOrWhiteSpace(path))
        {
            error = "A workbook path is required.";
            return false;
        }
        if (!string.Equals(Path.GetExtension(path), ".xml", StringComparison.OrdinalIgnoreCase))
        {
            error = "Unsupported workbook format. Supported format: XMLSS (.xml).";
            return false;
        }
        try
        {
            workbook = new XMLSSIOFormat().Read(path);
            if (workbook == null)
            {
                error = "The file does not contain a supported XMLSS workbook.";
                return false;
            }
            return true;
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is XmlException || ex is ArgumentException || ex is InvalidOperationException)
        {
            workbook = null;
            error = ex.Message;
            return false;
        }
    }

    public static Workbook Read(string path)
    {
        if (TryRead(path, out Workbook workbook, out string error))
            return workbook;
        throw new IOException("Cannot read workbook: " + error);
    }

    public static string SupportedFormatFilter => "XMLSS (*.xml)|*.xml|All files (*.*)|*.*";
}

/// <summary>Compatibility wrapper for callers using the original spelling.</summary>
public sealed class WorkBookIO
{
    public Workbook Read(string filename) => WorkbookIO.Read(filename);
    public string SupportedFormatFilter() => WorkbookIO.SupportedFormatFilter;
    public int DefaultFormatIndex() => 1;
}
