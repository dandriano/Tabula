namespace Tabula.IO;

/// <summary>
/// An IOFormat determines how to read a given XML file containing a
/// spreadsheet workbook.
/// Currently, only Excel 2003 XMLSS format is supported.
/// </summary>
internal abstract class IOFormat
{
    public abstract Workbook Read(string filename);

    private string _fileExtension;
    private string _description;

    public string GetFilter()
    {
        return _description + " (*." + _fileExtension + ")|*." + _fileExtension;
    }

    public IOFormat(string fileextension, string description)
    {
        _fileExtension = fileextension;
        _description = description;
    }

    public bool ValidExtension(string ext)
    {
        return _fileExtension == ext;
    }
}
