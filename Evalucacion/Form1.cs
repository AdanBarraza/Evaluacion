using System.Data;                      // DataTable, DataView
using System.IO;                        // Path, File
using System.Linq;                      // LINQ para consultas a DataTable
using System.Collections.Generic;       // List<T>, HashSet<T>

namespace Evalucacion
{
    public partial class AfiliacionPRI : Form
    {
        // Tabla con TODOS los datos cargados (de uno o varios archivos)
        private DataTable _tabla = new();

        // Vista filtrada sobre _tabla (es la que se enlaza al DataGridView)
        private DataView _vista;

        // Conjunto de rutas de archivos ya cargados (evita reabrir el mismo)
        private HashSet<string> _archivosCargados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public AfiliacionPRI()
        {
            InitializeComponent(); // Inicializa los controles del diseñador
        }

        private void AfiliacionPRI_Load(object sender, EventArgs e)
        {
            // Llena combo de estatus al iniciar
            cboEstatus.Items.Clear();
            cboEstatus.Items.AddRange(new object[] { "(Todos)", "Afiliado", "No afiliado" });
            cboEstatus.SelectedIndex = 0;

            // Configuración del DataGridView para mejor rendimiento/visual
            dgvDatos.ReadOnly = true;
            dgvDatos.AllowUserToAddRows = false;
            dgvDatos.AllowUserToDeleteRows = false;
            dgvDatos.RowHeadersVisible = false;
            dgvDatos.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            dgvDatos.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

            // Asegura consistencia inicial de habilitación de DateTimePicker con los CheckBox
            dtpDesde.Enabled = chkDesde.Checked;
            dtpHasta.Enabled = chkHasta.Checked;

            // Deja el formulario en estado "sin archivo"
            EstadoSinArchivo();
        }

