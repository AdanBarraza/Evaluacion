using System;                               // Tipos base (Exception, etc.)
using System.Collections.Generic;           // Dictionary, HashSet, List
using System.Data;                          // DataTable, DataRow, DataColumn
using System.Globalization;                 // CultureInfo para parsear fechas
using System.IO;                            // File, Path, StreamReader
using System.Linq;                          // LINQ (Select, Where, Distinct, etc.)
using System.Text;                          // StringBuilder, Encoding
using System.Text.Json;                     // System.Text.Json para leer JSON
using ClosedXML.Excel;    // NuGet          // ClosedXML: para leer XLSX
using CsvHelper;          // NuGet          // CsvHelper: para leer CSV
using CsvHelper.Configuration;              // Configuración de CsvHelper

public static class DataLoader
{
    // Diccionario para mapear encabezados "variantes" a nombres canónicos.
    // Ej.: "estado", "Entidad" → "Entidad". Esto permite que los archivos
    // lleguen con columnas con nombres diferentes pero se unifiquen en el DataTable.
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

    // Punto de entrada: según la extensión, delega a XLSX/CSV/JSON.
    public static DataTable CargarTabla(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant(); // ".xlsx", ".csv", ".json"
        return ext switch
        {
            ".xlsx" => LeerXlsx(path), // Excel
            ".csv" => LeerCsv(path),  // CSV
            ".json" => LeerJson(path), // JSON
            _ => throw new NotSupportedException($"Extensión no soportada: {ext}")
        };
    }

    // ===== Métodos privados =====

    // Agrega/actualiza una columna "FilaExcel" con el número de fila "humano"
    // tal como se vería en Excel (offset suele ser 2 si fila 1 es encabezado).
    private static void NumerarFilas(DataTable dt, int offset)
    {
        if (!dt.Columns.Contains("FilaExcel"))
            dt.Columns.Add("FilaExcel", typeof(int));
        for (int i = 0; i < dt.Rows.Count; i++)
            dt.Rows[i]["FilaExcel"] = offset + i; // fila 0 -> offset, 1 -> offset+1, etc.
    }

    // Lee la PRIMERA hoja de un XLSX a DataTable, tomando la primera fila como encabezados.
    private static DataTable LeerXlsx(string path)
    {
        var dt = new DataTable();

        // Abre el workbook desde archivo (ClosedXML se encarga del parseo).
        using var wb = new XLWorkbook(path);
        var ws = wb.Worksheet(1); // hoja 1 (1-based)

        bool first = true;                 // bandera para saber si estamos en la fila de encabezados
        var headers = new List<string>();  // lista de nombres de columna canónicos

        // Para numeración: determina en qué fila inicia el encabezado en Excel
        int headerRowNum = ws.FirstRowUsed().RowNumber(); // normalmente 1
        int dataStartRowNum = headerRowNum + 1;           // datos empiezan justo debajo

        // Recorre solo las filas "usadas" (ClosedXML ignora vacías fuera de rango).
        foreach (var row in ws.RowsUsed())
        {
            if (first)
            {
                // La primera fila usada se trata como encabezado:
                foreach (var cell in row.CellsUsed())
                {
                    string h = NormalizarColumna(cell.GetString()); // mapea encabezado → canónico
                    headers.Add(h);
                    dt.Columns.Add(h, typeof(string));               // todas como string inicialmente
                }
                first = false;
            }
            else
            {
                // Resto de filas: datos
                var dr = dt.NewRow();
                int i = 0;
                // Toma celdas desde la 1 hasta la cantidad de encabezados esperados
                foreach (var cell in row.Cells(1, headers.Count))
                    dr[i++] = cell.GetString(); // texto de la celda
                dt.Rows.Add(dr);
            }
        }

        // Crea/actualiza la columna "FilaExcel" como se ve en Excel (2,3,4,...)
        NumerarFilas(dt, dataStartRowNum);

        // Post-proceso: fechas, estatus y columnas mínimas
        PostProcesarTipos(dt);
        return dt;
    }

    // Lee un CSV asumiendo primera fila como encabezado (auto-detecta separador).
    private static DataTable LeerCsv(string path)
    {
        var dt = new DataTable();

        // Crea StreamReader con la codificación detectada (BOM UTF-8 o Default)
        using var reader = new StreamReader(path, DetectEncoding(path));

        // Configuración del lector CSV (CsvHelper)
        var cfg = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,   // hay encabezado
            DetectDelimiter = true,   // intenta detectar ',' ';' '\t'
            BadDataFound = null,      // ignora registros mal formados
            MissingFieldFound = null, // ignora faltantes
            IgnoreBlankLines = true   // salta líneas en blanco
        };
        using var csv = new CsvReader(reader, cfg);

        // Lee encabezados y crea columnas en DataTable con nombres canónicos
        csv.Read();
        csv.ReadHeader();
        var headers = csv.HeaderRecord.Select(NormalizarColumna).ToArray();
        foreach (var h in headers) dt.Columns.Add(h, typeof(string));

        // Lee cada registro y llena DataTable como texto
        while (csv.Read())
        {
            var dr = dt.NewRow();
            for (int i = 0; i < headers.Length; i++)
                dr[i] = csv.GetField(i); // obtiene campo i como string
            dt.Rows.Add(dr);
        }

