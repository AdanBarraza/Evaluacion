using System.Data;                      // DataTable, DataView: estructuras tabulares y vistas filtrables
using System.IO;                        // Path, File: utilidades de sistema de archivos
using System.Linq;                      // LINQ para consultar DataTable (AsEnumerable, Select, Where, etc.)
using System.Collections.Generic;       // List<T>, HashSet<T>, comparadores

namespace Evalucacion
{
    public partial class AfiliacionPRI : Form
    {
        // Contenedor maestro de todos los registros cargados (puede venir de 1 o varios archivos).
        // Se inicializa vacío para evitar nulls.
        private DataTable _tabla = new();

        // Vista filtrada que envuelve a _tabla. Es lo que se enlaza al DataGridView para mostrar/filtrar.
        // Puede quedar null cuando no hay archivo cargado.
        private DataView _vista;

        // Registro de rutas de archivos ya cargados (si habilitas la prevención de duplicados).
        // Comparación case-insensitive para evitar reabrir el mismo con distinta capitalización.
        private HashSet<string> _archivosCargados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public AfiliacionPRI()
        {
            InitializeComponent(); // Crea y configura los controles diseñados en el formulario.
        }

        private void AfiliacionPRI_Load(object sender, EventArgs e)
        {
            // ===== Inicialización de combos/controles =====

            // Llena combo de Estatus con opciones fijas. "(Todos)" significa sin filtro.
            cboEstatus.Items.Clear();
            cboEstatus.Items.AddRange(new object[] { "(Todos)", "Afiliado", "No afiliado" });
            cboEstatus.SelectedIndex = 0; // valor por defecto

            // Configuración del DataGridView para buena performance con tablas grandes.
            dgvDatos.ReadOnly = true;                                // solo lectura
            dgvDatos.AllowUserToAddRows = false;                     // sin fila vacía al final
            dgvDatos.AllowUserToDeleteRows = false;                  // no se borran filas desde UI
            dgvDatos.RowHeadersVisible = false;                      // oculta encabezados de fila
            dgvDatos.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None; // evita autosize costoso
            dgvDatos.SelectionMode = DataGridViewSelectionMode.FullRowSelect;    // selecciona filas completas

            // Los DateTimePicker empiezan habilitados o no según el estado de sus CheckBox asociados.
            dtpDesde.Enabled = chkDesde.Checked;
            dtpHasta.Enabled = chkHasta.Checked;

            // Deja todo el formulario en estado “sin archivo” (grid vacío, filtros apagados, mensaje de ayuda).
            EstadoSinArchivo();
        }

