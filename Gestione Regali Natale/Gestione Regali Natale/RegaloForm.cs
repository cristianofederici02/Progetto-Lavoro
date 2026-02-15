using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Gestione_Regali_Natale
{
    public partial class RegaloForm : Form
    {
        public string Regalo => txtRegalo.Text;
        public string Categoria => cmbCategoria.Text;

        public int Anno => int.TryParse(txtAnno.Text, out int a) ? a : 0;
        public RegaloForm()
        {
            InitializeComponent();
            cmbCategoria.SelectedIndex = 0;
        }
        public RegaloForm(string regalo,int anno, string categoria)
        {
            InitializeComponent();

            txtRegalo.Text = regalo;
            txtAnno.Text = anno.ToString();

            if (cmbCategoria.Items.Contains(categoria))
                cmbCategoria.SelectedItem = categoria;
            else
                cmbCategoria.SelectedIndex = 0;
        }
        private void RegaloForm_Load(object sender, EventArgs e)
        {

        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(Regalo))
            {
                MessageBox.Show("Inserisci il regalo");
                return;
            }

            if (Anno <= 0)
            {
                MessageBox.Show("Anno non valido");
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        private void btnAnnulla_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