        // En CSV el encabezado es la fila 1, por lo que los datos inician en 2
        NumerarFilas(dt, 2);

        PostProcesarTipos(dt);
        return dt;
    }

    // Lee un JSON con formato: [ {objeto}, {objeto}, ... ]
    private static DataTable LeerJson(string path)
    {
        var dt = new DataTable();

        // Abre el archivo y parsea todo el documento JSON
        using var fs = File.OpenRead(path);
        using var doc = JsonDocument.Parse(fs);

        // Validación: la raíz debe ser un arreglo (lista de registros)
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            throw new Exception("JSON debe ser un arreglo de objetos");

        // Descubre TODAS las propiedades presentes para crear columnas
        var props = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var el in doc.RootElement.EnumerateArray())
            foreach (var p in el.EnumerateObject())
                props.Add(NormalizarColumna(p.Name)); // mapea a nombre canónico

        // Crea las columnas (como string)
        foreach (var p in props) dt.Columns.Add(p, typeof(string));

        // Recorre el arreglo y llena filas
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            var dr = dt.NewRow();
            foreach (var p in el.EnumerateObject())
            {
                string col = NormalizarColumna(p.Name); // nombre canónico de la propiedad
                if (!dt.Columns.Contains(col)) continue; // por seguridad

                // Guarda el valor como string (o texto crudo para números/otros)
                dr[col] = p.Value.ValueKind switch
                {
                    JsonValueKind.String => p.Value.GetString(),
                    JsonValueKind.Number => p.Value.GetRawText(), // mantiene forma original
                    JsonValueKind.True or JsonValueKind.False => p.Value.GetBoolean().ToString(),
                    _ => p.Value.GetRawText() // objetos/arrays anidados como JSON
                };
            }
            dt.Rows.Add(dr);
        }

        // Asumimos encabezados lógicos (propiedades) "en la fila 1", datos desde "2"
        NumerarFilas(dt, 2);

        PostProcesarTipos(dt);
        return dt;
    }

    // Detección rápida de codificación: si hay BOM UTF-8 usa UTF8, si no, default del sistema.
    private static Encoding DetectEncoding(string path)
    {
        using var fs = File.OpenRead(path);
        using var br = new BinaryReader(fs, Encoding.Default, leaveOpen: true);
        var bom = br.ReadBytes(3);
        if (bom.Length >= 3 && bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
            return new UTF8Encoding(true); // UTF-8 con BOM
        return Encoding.Default;           // otra codificación (ej. ANSI/Windows-1252)
    }

    // Normaliza el nombre de una columna: quita espacios/acentos/guiones/underscores y mapea.
    private static string NormalizarColumna(string raw)
    {
        string s = (raw ?? string.Empty).Trim(); // protege null y recorta
        // Quita acentos y separadores para comparar en el diccionario
        string sinAcentos = RemoverAcentos(s).Replace(" ", "").Replace("-", "").Replace("_", "");
        // Si existe un mapeo, usa el nombre canónico, si no, se queda como vino
        return MapCanonico.TryGetValue(sinAcentos, out var canon) ? canon : s;
    }

    // Quita acentos usando normalización Unicode (decompone y filtra NonSpacingMark)
    private static string RemoverAcentos(string input)
    {
        var norm = input.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in norm)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(c);
            if (cat != UnicodeCategory.NonSpacingMark) sb.Append(c); // ignora marcas (acentos)
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    // Ajustes finales: convierte FechaAfiliacion a DateTime, normaliza Estatus
    // y asegura columnas mínimas (Nombre, Entidad, Municipio, Estatus).
    private static void PostProcesarTipos(DataTable dt)
    {
        // Si hay columna "FechaAfiliacion" (texto), crea una DateTime y la sustituye
        if (dt.Columns.Contains("FechaAfiliacion"))
        {
            var colFecha = new DataColumn("__FechaDT", typeof(DateTime));
            dt.Columns.Add(colFecha);

            foreach (DataRow r in dt.Rows)
            {
                var txt = r["FechaAfiliacion"]?.ToString();
                // Intenta parsear con es-MX o InvariantCulture
                if (DateTime.TryParse(txt, CultureInfo.GetCultureInfo("es-MX"), DateTimeStyles.None, out var f) ||
                    DateTime.TryParse(txt, CultureInfo.InvariantCulture, DateTimeStyles.None, out f))
                {
                    r[colFecha] = f; // guarda fecha válida
                }
            }

            // Quita la de texto y renombra la DateTime al nombre original
            dt.Columns.Remove("FechaAfiliacion");
            colFecha.ColumnName = "FechaAfiliacion";
        }

        // Normaliza Estatus a "Afiliado"/"No afiliado" con reglas básicas
        if (dt.Columns.Contains("Estatus"))
        {
            foreach (DataRow r in dt.Rows)
            {
                string v = (r["Estatus"]?.ToString() ?? "").Trim().ToLowerInvariant();
                if (v == "1" || v.Contains("afiliado")) r["Estatus"] = "Afiliado";
                else if (v == "0" || v.Contains("no afili")) r["Estatus"] = "No afiliado";
            }
        }

        // Asegura que existan las columnas clave; si faltan, se crean vacías (string)
        foreach (var col in new[] { "Nombre", "Entidad", "Municipio", "Estatus" })
            if (!dt.Columns.Contains(col)) dt.Columns.Add(col, typeof(string));
    }
}

