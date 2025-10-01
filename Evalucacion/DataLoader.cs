using System;                               // Tipos base (Exception, DateTime, etc.)
using System.Collections.Generic;           // Dictionary, List, HashSet
using System.Data;                          // DataTable, DataRow, DataColumn
using System.Globalization;                 // CultureInfo y DateTimeStyles para parsear fechas
using System.IO;                            // File, Path, FileStream
using System.Linq;                          // LINQ (Select, Where, Distinct, etc.)
using System.Text;                          // StringBuilder, Encoding
using ClosedXML.Excel;    // NuGet          // Librería para leer archivos .xlsx de forma sencilla

// Clase utilitaria estática: abre un archivo .xlsx y lo convierte a un DataTable “uniformado”
// con nombres de columnas canónicos y tipos útiles (por ejemplo, FechaAfiliacion → DateTime).
public static class DataLoader
{
    // ===== Normalización de encabezados =====
    // Algunos archivos vienen con encabezados diferentes para la misma columna.
    // Este diccionario mapea variantes “de la vida real” a un nombre canónico interno.
    // Ejemplo: "estado", "Entidad", "ESTADO" => "Entidad".
    private static readonly Dictionary<string, string> MapCanonico =
        new(StringComparer.OrdinalIgnoreCase) // comparación sin distinguir mayúsc/minúsc.
        {
            {"nombre","Nombre"},
            {"nombres","Nombre"},
            {"apellido","Apellido"},          // NOTA: tu app actual no usa "Apellido", pero se mapea por si aparece.
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

    // ===== Punto de entrada =====
    // Recibe la ruta del archivo, valida extensión y delega la lectura.
    public static DataTable CargarTabla(string path)
    {
        // Obtiene la extensión en minúsculas (".xlsx", ".csv", etc.)
        string ext = Path.GetExtension(path).ToLowerInvariant();

        // Solo aceptamos .xlsx; cualquier otra extensión se rechaza explícitamente.
        return ext switch
        {
            ".xlsx" => LeerXlsx(path), // Lectura de Excel
            _ => throw new NotSupportedException($"Extensión no soportada: {ext} (solo .xlsx)")
        };
    }

    // ===== Métodos privados =====

    //Agrega una columna extra con el número de fila real en Excel,
    //para que el usuario pueda identificar rápidamente en qué fila estaba el registro original.
    private static void NumerarFilas(DataTable dt, int offset)
    {
        // Crea la columna si no existe aún (tipo int).
        if (!dt.Columns.Contains("FilaExcel"))
            dt.Columns.Add("FilaExcel", typeof(int));

        // Asigna a cada DataRow su número de fila equivalente en Excel: offset + índice local.
        for (int i = 0; i < dt.Rows.Count; i++)
            dt.Rows[i]["FilaExcel"] = offset + i; // fila 0 -> offset, fila 1 -> offset+1, etc.
    }

    // Lee la PRIMERA hoja del .xlsx y vuelca los datos a un DataTable:
    //  - La primera fila “usada” se toma como encabezados.
    //  - Todas las celdas se cargan inicialmente como string (post-procesamos después).
    private static DataTable LeerXlsx(string path)
    {
        var dt = new DataTable();

        // Abre el libro de Excel.
        // OPCIONAL: si quieres poder leer aunque el archivo esté abierto en Excel, usa:
        // using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        // using var wb = new XLWorkbook(fs);
        using var wb = new XLWorkbook(path);

        // Toma siempre la hoja 1 (ClosedXML es 1-based para worksheets).
        var ws = wb.Worksheet(1);

        bool first = true;                 // true mientras procesamos la fila de encabezados
        var headers = new List<string>();  // lista de nombres canónicos de las columnas

        // Para numerar filas como en Excel:
        // FirstRowUsed() devuelve la primera fila “con contenido/uso”.
        int headerRowNum = ws.FirstRowUsed().RowNumber(); // normalmente 1
        int dataStartRowNum = headerRowNum + 1;           // los datos empiezan justo debajo

        // Recorremos SOLO filas realmente usadas (ignora vacías al final).
        foreach (var row in ws.RowsUsed())
        {
            if (first)
            {
                // Esta es la fila de encabezados: definimos nombres de columnas.
                foreach (var cell in row.CellsUsed())
                {
                    // Se toma el texto del encabezado y se normaliza (quitar acentos/espacios y mapear a canónico).
                    string h = NormalizarColumna(cell.GetString());
                    headers.Add(h);

                    // Creamos la columna en el DataTable. Tipo string (ya convertiremos fechas luego).
                    dt.Columns.Add(h, typeof(string));
                }
                first = false; // las siguientes filas son datos
            }
            else
            {
                // Filas de datos:
                var dr = dt.NewRow();
                int i = 0;

                // Leemos tantas celdas como columnas de encabezado haya (si hay extras a la derecha, se ignoran).
                foreach (var cell in row.Cells(1, headers.Count))
                    dr[i++] = cell.GetString(); // ClosedXML devuelve string (incluso si la celda es numérica/fecha)

                dt.Rows.Add(dr);
            }
        }

        // Añade columna "FilaExcel" con numeración equivalente a Excel (2,3,4,...) en función del encabezado detectado.
        NumerarFilas(dt, dataStartRowNum);

        // Ajustes finales de tipos y valores canónicos (fechas, estatus, columnas mínimas).
        PostProcesarTipos(dt);
        return dt;
    }

    // (Actualmente NO se usa, porque solo admitimos .xlsx.)
    // Se deja como utilitario si en el futuro se reactivan CSV/otros formatos que lo necesiten.
    private static Encoding DetectEncoding(string path)
    {
        using var fs = File.OpenRead(path);
        using var br = new BinaryReader(fs, Encoding.Default, leaveOpen: true);
        var bom = br.ReadBytes(3);

        // Si el archivo tiene BOM UTF-8 (EF BB BF), devolvemos UTF8; en otro caso, codificación por defecto.
        if (bom.Length >= 3 && bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
            return new UTF8Encoding(true); // UTF-8 con BOM
        return Encoding.Default;           // ANSI / Windows-1252 u otra codificación local
    }

    // Normaliza nombres de columnas:
    //  - Quita tildes.
    //  - Elimina espacios, guiones y underscores para poder buscar en el diccionario.
    //  - Si hay mapeo, usa el nombre canónico; si no, conserva el original (respetando cómo venía).
    private static string NormalizarColumna(string raw)
    {
        string s = (raw ?? string.Empty).Trim(); // protege de null y quita espacios externos

        // Quitamos acentos y separadores (espacio, -, _) para comparar contra MapCanonico.
        string sinAcentos = RemoverAcentos(s).Replace(" ", "").Replace("-", "").Replace("_", "");

        // Intenta mapear a un nombre canónico; si no hay coincidencia, retorna el nombre original.
        return MapCanonico.TryGetValue(sinAcentos, out var canon) ? canon : s;
    }

    // Remueve acentos con normalización Unicode: descompone (FormD) y omite marcas diacríticas.
    private static string RemoverAcentos(string input)
    {
        var norm = input.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();

        foreach (var c in norm)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(c);
            if (cat != UnicodeCategory.NonSpacingMark) // omitimos las marcas (acentos)
                sb.Append(c);
        }

        // Volvemos a componer la cadena (FormC) ya sin acentos.
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    //Aquí se hacen ajustes a los datos que vienen de Excel.
    //Por ejemplo, convierte las fechas de texto a un formato de fecha real,
    //normaliza los estatus para que todos aparezcan como ‘Afiliado’ o ‘No afiliado’,
    //y se asegura de que existan las columnas más importantes como Nombre, Entidad y Municipio.”
    private static void PostProcesarTipos(DataTable dt)
    {
        // === 1) FechaAfiliacion -> DateTime ===
        if (dt.Columns.Contains("FechaAfiliacion"))
        {
            // Creamos una columna temporal de tipo DateTime.
            var colFecha = new DataColumn("__FechaDT", typeof(DateTime));
            dt.Columns.Add(colFecha);

            // Intentamos parsear cada fila; si parsea, se asigna la fecha; si no, queda DBNull (celda vacía).
            foreach (DataRow r in dt.Rows)
            {
                var txt = r["FechaAfiliacion"]?.ToString();

                // Probamos con cultura "es-MX" (formato local) y con InvariantCulture (ISO u otros).
                if (DateTime.TryParse(txt, CultureInfo.GetCultureInfo("es-MX"), DateTimeStyles.None, out var f) ||
                    DateTime.TryParse(txt, CultureInfo.InvariantCulture, DateTimeStyles.None, out f))
                {
                    r[colFecha] = f; // solo guardamos si la conversión fue exitosa
                }
            }

            // Eliminamos la columna original (string) y renombramos la DateTime al nombre original.
            dt.Columns.Remove("FechaAfiliacion");
            colFecha.ColumnName = "FechaAfiliacion";
        }

        // === 2) Estatus -> "Afiliado" / "No afiliado" ===
        if (dt.Columns.Contains("Estatus"))
        {
            foreach (DataRow r in dt.Rows)
            {
                string v = (r["Estatus"]?.ToString() ?? "").Trim().ToLowerInvariant();

                // Reglas simples de normalización: números o cadenas que contengan “afiliado/no afili…”
                if (v == "1" || v.Contains("afiliado"))
                    r["Estatus"] = "Afiliado";
                else if (v == "0" || v.Contains("no afili"))
                    r["Estatus"] = "No afiliado";
                // En cualquier otro caso, se deja tal cual vino (por si hay otras marcas).
            }
        }

        // === 3) Columnas mínimas aseguradas ===
        // Garantiza que el DataTable siempre tenga estas columnas, aunque el archivo no las traiga.
        // Se crean como string vacías para que la UI y los filtros no fallen por columna inexistente.
        foreach (var col in new[] { "Nombre", "Entidad", "Municipio", "Estatus" })
            if (!dt.Columns.Contains(col))
                dt.Columns.Add(col, typeof(string));
    }
}


