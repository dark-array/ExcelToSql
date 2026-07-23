using System.Text.Encodings.Web;
using System.Text.Json;
using ExcelToSql.Models;
using ExcelToSql.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExcelToSql.Pages;

[RequestSizeLimit(50 * 1024 * 1024)]
[RequestFormLimits(MultipartBodyLengthLimit = 50 * 1024 * 1024, ValueCountLimit = 20_000)]
public class IndexModel : PageModel
{
    private const string SessionKey = "uploadId";
    private readonly UploadedFileStore _store;
    private readonly SpreadsheetReader _reader;
    private readonly SqlScriptGenerator _generator;

    public IndexModel(UploadedFileStore store, SpreadsheetReader reader, SqlScriptGenerator generator)
    {
        _store = store;
        _reader = reader;
        _generator = generator;
    }

    [BindProperty]
    public ConversionForm Input { get; set; } = new();

    [BindProperty]
    public SheetForm Editor { get; set; } = new();

    [BindProperty]
    public int EditorIndex { get; set; } = -1;

    [BindProperty]
    public string SelectedSheetName { get; set; } = string.Empty;

    [BindProperty]
    public int ItemIndex { get; set; } = -1;

    [BindProperty]
    public IFormFile? Upload { get; set; }

    public string Step { get; set; } = "upload";
    public string? FileName { get; set; }
    public string? ErrorMessage { get; set; }
    public string? GeneratedSql { get; set; }
    public SpreadsheetDocument? Document { get; set; }
    public string PreviewJson { get; private set; } = "{}";

    public IEnumerable<SheetData> UnusedSheets =>
        Document?.Sheets.Where(sheet =>
            !Input.Sheets.Any(saved =>
                string.Equals(saved.SheetName, sheet.Name, StringComparison.OrdinalIgnoreCase))
            && !string.Equals(sheet.Name, Editor.SheetName, StringComparison.OrdinalIgnoreCase))
        ?? [];

    public SheetData? EditorSheet =>
        Document?.Sheets.FirstOrDefault(sheet =>
            string.Equals(sheet.Name, Editor.SheetName, StringComparison.OrdinalIgnoreCase));

    public bool IsEditing => EditorIndex >= 0;

    public void OnGet()
    {
        Step = "upload";
    }

    public IActionResult OnPostUpload()
    {
        if (Upload is null || Upload.Length == 0)
            return PageWithError("Seleccione un archivo Excel o CSV.");

        var ext = Path.GetExtension(Upload.FileName);
        if (!SpreadsheetReader.IsSupported(ext))
            return PageWithError("Formato no soportado. Use xls, xlsx, xlsm, csv o tsv.");

        try
        {
            ClearUpload();
            var id = _store.Save(Upload);
            HttpContext.Session.SetString(SessionKey, id);
            LoadDocument(keepSelections: false);
            return Configure();
        }
        catch (Exception ex)
        {
            return PageWithError(ex.Message);
        }
    }

    public IActionResult OnPostRefresh()
    {
        try
        {
            LoadDocument(keepSelections: true);
            return Configure();
        }
        catch (Exception ex)
        {
            return PageWithError(ex.Message);
        }
    }

    public IActionResult OnPostLoadSheet()
    {
        try
        {
            LoadDocument(keepSelections: true);
            if (!string.IsNullOrWhiteSpace(Editor.SheetName))
                return Configure("Guarde o cancele la hoja actual antes de cargar otra.");

            if (string.IsNullOrWhiteSpace(SelectedSheetName))
                return Configure("Seleccione una hoja.");

            var sheet = Document!.Sheets.FirstOrDefault(s =>
                string.Equals(s.Name, SelectedSheetName, StringComparison.OrdinalIgnoreCase));
            if (sheet is null)
                return Configure("La hoja no existe en el archivo.");

            if (Input.Sheets.Any(s =>
                    string.Equals(s.SheetName, sheet.Name, StringComparison.OrdinalIgnoreCase)))
                return Configure("Esa hoja ya está en la lista. Use Editar.");

            Editor = MapSheet(sheet, []);
            EditorIndex = -1;
            SelectedSheetName = string.Empty;
            return Configure();
        }
        catch (Exception ex)
        {
            return PageWithError(ex.Message);
        }
    }

    public IActionResult OnPostSaveSheet()
    {
        try
        {
            LoadDocument(keepSelections: true);
            var error = SaveEditorToList();
            return Configure(error);
        }
        catch (Exception ex)
        {
            return PageWithError(ex.Message);
        }
    }

    public IActionResult OnPostCancelEditor()
    {
        try
        {
            LoadDocument(keepSelections: true);
            ClearEditor();
            return Configure();
        }
        catch (Exception ex)
        {
            return PageWithError(ex.Message);
        }
    }

