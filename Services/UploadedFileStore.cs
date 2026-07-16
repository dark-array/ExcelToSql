using System.Text;
using System.Text.RegularExpressions;

namespace ExcelToSql.Services;

public sealed class UploadedFileStore
{
    private static readonly Regex SafeId = new("^[a-fA-F0-9]{32}$", RegexOptions.Compiled);
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ExcelToSql");

    public string Save(IFormFile file)
    {
        var id = Guid.NewGuid().ToString("N");
        var dir = Path.Combine(_root, id);
        Directory.CreateDirectory(dir);

        var safeName = Path.GetFileName(file.FileName);
        if (string.IsNullOrWhiteSpace(safeName))
            safeName = "archivo.xlsx";

        var path = Path.Combine(dir, safeName);
        using (var stream = File.Create(path))
            file.CopyTo(stream);

        File.WriteAllText(Path.Combine(dir, "name.txt"), safeName, Encoding.UTF8);
        return id;
    }

    public string GetFilePath(string id)
    {
        var dir = GetDir(id);
        var name = File.ReadAllText(Path.Combine(dir, "name.txt"), Encoding.UTF8).Trim();
        var path = Path.Combine(dir, name);
        if (!File.Exists(path))
            throw new FileNotFoundException("El archivo cargado ya no está disponible. Vuelva a subirlo.");
        return path;
    }

    public string GetOriginalName(string id)
        => File.ReadAllText(Path.Combine(GetDir(id), "name.txt"), Encoding.UTF8).Trim();

    public void SaveSql(string id, string sql)
        => File.WriteAllText(Path.Combine(GetDir(id), "output.sql"), sql, new UTF8Encoding(false));

    public string? TryReadSql(string id)
    {
        var path = Path.Combine(GetDir(id), "output.sql");
        return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : null;
    }

    public void Delete(string id)
    {
        if (!SafeId.IsMatch(id))
            return;

        var dir = Path.Combine(_root, id);
        if (Directory.Exists(dir))
            Directory.Delete(dir, true);
    }

    private string GetDir(string id)
    {
        if (!SafeId.IsMatch(id))
            throw new InvalidOperationException("Sesión de archivo inválida.");

        var dir = Path.Combine(_root, id);
        if (!Directory.Exists(dir))
            throw new FileNotFoundException("El archivo cargado ya no está disponible. Vuelva a subirlo.");
        return dir;
    }
}
