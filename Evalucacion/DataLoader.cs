using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;    // NuGet
using CsvHelper;          // NuGet
using CsvHelper.Configuration;

public static class DataLoader
{
    // Diccionario de alias de encabezados
    private static readonly Dictionary<string, string> MapCanonico =
        new(StringComparer.OrdinalIgnoreCase)
        {
            {"nombre","Nombre"},
            {"nombres","Nombre"},
            {"apellido","Apellido"},
            {"apellidos","Apellido"},
            {"municipio","Municipio"},
            {"mpio","Municipio"},
            {"entidad","Entidad"},
            {"estado","Entidad"},
            {"estatus","Estatus"},
            {"estatusafiliacion","Estatus"},
            {"afiliacion","Estatus"},
            {"fechaafiliacion","FechaAfiliacion"},
            {"fecha de afiliacion","FechaAfiliacion"},
            {"fecha_afiliacion","FechaAfiliacion"},
        };

    public static DataTable CargarTabla(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".xlsx" => LeerXlsx(path),
            ".csv" => LeerCsv(path),
            ".json" => LeerJson(path),
            _ => throw new NotSupportedException($"Extensión no soportada: {ext}")
        };
    }

    // ===== Métodos privados =====


    private static void NumerarFilas(DataTable dt, int offset)
    {
        if (!dt.Columns.Contains("FilaExcel"))
            dt.Columns.Add("FilaExcel", typeof(int));
        for (int i = 0; i < dt.Rows.Count; i++)
            dt.Rows[i]["FilaExcel"] = offset + i;
    }

    private static DataTable LeerXlsx(string path)
    {
        var dt = new DataTable();
        using var wb = new XLWorkbook(path);
        var ws = wb.Worksheet(1);

        bool first = true;
        var headers = new List<string>();

        // Número de fila del encabezado según Excel
        int headerRowNum = ws.FirstRowUsed().RowNumber(); // ej. 1 si el encabezado está en la fila 1
        int dataStartRowNum = headerRowNum + 1;           // primera fila de datos en Excel

        foreach (var row in ws.RowsUsed())
        {
            if (first)
            {
                foreach (var cell in row.CellsUsed())
                {
                    string h = NormalizarColumna(cell.GetString());
                    headers.Add(h);
                    dt.Columns.Add(h, typeof(string));
                }
                first = false;
            }
            else
            {
                var dr = dt.NewRow();
                int i = 0;
                foreach (var cell in row.Cells(1, headers.Count))
                    dr[i++] = cell.GetString();
                dt.Rows.Add(dr);
            }
        }

        // Numerar filas como en Excel (2, 3, 4, … si el encabezado está en 1)
        NumerarFilas(dt, dataStartRowNum);

        PostProcesarTipos(dt);
        return dt;
    }

    private static DataTable LeerCsv(string path)
    {
        var dt = new DataTable();
        using var reader = new StreamReader(path, DetectEncoding(path));
        var cfg = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            DetectDelimiter = true,
            BadDataFound = null,
            MissingFieldFound = null,
            IgnoreBlankLines = true
        };
        using var csv = new CsvReader(reader, cfg);

        csv.Read();
        csv.ReadHeader();
        var headers = csv.HeaderRecord.Select(NormalizarColumna).ToArray();
        foreach (var h in headers) dt.Columns.Add(h, typeof(string));

        while (csv.Read())
        {
            var dr = dt.NewRow();
            for (int i = 0; i < headers.Length; i++)
                dr[i] = csv.GetField(i);
            dt.Rows.Add(dr);
        }
        NumerarFilas(dt, 2); // datos empiezan en “2” tras encabezado en “1”

        PostProcesarTipos(dt);
        return dt;
    }

    private static DataTable LeerJson(string path)
    {
        var dt = new DataTable();
        using var fs = File.OpenRead(path);
        using var doc = JsonDocument.Parse(fs);

        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            throw new Exception("JSON debe ser un arreglo de objetos");

        var props = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var el in doc.RootElement.EnumerateArray())
            foreach (var p in el.EnumerateObject())
                props.Add(NormalizarColumna(p.Name));

        foreach (var p in props) dt.Columns.Add(p, typeof(string));

        foreach (var el in doc.RootElement.EnumerateArray())
        {
            var dr = dt.NewRow();
            foreach (var p in el.EnumerateObject())
            {
                string col = NormalizarColumna(p.Name);
                if (!dt.Columns.Contains(col)) continue;
                dr[col] = p.Value.ValueKind switch
                {
                    JsonValueKind.String => p.Value.GetString(),
                    JsonValueKind.Number => p.Value.GetRawText(),
                    JsonValueKind.True or JsonValueKind.False => p.Value.GetBoolean().ToString(),
                    _ => p.Value.GetRawText()
                };
            }
            dt.Rows.Add(dr);
        }
        NumerarFilas(dt, 2); // datos empiezan en “2” tras encabezado en “1”

        PostProcesarTipos(dt);
        return dt;
    }

    private static Encoding DetectEncoding(string path)
    {
        using var fs = File.OpenRead(path);
        using var br = new BinaryReader(fs, Encoding.Default, leaveOpen: true);
        var bom = br.ReadBytes(3);
        if (bom.Length >= 3 && bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
            return new UTF8Encoding(true);
        return Encoding.Default;
    }

    private static string NormalizarColumna(string raw)
    {
        string s = (raw ?? string.Empty).Trim();
        string sinAcentos = RemoverAcentos(s).Replace(" ", "").Replace("-", "").Replace("_", "");
        return MapCanonico.TryGetValue(sinAcentos, out var canon) ? canon : s;
    }

    private static string RemoverAcentos(string input)
    {
        var norm = input.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in norm)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(c);
            if (cat != UnicodeCategory.NonSpacingMark) sb.Append(c);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private static void PostProcesarTipos(DataTable dt)
    {
        // FechaAfiliacion → DateTime
        if (dt.Columns.Contains("FechaAfiliacion"))
        {
            var colFecha = new DataColumn("__FechaDT", typeof(DateTime));
            dt.Columns.Add(colFecha);
            foreach (DataRow r in dt.Rows)
            {
                var txt = r["FechaAfiliacion"]?.ToString();
                if (DateTime.TryParse(txt, CultureInfo.GetCultureInfo("es-MX"), DateTimeStyles.None, out var f) ||
                    DateTime.TryParse(txt, CultureInfo.InvariantCulture, DateTimeStyles.None, out f))
                {
                    r[colFecha] = f;
                }
            }
            dt.Columns.Remove("FechaAfiliacion");
            colFecha.ColumnName = "FechaAfiliacion";
        }

        // Estatus → “Afiliado/No afiliado”
        if (dt.Columns.Contains("Estatus"))
        {
            foreach (DataRow r in dt.Rows)
            {
                string v = (r["Estatus"]?.ToString() ?? "").Trim().ToLowerInvariant();
                if (v == "1" || v.Contains("afiliado")) r["Estatus"] = "Afiliado";
                else if (v == "0" || v.Contains("no afili")) r["Estatus"] = "No afiliado";
            }
        }

        // Asegura columnas clave
        foreach (var col in new[] { "Nombre", "Entidad", "Municipio", "Estatus" })
            if (!dt.Columns.Contains(col)) dt.Columns.Add(col, typeof(string));
    }
}