    public IActionResult OnPostEditSheet()
    {
        try
        {
            LoadDocument(keepSelections: true);
            if (ItemIndex < 0 || ItemIndex >= Input.Sheets.Count)
                return Configure("No se encontró la hoja a editar.");

            Editor = CloneSheet(Input.Sheets[ItemIndex]);
            EditorIndex = ItemIndex;
            return Configure();
        }
        catch (Exception ex)
        {
            return PageWithError(ex.Message);
        }
    }

    public IActionResult OnPostRemoveSheet()
    {
        try
        {
            LoadDocument(keepSelections: true);
            if (ItemIndex < 0 || ItemIndex >= Input.Sheets.Count)
                return Configure("No se encontró la hoja a eliminar.");

            Input.Sheets.RemoveAt(ItemIndex);
            if (EditorIndex == ItemIndex)
                ClearEditor();
            else if (EditorIndex > ItemIndex)
                EditorIndex--;

            return Configure();
        }
        catch (Exception ex)
        {
            return PageWithError(ex.Message);
        }
    }

    public IActionResult OnPostGenerate()
    {
        try
        {
            LoadDocument(keepSelections: true);
            if (!string.IsNullOrWhiteSpace(Editor.SheetName) && Editor.Columns.Count > 0)
            {
                var saveError = SaveEditorToList();
                if (saveError is not null)
                    return Configure(saveError);
            }

            if (Input.Sheets.Count == 0)
                return Configure("Agregue al menos una hoja a la lista antes de generar.");

            if (Document is null)
                return PageWithError("No hay un archivo cargado.");

            GeneratedSql = _generator.Generate(Document, Input);
            var id = HttpContext.Session.GetString(SessionKey);
            if (!string.IsNullOrEmpty(id))
                _store.SaveSql(id, GeneratedSql);

            Step = "result";
            return Page();
        }
        catch (Exception ex)
        {
            try
            {
                LoadDocument(keepSelections: true);
                return Configure(ex.Message);
            }
            catch
            {
                return PageWithError(ex.Message);
            }
        }
    }

    public IActionResult OnPostAdjust()
    {
        try
        {
            LoadDocument(keepSelections: true);
            ClearEditor();
            return Configure();
        }
        catch (Exception ex)
        {
            return PageWithError(ex.Message);
        }
    }

    public IActionResult OnPostReset()
    {
        ClearUpload();
        Input = new ConversionForm();
        ClearEditor();
        Step = "upload";
        return Page();
    }

    public IActionResult OnGetDownload()
    {
        var id = HttpContext.Session.GetString(SessionKey);
        if (string.IsNullOrEmpty(id))
            return RedirectToPage();

        var sql = _store.TryReadSql(id);
        if (string.IsNullOrEmpty(sql))
            return RedirectToPage();

        var name = Path.GetFileNameWithoutExtension(_store.GetOriginalName(id));
        return File(System.Text.Encoding.UTF8.GetBytes(sql), "application/sql", name + ".sql");
    }

    private string? SaveEditorToList()
    {
        if (string.IsNullOrWhiteSpace(Editor.SheetName) || Editor.Columns.Count == 0)
            return "Cargue una hoja antes de guardarla.";

        if (!Editor.Columns.Any(c => c.Selected))
            return "Seleccione al menos una columna.";

        Editor.Included = true;
        if (string.IsNullOrWhiteSpace(Editor.TableName))
            Editor.TableName = IdentifierSanitizer.Sanitize(Editor.SheetName);

        var saved = CloneSheet(Editor);
        if (EditorIndex >= 0 && EditorIndex < Input.Sheets.Count)
        {
            Input.Sheets[EditorIndex] = saved;
        }
        else
        {
            var existing = Input.Sheets.FindIndex(s =>
                string.Equals(s.SheetName, saved.SheetName, StringComparison.OrdinalIgnoreCase));
            if (existing >= 0)
                Input.Sheets[existing] = saved;
            else
                Input.Sheets.Add(saved);
        }

        ClearEditor();
        return null;
    }

    private void LoadDocument(bool keepSelections)
    {
        var id = HttpContext.Session.GetString(SessionKey);
        if (string.IsNullOrEmpty(id))
            throw new InvalidOperationException("No hay un archivo cargado. Suba un Excel o CSV.");

        var path = _store.GetFilePath(id);
        FileName = _store.GetOriginalName(id);
        Document = _reader.Read(path, FileName, Input.FirstRowIsHeader, Input.Engine);

        if (!keepSelections)
        {
            Input.Sheets = [];
            ClearEditor();
            BuildPreviewJson();
            return;
        }

        Input.Sheets = RemapSheets(Input.Sheets);
        if (!string.IsNullOrWhiteSpace(Editor.SheetName))
        {
            var sheet = Document.Sheets.FirstOrDefault(s =>
                string.Equals(s.Name, Editor.SheetName, StringComparison.OrdinalIgnoreCase));
            if (sheet is null)
                ClearEditor();
            else
                Editor = MapSheet(sheet, [Editor]);
        }

        BuildPreviewJson();
    }