        // NOTA DE DISEÑO:
        // Use Task.Run + await para cargar/parsear el archivo en segundo plano SIN congelar la UI.
        // Ventajas frente a Thread manual:
        // 1) await reanuda en el hilo de UI (no hace falta this.Invoke para tocar controles),
        // 2) las excepciones fluyen con try/catch normal,
        // 3) usa thread pool (más eficiente) y es el patrón moderno en .NET (TPL).
        // Si se exigiera Thread, habría que hacer this.Invoke al volver y capturar errores dentro del hilo.
        private async void btnAbrir_Click(object sender, EventArgs e)
        {
            // Diálogo estándar para elegir archivo. Filtra a solo .xlsx.
            using var ofd = new OpenFileDialog
            {
                Title = "Abrir padrón (XLSX)",
                Filter = "Excel (*.xlsx)|*.xlsx"
            };
            if (ofd.ShowDialog() != DialogResult.OK) return; // usuario canceló

            try
            {
                // Ruta completa del archivo seleccionado.
                string path = ofd.FileName;

                // Carga del archivo en segundo plano para NO bloquear la UI.
                // Task.Run delega al thread pool y 'await' regresa al hilo de UI para actualizar controles.
                DataTable nueva = await Task.Run(() => DataLoader.CargarTabla(path));

                // Determina si ya existían datos visibles en la tabla principal.
                bool hayDatos = _tabla != null && _tabla.Rows.Count > 0;

                if (!hayDatos)
                {
                    // Primera carga en esta sesión: se asigna directamente.
                    _tabla = nueva;
                }
                else
                {
                    // Ya hay datos: pregunta si quieres combinar (agregar) o reemplazar por completo.
                    var resp = MessageBox.Show(
                        "Ya hay un padrón cargado.\n\n¿Quieres AGREGAR (combinar) este archivo al actual?\n\nSí = Agregar (combinar)\nNo = Reemplazar\nCancelar = Volver",
                        "Cargar otro archivo",
                        MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Question);

                    if (resp == DialogResult.Cancel) return; // usuario decidió no hacer nada

                    if (resp == DialogResult.No)
                    {
                        // Reemplazo total: descartamos lo anterior y usamos la nueva tabla.
                        _tabla = nueva;
                    }
                    else
                    {
                        // Combinación: alinea esquemas y copia filas de 'nueva' a '_tabla'.
                        // También marca cada fila con el nombre del archivo de origen.
                        AppendTabla(_tabla, nueva, Path.GetFileName(path));
                    }
                }

                // Crea/actualiza la vista filtrable sobre la tabla maestra y la enlaza al grid.
                _vista = new DataView(_tabla);
                dgvDatos.DataSource = _vista;

                // Alimenta combos de filtros basados en valores distintos presentes en la tabla.
                PoblarEntidades();     // llena cboEntidad con valores únicos de "Entidad"
                PoblarMunicipios();    // llena cboMunicipio (depende de la entidad seleccionada)

                // Asegura que Estatus tenga opciones visibles y selecciona "(Todos)".
                if (cboEstatus.Items.Count == 0)
                    cboEstatus.Items.AddRange(new object[] { "(Todos)", "Afiliado", "No afiliado" });
                cboEstatus.SelectedIndex = 0;

                // Limpia filtros de texto y apaga filtros de fecha para empezar “desde cero”.
                txtNombre.Clear();
                chkDesde.Checked = chkHasta.Checked = false;

                // Muestra información de cuántas filas y columnas se cargaron.
                lblInfo.Text = $"Cargadas: {_tabla.Rows.Count:n0} filas, {_tabla.Columns.Count} columnas";

                // Rehabilita todos los controles de filtrado ahora que hay datos.
                txtNombre.Enabled = true;
                cboEntidad.Enabled = true;
                cboMunicipio.Enabled = true;
                cboEstatus.Enabled = true;
                chkDesde.Enabled = true;
                chkHasta.Enabled = true;
                dtpDesde.Enabled = chkDesde.Checked; // coherente con el estado del check
                dtpHasta.Enabled = chkHasta.Checked;

                // Corrige posibles -1 en SelectedIndex tras asignar nuevos DataSource en combos.
                if (cboEntidad.Items.Count > 0 && cboEntidad.SelectedIndex < 0) cboEntidad.SelectedIndex = 0;
                if (cboMunicipio.Items.Count > 0 && cboMunicipio.SelectedIndex < 0) cboMunicipio.SelectedIndex = 0;
                if (cboEstatus.Items.Count == 0) cboEstatus.Items.AddRange(new object[] { "(Todos)", "Afiliado", "No afiliado" });
                if (cboEstatus.SelectedIndex < 0) cboEstatus.SelectedIndex = 0;

                // Aplica una pasada de filtros para refrescar el contador “Mostrando: X / Y”.
                AplicarFiltros();
            }
            catch (Exception ex)
            {
                // Cualquier error de lectura/parseo se notifica al usuario de forma segura (UI thread).
                MessageBox.Show("Error al cargar: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnReset_Click(object sender, EventArgs e)
        {
            // “Descarga” el archivo de la aplicación: limpia grid, filtros y deshabilita controles.
            // No borra nada del disco.
            EstadoSinArchivo();

            // Si activas la prevención de duplicados, también conviene limpiar el registro:
            // _archivosCargados.Clear();
        }

        // ===== Handlers de filtros: cualquier cambio re-aplica el RowFilter =====

        private void txtNombre_TextChanged(object sender, EventArgs e)
        {
            AplicarFiltros(); // filtra por texto libre en "Nombre"
        }

        private void cboEntidad_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Si cambia la Entidad, se debe repoblar Municipios con los que apliquen
            // y luego re-aplicar el filtro completo.
            PoblarMunicipios();
            AplicarFiltros();
        }

        private void cboMunicipio_SelectedIndexChanged(object sender, EventArgs e)
        {
            AplicarFiltros(); // filtra por municipio exacto (si no es "(Todos)")
        }

        private void cboEstatus_SelectedIndexChanged(object sender, EventArgs e)
        {
            AplicarFiltros(); // filtra por estatus (Afiliado/No afiliado), si no es "(Todos)"
        }

        private void chkDesde_CheckedChanged(object sender, EventArgs e)
        {
            // Habilita o deshabilita el DateTimePicker de “Desde” según el check.
            dtpDesde.Enabled = chkDesde.Checked;
            AplicarFiltros(); // incorpora/quita la condición de fecha mínima
        }

        private void chkHasta_CheckedChanged(object sender, EventArgs e)
        {
            dtpHasta.Enabled = chkHasta.Checked;
            AplicarFiltros(); // incorpora/quita la condición de fecha máxima
        }

        private void dtpDesde_ValueChanged(object sender, EventArgs e)
        {
            AplicarFiltros(); // cambia la fecha mínima del rango
        }

        private void dtpHasta_ValueChanged(object sender, EventArgs e)
        {
            AplicarFiltros(); // cambia la fecha máxima del rango
        }

        // Construye la expresión RowFilter (tipo SQL simplificado) a partir de los controles de filtro
        // y la aplica sobre la DataView para que el DataGridView muestre solo las filas que cumplen.
        private void AplicarFiltros()
        {
            if (_vista == null) return; // sin datos cargados, nada que filtrar

            var filtros = new List<string>(); // acumulador de condiciones individuales

            // ---- 1) Filtro de texto libre sobre "Nombre" ----
            string texto = txtNombre.Text.Trim();
            if (!string.IsNullOrEmpty(texto))
            {
                // RowFilter usa LIKE con comodines (% y _) y soporta caracteres especiales.
                // Aquí escapamos comillas y corchetes para evitar errores y que cuente como literal.
                string esc = texto.Replace("'", "''")   // escapa comilla simple
                                  .Replace("%", "[%]")  // trata % como texto
                                  .Replace("_", "[_]")  // trata _ como texto
                                  .Replace("[", "[[")   // trata [ como texto
                                  .Replace("]", "]]");  // trata ] como texto

                // Convert(, 'System.String') asegura tratar la columna como texto por si el origen varía.
                filtros.Add($"Convert([Nombre], 'System.String') LIKE '%{esc}%'");
            }

            // ---- 2) Filtro por Entidad exacta (si no es "(Todas)") ----
            if (cboEntidad.SelectedItem is string ent && ent != "(Todas)")
                filtros.Add($"Convert([Entidad], 'System.String') = '{ent.Replace("'", "''")}'");

            // ---- 3) Filtro por Municipio exacto (si no es "(Todos)") ----
            if (cboMunicipio.SelectedItem is string mp && mp != "(Todos)")
                filtros.Add($"Convert([Municipio], 'System.String') = '{mp.Replace("'", "''")}'");

            // ---- 4) Filtro por Estatus exacto (si no es "(Todos)") ----
            if (cboEstatus.SelectedItem is string est && est != "(Todos)")
                filtros.Add($"Convert([Estatus], 'System.String') = '{est.Replace("'", "''")}'");

            // ---- 5) Filtros por rango de fechas (RowFilter requiere formato #MM/dd/yyyy#) ----
            if (chkDesde.Checked && _tabla.Columns.Contains("FechaAfiliacion"))
                filtros.Add($"[FechaAfiliacion] >= #{dtpDesde.Value:MM/dd/yyyy}#");

            if (chkHasta.Checked && _tabla.Columns.Contains("FechaAfiliacion"))
                filtros.Add($"[FechaAfiliacion] <= #{dtpHasta.Value:MM/dd/yyyy}#");

            // Une todas las condiciones con AND y aplica a la DataView.
            _vista.RowFilter = string.Join(" AND ", filtros);

            // Actualiza la etiqueta con cuántas filas pasan el filtro sobre el total cargado.
            lblInfo.Text = $"Mostrando: {_vista.Count:n0} / {_tabla.Rows.Count:n0} filas";
        }

        // Llena el ComboBox de Entidades con valores únicos presentes en la columna "Entidad".
        private void PoblarEntidades()
        {
            var valores = new List<string> { "(Todas)" }; // primera opción: sin filtro

            if (_tabla.Columns.Contains("Entidad"))
            {
                // AsEnumerable permite LINQ sobre DataTable.
                var q = _tabla.AsEnumerable()
                              .Select(r => (r["Entidad"]?.ToString() ?? "").Trim())  // texto limpio
                              .Where(s => !string.IsNullOrEmpty(s))                   // descarta vacíos
                              .Distinct(StringComparer.OrdinalIgnoreCase)             // únicos, sin distinguir may/min
                              .OrderBy(s => s, StringComparer.OrdinalIgnoreCase);     // orden alfabético
                valores.AddRange(q);
            }

            // Asigna la lista al ComboBox (crea los ítems visibles).
            cboEntidad.DataSource = valores;
        }

        // Llena el ComboBox de Municipios. Si hay Entidad seleccionada (≠ "(Todas)"), solo lista los de esa.
        private void PoblarMunicipios()
        {
            var valores = new List<string> { "(Todos)" }; // primera opción: sin filtro

            if (_tabla.Columns.Contains("Municipio"))
            {
                IEnumerable<string> q;

                if (cboEntidad.SelectedItem is string ent && ent != "(Todas)")
                {
                    // Solo municipios pertenecientes a la entidad elegida.
                    q = _tabla.AsEnumerable()
                              .Where(r => string.Equals(
                                  (r["Entidad"]?.ToString() ?? "").Trim(),
                                  ent,
                                  StringComparison.OrdinalIgnoreCase))
                              .Select(r => r["Municipio"]?.ToString() ?? "");
                }
                else
                {
                    // Todos los municipios disponibles en la tabla (sin filtrar por entidad).
                    q = _tabla.AsEnumerable().Select(r => r["Municipio"]?.ToString() ?? "");
                }

                // Limpia, unifica y ordena los nombres de municipio para el combo.
                var dist = q.Select(s => s.Trim())
                            .Where(s => !string.IsNullOrEmpty(s))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase);

                valores.AddRange(dist);
            }

            // Aplica la lista final al ComboBox de Municipios.
            cboMunicipio.DataSource = valores;
        }

        // Deja la app en estado “sin archivo cargado”: grid vacío, filtros apagados, mensaje informativo.
        private void EstadoSinArchivo()
        {
            // Datos: sin vista (null) y tabla vacía (nueva instancia).
            _vista = null;
            _tabla = new DataTable();

            // Grid: desasocia DataSource y borra filas/columnas (por si acaso).
            dgvDatos.DataSource = null;
            dgvDatos.Rows.Clear();
            dgvDatos.Columns.Clear();

            // Filtros: limpia controles de entrada y combos dependientes.
            txtNombre.Clear();

            cboEntidad.DataSource = null;
            cboEntidad.Items.Clear();
            cboEntidad.SelectedIndex = -1; // sin selección

            cboMunicipio.DataSource = null;
            cboMunicipio.Items.Clear();
            cboMunicipio.SelectedIndex = -1; // sin selección

            // Asegura que Estatus tenga sus opciones base y deja "(Todos)" seleccionado.
            if (cboEstatus.Items.Count == 0)
                cboEstatus.Items.AddRange(new object[] { "(Todos)", "Afiliado", "No afiliado" });
            cboEstatus.SelectedIndex = 0;

            // Apaga filtros de fecha.
            chkDesde.Checked = false;
            chkHasta.Checked = false;

            // Deshabilita todos los filtros hasta que se cargue un archivo (mejor UX).
            HabilitarFiltros(false);

            // Mensaje de estado para guiar al usuario.
            lblInfo.Text = "Sin archivo cargado.";
        }

        // Método comodín por si quieres activar filtros de golpe cuando haya datos.
        private void EstadoConArchivo()
        {
            HabilitarFiltros(true);
        }

        // Habilita/deshabilita todos los controles de filtrado de una sola vez (coherencia).
        private void HabilitarFiltros(bool on)
        {
            txtNombre.Enabled = on;
            cboEntidad.Enabled = on;
            cboMunicipio.Enabled = on;
            cboEstatus.Enabled = on;
            chkDesde.Enabled = on;
            dtpDesde.Enabled = on && chkDesde.Checked; // el DateTimePicker respeta el check
            chkHasta.Enabled = on;
            dtpHasta.Enabled = on && chkHasta.Checked;
        }

        // Combina 'origen' dentro de 'destino': alinea columnas, etiqueta el archivo de origen y copia filas.
        private static void AppendTabla(DataTable destino, DataTable origen, string archivoOrigen)
        {
            // 1) Garantiza la existencia de la columna de traza "ArchivoOrigen" en ambas tablas.
            if (!destino.Columns.Contains("ArchivoOrigen"))
                destino.Columns.Add("ArchivoOrigen", typeof(string));
            if (!origen.Columns.Contains("ArchivoOrigen"))
                origen.Columns.Add("ArchivoOrigen", typeof(string));

            // Anota en cada fila del origen de qué archivo proviene (útil para auditoría).
            foreach (DataRow r in origen.Rows)
                r["ArchivoOrigen"] = archivoOrigen;

            // 2) Alineación de esquemas:
            //    Agrega al DESTINO cualquier columna que está en ORIGEN y falte en DESTINO…
            foreach (DataColumn c in origen.Columns)
                if (!destino.Columns.Contains(c.ColumnName))
                    destino.Columns.Add(c.ColumnName, c.DataType);

            //    …y agrega al ORIGEN cualquier columna que está en DESTINO y falte en ORIGEN.
            foreach (DataColumn c in destino.Columns)
                if (!origen.Columns.Contains(c.ColumnName))
                    origen.Columns.Add(c.ColumnName, c.DataType);

            // 3) Copia efectiva: importa todas las filas del origen al destino preservando valores.
            foreach (DataRow r in origen.Rows)
                destino.ImportRow(r);
        }
    }
}