        private async void btnAbrir_Click(object sender, EventArgs e)
        {
            // Diálogo para elegir archivo
            using var ofd = new OpenFileDialog
            {
                Title = "Abrir padrón (XLSX/CSV/JSON)",
                Filter = "Excel (*.xlsx)|*.xlsx|CSV (*.csv)|*.csv|JSON (*.json)|*.json|Todos (*.*)|*.*"
            };
            if (ofd.ShowDialog() != DialogResult.OK) return;

            try
            {
                // Ruta seleccionada
                string path = ofd.FileName;

                // Carga del archivo en DataTable (hilo de fondo para no congelar UI)
                DataTable nueva = await Task.Run(() => DataLoader.CargarTabla(path));

                // ¿Ya había datos cargados en _tabla?
                bool hayDatos = _tabla != null && _tabla.Rows.Count > 0;

                if (!hayDatos)
                {
                    // Primera vez: asigna la nueva tabla directamente
                    _tabla = nueva;
                }
                else
                {
                    // Ya hay datos: pregunta si combinar o reemplazar
                    var resp = MessageBox.Show(
                        "Ya hay un padrón cargado.\n\n¿Quieres AGREGAR (combinar) este archivo al actual?\n\nSí = Agregar (combinar)\nNo = Reemplazar\nCancelar = Volver",
                        "Cargar otro archivo",
                        MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Question);

                    if (resp == DialogResult.Cancel) return;

                    if (resp == DialogResult.No)
                    {
                        // Reemplaza completamente
                        _tabla = nueva;
                    }
                    else
                    {
                        // Agrega filas del nuevo DataTable al existente (alineando columnas)
                        AppendTabla(_tabla, nueva, Path.GetFileName(path));
                    }
                }

                // (Opcional) si deseas evitar abrir el mismo archivo 2 veces:
                // _archivosCargados.Add(path);

                // Crea/actualiza la vista y la enlaza al DataGridView
                _vista = new DataView(_tabla);
                dgvDatos.DataSource = _vista;

                // Repuebla combos de filtros dependientes del contenido
                PoblarEntidades();
                PoblarMunicipios();

                // Asegura opciones e índice del estatus
                if (cboEstatus.Items.Count == 0)
                    cboEstatus.Items.AddRange(new object[] { "(Todos)", "Afiliado", "No afiliado" });
                cboEstatus.SelectedIndex = 0;

                // Limpia filtros de texto y fecha
                txtNombre.Clear();
                chkDesde.Checked = chkHasta.Checked = false;

                // Info de conteo total
                lblInfo.Text = $"Cargadas: {_tabla.Rows.Count:n0} filas, {_tabla.Columns.Count} columnas";

                // Rehabilita filtros y asegura índices válidos en combos
                txtNombre.Enabled = true;
                cboEntidad.Enabled = true;
                cboMunicipio.Enabled = true;
                cboEstatus.Enabled = true;
                chkDesde.Enabled = true;
                chkHasta.Enabled = true;
                dtpDesde.Enabled = chkDesde.Checked;
                dtpHasta.Enabled = chkHasta.Checked;

                if (cboEntidad.Items.Count > 0 && cboEntidad.SelectedIndex < 0) cboEntidad.SelectedIndex = 0;
                if (cboMunicipio.Items.Count > 0 && cboMunicipio.SelectedIndex < 0) cboMunicipio.SelectedIndex = 0;
                if (cboEstatus.Items.Count == 0) cboEstatus.Items.AddRange(new object[] { "(Todos)", "Afiliado", "No afiliado" });
                if (cboEstatus.SelectedIndex < 0) cboEstatus.SelectedIndex = 0;

                // Aplica filtros una vez para refrescar el contador visible
                AplicarFiltros();
            }
            catch (Exception ex)
            {
                // Cualquier error en la carga/parseo del archivo
                MessageBox.Show("Error al cargar: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnReset_Click(object sender, EventArgs e)
        {
            // Limpia completamente la app (no borra el archivo del disco)
            EstadoSinArchivo();

            // (Opcional) si usaste la prevención de archivo duplicado:
            // _archivosCargados.Clear();
        }

        // Evento del TextBox de búsqueda por nombre
        private void txtNombre_TextChanged(object sender, EventArgs e)
        {
            AplicarFiltros();
        }

        // Al cambiar Entidad: repuebla municipios y aplica filtro
        private void cboEntidad_SelectedIndexChanged(object sender, EventArgs e)
        {
            PoblarMunicipios();
            AplicarFiltros();
        }

        // Cambios en Municipio, Estatus o Fechas => reaplicar filtro
        private void cboMunicipio_SelectedIndexChanged(object sender, EventArgs e)
        {
            AplicarFiltros();
        }

        private void cboEstatus_SelectedIndexChanged(object sender, EventArgs e)
        {
            AplicarFiltros();
        }

        private void chkDesde_CheckedChanged(object sender, EventArgs e)
        {
            // Habilita/deshabilita el DateTimePicker según el checkbox
            dtpDesde.Enabled = chkDesde.Checked;
            AplicarFiltros();
        }

        private void chkHasta_CheckedChanged(object sender, EventArgs e)
        {
            dtpHasta.Enabled = chkHasta.Checked;
            AplicarFiltros();
        }

        private void dtpDesde_ValueChanged(object sender, EventArgs e)
        {
            AplicarFiltros();
        }

        private void dtpHasta_ValueChanged(object sender, EventArgs e)
        {
            AplicarFiltros();
        }

        // Construye la expresión RowFilter con todos los filtros activos y la aplica a _vista
        private void AplicarFiltros()
        {
            if (_vista == null) return;

            var filtros = new List<string>();

            // Texto libre sobre la columna "Nombre" (ya unificada: nombre + apellidos)
            string texto = txtNombre.Text.Trim();
            if (!string.IsNullOrEmpty(texto))
            {
                // Escapar caracteres especiales para LIKE de RowFilter
                string esc = texto.Replace("'", "''")
                                  .Replace("%", "[%]")
                                  .Replace("_", "[_]")
                                  .Replace("[", "[[")
                                  .Replace("]", "]]");

                filtros.Add($"Convert([Nombre], 'System.String') LIKE '%{esc}%'");
            }

            // Filtro por Entidad exacta
            if (cboEntidad.SelectedItem is string ent && ent != "(Todas)")
                filtros.Add($"Convert([Entidad], 'System.String') = '{ent.Replace("'", "''")}'");

            // Filtro por Municipio exacto
            if (cboMunicipio.SelectedItem is string mp && mp != "(Todos)")
                filtros.Add($"Convert([Municipio], 'System.String') = '{mp.Replace("'", "''")}'");

            // Filtro por Estatus exacto
            if (cboEstatus.SelectedItem is string est && est != "(Todos)")
                filtros.Add($"Convert([Estatus], 'System.String') = '{est.Replace("'", "''")}'");

            // Filtros por rango de fechas (RowFilter usa #MM/dd/yyyy#)
            if (chkDesde.Checked && _tabla.Columns.Contains("FechaAfiliacion"))
                filtros.Add($"[FechaAfiliacion] >= #{dtpDesde.Value:MM/dd/yyyy}#");

            if (chkHasta.Checked && _tabla.Columns.Contains("FechaAfiliacion"))
                filtros.Add($"[FechaAfiliacion] <= #{dtpHasta.Value:MM/dd/yyyy}#");

            // Aplica el filtro combinado (AND)
            _vista.RowFilter = string.Join(" AND ", filtros);

            // Feedback en UI: cuántas filas visibles vs total
            lblInfo.Text = $"Mostrando: {_vista.Count:n0} / {_tabla.Rows.Count:n0} filas";
        }

        // Llena el ComboBox de Entidades a partir de los valores únicos en la tabla
        private void PoblarEntidades()
        {
            var valores = new List<string> { "(Todas)" };

            if (_tabla.Columns.Contains("Entidad"))
            {
                var q = _tabla.AsEnumerable()
                              .Select(r => (r["Entidad"]?.ToString() ?? "").Trim())
                              .Where(s => !string.IsNullOrEmpty(s))
                              .Distinct(StringComparer.OrdinalIgnoreCase)
                              .OrderBy(s => s, StringComparer.OrdinalIgnoreCase);
                valores.AddRange(q);
            }

            cboEntidad.DataSource = valores; // asigna lista al combo
        }

        // Llena el ComboBox de Municipios; si hay Entidad seleccionada, filtra por ella
        private void PoblarMunicipios()
        {
            var valores = new List<string> { "(Todos)" };

            if (_tabla.Columns.Contains("Municipio"))
            {
                IEnumerable<string> q;

                if (cboEntidad.SelectedItem is string ent && ent != "(Todas)")
                {
                    // Municipios solo de la Entidad elegida
                    q = _tabla.AsEnumerable()
                              .Where(r => string.Equals(
                                  (r["Entidad"]?.ToString() ?? "").Trim(),
                                  ent,
                                  StringComparison.OrdinalIgnoreCase))
                              .Select(r => r["Municipio"]?.ToString() ?? "");
                }
                else
                {
                    // Todos los municipios (de todas las entidades)
                    q = _tabla.AsEnumerable().Select(r => r["Municipio"]?.ToString() ?? "");
                }

                // Distintos, no vacíos y ordenados alfabéticamente
                var dist = q.Select(s => s.Trim())
                            .Where(s => !string.IsNullOrEmpty(s))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase);

                valores.AddRange(dist);
            }

            cboMunicipio.DataSource = valores; // asigna lista al combo
        }

        // Deja la app en estado "sin archivo cargado"
        private void EstadoSinArchivo()
        {
            // Datos: limpia referencias
            _vista = null;
            _tabla = new DataTable();

            // Grid: sin origen ni filas/columnas
            dgvDatos.DataSource = null;
            dgvDatos.Rows.Clear();
            dgvDatos.Columns.Clear();

            // Filtros: limpia y sin selección
            txtNombre.Clear();

            cboEntidad.DataSource = null;
            cboEntidad.Items.Clear();
            cboEntidad.SelectedIndex = -1;

            cboMunicipio.DataSource = null;
            cboMunicipio.Items.Clear();
            cboMunicipio.SelectedIndex = -1;

            // Asegura opciones de estatus (si no estuvieran)
            if (cboEstatus.Items.Count == 0)
                cboEstatus.Items.AddRange(new object[] { "(Todos)", "Afiliado", "No afiliado" });
            cboEstatus.SelectedIndex = 0; // puedes usar -1 si no quieres selección por defecto

            chkDesde.Checked = false;
            chkHasta.Checked = false;

            // Deshabilita filtros hasta que cargues un archivo (protege UX)
            HabilitarFiltros(false);

            // Mensaje de estado
            lblInfo.Text = "Sin archivo cargado.";
        }

        // (Por si quieres un lugar claro para habilitar tras cargar)
        private void EstadoConArchivo()
        {
            HabilitarFiltros(true);
        }

        // Habilita/Deshabilita todos los filtros de una sola vez (UX)
        private void HabilitarFiltros(bool on)
        {
            txtNombre.Enabled = on;
            cboEntidad.Enabled = on;
            cboMunicipio.Enabled = on;
            cboEstatus.Enabled = on;
            chkDesde.Enabled = on;
            dtpDesde.Enabled = on && chkDesde.Checked; // coherente con su CheckBox
            chkHasta.Enabled = on;
            dtpHasta.Enabled = on && chkHasta.Checked;
        }

        // Agrega (importa) filas de 'origen' a 'destino', alineando columnas; marca archivo de origen
        private static void AppendTabla(DataTable destino, DataTable origen, string archivoOrigen)
        {
            // Asegura columna para saber de qué archivo vino cada fila
            if (!destino.Columns.Contains("ArchivoOrigen"))
                destino.Columns.Add("ArchivoOrigen", typeof(string));
            if (!origen.Columns.Contains("ArchivoOrigen"))
                origen.Columns.Add("ArchivoOrigen", typeof(string));

            foreach (DataRow r in origen.Rows)
                r["ArchivoOrigen"] = archivoOrigen;

            // Alinea columnas: agrega al destino las que faltan del origen…
            foreach (DataColumn c in origen.Columns)
                if (!destino.Columns.Contains(c.ColumnName))
                    destino.Columns.Add(c.ColumnName, c.DataType);

            // …y al origen las que falten del destino
            foreach (DataColumn c in destino.Columns)
                if (!origen.Columns.Contains(c.ColumnName))
                    origen.Columns.Add(c.ColumnName, c.DataType);

            // Importa todas las filas del origen al destino
            foreach (DataRow r in origen.Rows)
                destino.ImportRow(r);
        }
    }
}