    private void BuildPreviewJson()
    {
        if (Document is null)
        {
            PreviewJson = "{}";
            return;
        }

        var payload = Document.Sheets.ToDictionary(
            sheet => sheet.Name,
            sheet => new
            {
                headers = sheet.Headers,
                totalRows = sheet.Rows.Count,
                rows = sheet.Rows.Take(500).ToList()
            });

        PreviewJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }).Replace("<", "\\u003c", StringComparison.Ordinal);
    }

    private List<SheetForm> RemapSheets(List<SheetForm> previous)
    {
        var result = new List<SheetForm>();
        foreach (var saved in previous)
        {
            var sheet = Document!.Sheets.FirstOrDefault(s =>
                string.Equals(s.Name, saved.SheetName, StringComparison.OrdinalIgnoreCase));
            if (sheet is null)
                continue;
            result.Add(MapSheet(sheet, [saved]));
        }

        return result;
    }

    private static SheetForm MapSheet(SheetData sheet, List<SheetForm> previous)
    {
        var prior = previous.FirstOrDefault(s =>
            string.Equals(s.SheetName, sheet.Name, StringComparison.OrdinalIgnoreCase));

        var form = new SheetForm
        {
            Included = true,
            SheetName = sheet.Name,
            TableName = string.IsNullOrWhiteSpace(prior?.TableName)
                ? IdentifierSanitizer.Sanitize(sheet.Name)
                : prior!.TableName,
            IdentityColumn = prior?.IdentityColumn ?? GuessIdentity(sheet)
        };

        foreach (var column in sheet.Columns)
        {
            var priorCol = prior?.Columns.FirstOrDefault(c =>
                string.Equals(c.OriginalName, column.OriginalName, StringComparison.OrdinalIgnoreCase));

            form.Columns.Add(new ColumnForm
            {
                Selected = priorCol?.Selected ?? true,
                OriginalName = column.OriginalName,
                SqlName = string.IsNullOrWhiteSpace(priorCol?.SqlName) ? column.SqlName : priorCol!.SqlName,
                DataType = priorCol?.DataType ?? column.DataType,
                Length = priorCol?.Length ?? column.Length,
                Precision = priorCol?.Precision ?? column.Precision,
                Scale = priorCol?.Scale ?? column.Scale,
                StripChars = priorCol?.StripChars ?? string.Empty
            });
        }

        if (!form.Columns.Any(c =>
                string.Equals(c.OriginalName, form.IdentityColumn, StringComparison.OrdinalIgnoreCase)))
            form.IdentityColumn = string.Empty;

        return form;
    }

    private static SheetForm CloneSheet(SheetForm source) => new()
    {
        Included = true,
        SheetName = source.SheetName,
        TableName = source.TableName,
        IdentityColumn = source.IdentityColumn,
        Columns = source.Columns.Select(c => new ColumnForm
        {
            Selected = c.Selected,
            OriginalName = c.OriginalName,
            SqlName = c.SqlName,
            DataType = c.DataType,
            Length = c.Length,
            Precision = c.Precision,
            Scale = c.Scale,
            StripChars = c.StripChars
        }).ToList()
    };

    private static string GuessIdentity(SheetData sheet)
    {
        var match = sheet.Columns.FirstOrDefault(c =>
            c.DataType is LogicalType.Integer or LogicalType.BigInteger &&
            (c.OriginalName.Equals("id", StringComparison.OrdinalIgnoreCase)
             || c.OriginalName.Equals("codigo", StringComparison.OrdinalIgnoreCase)
             || c.SqlName.Equals("id", StringComparison.OrdinalIgnoreCase)));
        return match?.OriginalName ?? string.Empty;
    }

    private void ClearEditor()
    {
        Editor = new SheetForm();
        EditorIndex = -1;
        SelectedSheetName = string.Empty;
    }

    private void ClearUpload()
    {
        var id = HttpContext.Session.GetString(SessionKey);
        if (!string.IsNullOrEmpty(id))
            _store.Delete(id);
        HttpContext.Session.Remove(SessionKey);
        Document = null;
        FileName = null;
        GeneratedSql = null;
        ClearEditor();
    }

    private PageResult Configure(string? error = null)
    {
        ErrorMessage = error;
        Step = "configure";
        return Page();
    }

    private PageResult PageWithError(string message)
    {
        ErrorMessage = message;
        Step = "upload";
        return Page();
    }
}
