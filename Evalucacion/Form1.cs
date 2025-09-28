using System.Data;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace Evalucacion
{
    public partial class AfiliacionPRI : Form
    {
        private DataTable _tabla = new();  // tabla completa
        private DataView _vista;           // vista filtrada
        private HashSet<string> _archivosCargados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);


        public AfiliacionPRI()
        {
            InitializeComponent();
        }

        private void AfiliacionPRI_Load(object sender, EventArgs e)
        {
            cboEstatus.Items.Clear();
            cboEstatus.Items.AddRange(new object[] { "(Todos)", "Afiliado", "No afiliado" });
            cboEstatus.SelectedIndex = 0;

            dgvDatos.ReadOnly = true;
            dgvDatos.AllowUserToAddRows = false;
            dgvDatos.AllowUserToDeleteRows = false;
            dgvDatos.RowHeadersVisible = false;
            dgvDatos.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            dgvDatos.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dtpDesde.Enabled = chkDesde.Checked; // inicia coherente con el checkbox
            dtpHasta.Enabled = chkHasta.Checked;
            // deja el form “vacío”
            EstadoSinArchivo();
        }

        private async void btnAbrir_Click(object sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Abrir padrón (XLSX/CSV/JSON)",
                Filter = "Excel (*.xlsx)|*.xlsx|CSV (*.csv)|*.csv|JSON (*.json)|*.json|Todos (*.*)|*.*"
            };
            if (ofd.ShowDialog() != DialogResult.OK) return;

            try
            {
                string path = ofd.FileName;
                DataTable nueva = await Task.Run(() => DataLoader.CargarTabla(path));

                // ¿Ya hay datos cargados?
                bool hayDatos = _tabla != null && _tabla.Rows.Count > 0;

                if (!hayDatos)
                {
                    // Primera carga: asigna directamente
                    _tabla = nueva;
                }
                else
                {
                    // Pregunta si combinar o reemplazar
                    var resp = MessageBox.Show(
                        "Ya hay un padrón cargado.\n\n¿Quieres AGREGAR (combinar) este archivo al actual?\n\nSí = Agregar (combinar)\nNo = Reemplazar\nCancelar = Volver",
                        "Cargar otro archivo",
                        MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Question);

                    if (resp == DialogResult.Cancel) return;

                    if (resp == DialogResult.No)
                    {
                        // Reemplazar por completo
                        _tabla = nueva;
                    }
                    else
                    {
                        // Agregar/combinar
                        AppendTabla(_tabla, nueva, Path.GetFileName(path));
                    }
                }


                // Refrescar vista y UI
                _vista = new DataView(_tabla);
                dgvDatos.DataSource = _vista;

                PoblarEntidades();
                PoblarMunicipios();

                if (cboEstatus.Items.Count == 0)
                    cboEstatus.Items.AddRange(new object[] { "(Todos)", "Afiliado", "No afiliado" });
                cboEstatus.SelectedIndex = 0;

                txtNombre.Clear();
                chkDesde.Checked = chkHasta.Checked = false;

                lblInfo.Text = $"Cargadas: {_tabla.Rows.Count:n0} filas, {_tabla.Columns.Count} columnas";

                // re-habilita filtros y pon selecciones válidas
                txtNombre.Enabled = true;
                cboEntidad.Enabled = true;
                cboMunicipio.Enabled = true;
                cboEstatus.Enabled = true;
                chkDesde.Enabled = true;
                chkHasta.Enabled = true;
                dtpDesde.Enabled = chkDesde.Checked;
                dtpHasta.Enabled = chkHasta.Checked;

                // fija índices (los DataSource recién asignados suelen dejar -1)
                if (cboEntidad.Items.Count > 0 && cboEntidad.SelectedIndex < 0) cboEntidad.SelectedIndex = 0;
                if (cboMunicipio.Items.Count > 0 && cboMunicipio.SelectedIndex < 0) cboMunicipio.SelectedIndex = 0;
                if (cboEstatus.Items.Count == 0) cboEstatus.Items.AddRange(new object[] { "(Todos)", "Afiliado", "No afiliado" });
                if (cboEstatus.SelectedIndex < 0) cboEstatus.SelectedIndex = 0;

                // aplica una vez para refrescar conteo inicial
                AplicarFiltros();
            }

            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar: " + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
 


        private void btnReset_Click(object sender, EventArgs e)
        {
            // No borra el archivo del disco, solo limpia la app
            EstadoSinArchivo();
        }

        private void txtNombre_TextChanged(object sender, EventArgs e)
        {
            AplicarFiltros();
        }

        private void cboEntidad_SelectedIndexChanged(object sender, EventArgs e)
        {
            PoblarMunicipios();
            AplicarFiltros();
        }

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
            dtpDesde.Enabled = chkDesde.Checked;  // <-- habilita/deshabilita
            AplicarFiltros();
        }

        private void chkHasta_CheckedChanged(object sender, EventArgs e)
        {
            dtpHasta.Enabled = chkHasta.Checked;  // <-- habilita/deshabilita
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

        private void AplicarFiltros()
        {
            if (_vista == null) return;
            var filtros = new List<string>();

            string texto = txtNombre.Text.Trim();
            if (!string.IsNullOrEmpty(texto))
            {
                string esc = texto.Replace("'", "''")
                    .Replace("%", "[%]").Replace("_", "[_]")
                      .Replace("[", "[[").Replace("]", "]]");

                filtros.Add($"Convert([Nombre], 'System.String') LIKE '%{esc}%'");

            }

            if (cboEntidad.SelectedItem is string ent && ent != "(Todas)")
                filtros.Add($"Convert([Entidad], 'System.String') = '{ent.Replace("'", "''")}'");

            if (cboMunicipio.SelectedItem is string mp && mp != "(Todos)")
                filtros.Add($"Convert([Municipio], 'System.String') = '{mp.Replace("'", "''")}'");

            if (cboEstatus.SelectedItem is string est && est != "(Todos)")
                filtros.Add($"Convert([Estatus], 'System.String') = '{est.Replace("'", "''")}'");

            if (chkDesde.Checked && _tabla.Columns.Contains("FechaAfiliacion"))
                filtros.Add($"[FechaAfiliacion] >= #{dtpDesde.Value:MM/dd/yyyy}#");

            if (chkHasta.Checked && _tabla.Columns.Contains("FechaAfiliacion"))
                filtros.Add($"[FechaAfiliacion] <= #{dtpHasta.Value:MM/dd/yyyy}#");

            _vista.RowFilter = string.Join(" AND ", filtros);
            lblInfo.Text = $"Mostrando: {_vista.Count:n0} / {_tabla.Rows.Count:n0} filas";
        }

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
            cboEntidad.DataSource = valores;
        }

        private void PoblarMunicipios()
        {
            var valores = new List<string> { "(Todos)" };
            if (_tabla.Columns.Contains("Municipio"))
            {
                IEnumerable<string> q;
                if (cboEntidad.SelectedItem is string ent && ent != "(Todas)")
                {
                    q = _tabla.AsEnumerable()
                              .Where(r => string.Equals((r["Entidad"]?.ToString() ?? "").Trim(), ent, StringComparison.OrdinalIgnoreCase))
                              .Select(r => r["Municipio"]?.ToString() ?? "");
                }
                else
                {
                    q = _tabla.AsEnumerable().Select(r => r["Municipio"]?.ToString() ?? "");
                }

                var dist = q.Select(s => s.Trim())
                            .Where(s => !string.IsNullOrEmpty(s))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase);
                valores.AddRange(dist);
            }
            cboMunicipio.DataSource = valores;
        }

        private void EstadoSinArchivo()
        {
            // Datos
            _vista = null;
            _tabla = new DataTable();

            // Grid
            dgvDatos.DataSource = null;
            dgvDatos.Rows.Clear();
            dgvDatos.Columns.Clear();

            // Filtros
            txtNombre.Clear();

            cboEntidad.DataSource = null;
            cboEntidad.Items.Clear();
            cboEntidad.SelectedIndex = -1;

            cboMunicipio.DataSource = null;
            cboMunicipio.Items.Clear();
            cboMunicipio.SelectedIndex = -1;

            // deja el combo de estatus con sus tres opciones pero sin selección
            // (o si prefieres, vuelve a (Todos))
            if (cboEstatus.Items.Count == 0)
                cboEstatus.Items.AddRange(new object[] { "(Todos)", "Afiliado", "No afiliado" });
            cboEstatus.SelectedIndex = 0; // o -1 si no quieres selección

            chkDesde.Checked = false;
            chkHasta.Checked = false;

            // Deshabilita filtros hasta que cargues un archivo (opcional)
            HabilitarFiltros(false);

            lblInfo.Text = "Sin archivo cargado.";
        }

        private void EstadoConArchivo()
        {
            // Habilita filtros cuando ya hay datos
            HabilitarFiltros(true);
        }

        private void HabilitarFiltros(bool on)
        {
            txtNombre.Enabled = on;
            cboEntidad.Enabled = on;
            cboMunicipio.Enabled = on;
            cboEstatus.Enabled = on;
            chkDesde.Enabled = on;
            dtpDesde.Enabled = on && chkDesde.Checked;
            chkHasta.Enabled = on;
            dtpHasta.Enabled = on && chkHasta.Checked;
        }

        private static void AppendTabla(DataTable destino, DataTable origen, string archivoOrigen)
        {
            // 1) Asegura columna de traza del archivo
            if (!destino.Columns.Contains("ArchivoOrigen"))
                destino.Columns.Add("ArchivoOrigen", typeof(string));
            if (!origen.Columns.Contains("ArchivoOrigen"))
                origen.Columns.Add("ArchivoOrigen", typeof(string));

            foreach (DataRow r in origen.Rows)
                r["ArchivoOrigen"] = archivoOrigen;

            // 2) Alinear esquemas (columnas que falten en uno u otro)
            foreach (DataColumn c in origen.Columns)
                if (!destino.Columns.Contains(c.ColumnName))
                    destino.Columns.Add(c.ColumnName, c.DataType);

            foreach (DataColumn c in destino.Columns)
                if (!origen.Columns.Contains(c.ColumnName))
                    origen.Columns.Add(c.ColumnName, c.DataType);

            // 3) Agregar filas del "origen" al "destino"
            foreach (DataRow r in origen.Rows)
                destino.ImportRow(r);
        }
    }

}
