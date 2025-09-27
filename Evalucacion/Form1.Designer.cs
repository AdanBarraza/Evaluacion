namespace Evalucacion
{
    partial class AfiliacionPRI
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            dgvDatos = new DataGridView();
            btnAbrir = new Button();
            btnReset = new Button();
            cboEntidad = new ComboBox();
            cboMunicipio = new ComboBox();
            cboEstatus = new ComboBox();
            chkDesde = new CheckBox();
            dtpDesde = new DateTimePicker();
            chkHasta = new CheckBox();
            dtpHasta = new DateTimePicker();
            lblInfo = new Label();
            txtNombre = new TextBox();
            ((System.ComponentModel.ISupportInitialize)dgvDatos).BeginInit();
            SuspendLayout();
            // 
            // dgvDatos
            // 
            dgvDatos.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvDatos.Location = new Point(1, 109);
            dgvDatos.Name = "dgvDatos";
            dgvDatos.RowHeadersWidth = 51;
            dgvDatos.Size = new Size(799, 360);
            dgvDatos.TabIndex = 0;
            // 
            // btnAbrir
            // 
            btnAbrir.Location = new Point(22, 24);
            btnAbrir.Name = "btnAbrir";
            btnAbrir.Size = new Size(145, 29);
            btnAbrir.TabIndex = 1;
            btnAbrir.Text = "Abrir Archivo";
            btnAbrir.UseVisualStyleBackColor = true;
            btnAbrir.Click += btnAbrir_Click;
            // 
            // btnReset
            // 
            btnReset.Location = new Point(182, 24);
            btnReset.Name = "btnReset";
            btnReset.Size = new Size(94, 29);
            btnReset.TabIndex = 2;
            btnReset.Text = "Reset";
            btnReset.UseVisualStyleBackColor = true;
            btnReset.Click += btnReset_Click;
            // 
            // cboEntidad
            // 
            cboEntidad.FormattingEnabled = true;
            cboEntidad.Location = new Point(294, 25);
            cboEntidad.Name = "cboEntidad";
            cboEntidad.Size = new Size(151, 28);
            cboEntidad.TabIndex = 3;
            cboEntidad.SelectedIndexChanged += cboEntidad_SelectedIndexChanged;
            // 
            // cboMunicipio
            // 
            cboMunicipio.FormattingEnabled = true;
            cboMunicipio.Location = new Point(460, 24);
            cboMunicipio.Name = "cboMunicipio";
            cboMunicipio.Size = new Size(151, 28);
            cboMunicipio.TabIndex = 4;
            cboMunicipio.SelectedIndexChanged += cboMunicipio_SelectedIndexChanged;
            // 
            // cboEstatus
            // 
            cboEstatus.FormattingEnabled = true;
            cboEstatus.Location = new Point(626, 25);
            cboEstatus.Name = "cboEstatus";
            cboEstatus.Size = new Size(151, 28);
            cboEstatus.TabIndex = 5;
            cboEstatus.SelectedIndexChanged += cboEstatus_SelectedIndexChanged;
            // 
            // chkDesde
            // 
            chkDesde.AutoSize = true;
            chkDesde.Location = new Point(12, 484);
            chkDesde.Name = "chkDesde";
            chkDesde.Size = new Size(76, 24);
            chkDesde.TabIndex = 6;
            chkDesde.Text = "Desde:";
            chkDesde.UseVisualStyleBackColor = true;
            chkDesde.CheckedChanged += chkDesde_CheckedChanged;
            // 
            // dtpDesde
            // 
            dtpDesde.Format = DateTimePickerFormat.Short;
            dtpDesde.Location = new Point(94, 484);
            dtpDesde.Name = "dtpDesde";
            dtpDesde.Size = new Size(127, 27);
            dtpDesde.TabIndex = 7;
            dtpDesde.ValueChanged += dtpDesde_ValueChanged;
            // 
            // chkHasta
            // 
            chkHasta.AutoSize = true;
            chkHasta.Location = new Point(227, 487);
            chkHasta.Name = "chkHasta";
            chkHasta.Size = new Size(72, 24);
            chkHasta.TabIndex = 8;
            chkHasta.Text = "Hasta:";
            chkHasta.UseVisualStyleBackColor = true;
            chkHasta.CheckedChanged += chkHasta_CheckedChanged;
            // 
            // dtpHasta
            // 
            dtpHasta.Format = DateTimePickerFormat.Short;
            dtpHasta.Location = new Point(294, 484);
            dtpHasta.Name = "dtpHasta";
            dtpHasta.Size = new Size(121, 27);
            dtpHasta.TabIndex = 9;
            dtpHasta.ValueChanged += dtpHasta_ValueChanged;
            // 
            // lblInfo
            // 
            lblInfo.AutoSize = true;
            lblInfo.Location = new Point(13, 523);
            lblInfo.Name = "lblInfo";
            lblInfo.Size = new Size(0, 20);
            lblInfo.TabIndex = 10;
            // 
            // txtNombre
            // 
            txtNombre.Location = new Point(438, 484);
            txtNombre.Name = "txtNombre";
            txtNombre.PlaceholderText = "Nombre/Apellidos";
            txtNombre.Size = new Size(272, 27);
            txtNombre.TabIndex = 11;
            txtNombre.TextChanged += txtNombre_TextChanged;
            // 
            // AfiliacionPRI
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 558);
            Controls.Add(txtNombre);
            Controls.Add(lblInfo);
            Controls.Add(dtpHasta);
            Controls.Add(chkHasta);
            Controls.Add(dtpDesde);
            Controls.Add(chkDesde);
            Controls.Add(cboEstatus);
            Controls.Add(cboMunicipio);
            Controls.Add(cboEntidad);
            Controls.Add(btnReset);
            Controls.Add(btnAbrir);
            Controls.Add(dgvDatos);
            Name = "AfiliacionPRI";
            Text = "Afiliacion PRI";
            Load += AfiliacionPRI_Load;
            ((System.ComponentModel.ISupportInitialize)dgvDatos).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private DataGridView dgvDatos;
        private Button btnAbrir;
        private Button btnReset;
        private ComboBox cboEntidad;
        private ComboBox cboMunicipio;
        private ComboBox cboEstatus;
        private CheckBox chkDesde;
        private DateTimePicker dtpDesde;
        private CheckBox chkHasta;
        private DateTimePicker dtpHasta;
        private Label lblInfo;
        private TextBox txtNombre;
    }
}
