using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Gestione_Regali_Natale
{
    public partial class ClienteForm : Form
    {
        public string RagioneSociale => txtRagione.Text;
        public string Nome => txtNome.Text;

        public string Referente => txtReferente.Text;
        public decimal Fatturato {  get {decimal.TryParse(txtFatturato.Text, out decimal f); return f; } }
        public string Categoria => cmbCategoria.Text;
        public string Indirizzo => txtIndirizzo.Text;
        public int? Anno { get { if (int.TryParse(txtAnno.Text.Trim(), out int a)) return a; return null; } }
        public bool IsModifica { get; set; } = false;


        public ClienteForm()
        {
            InitializeComponent();
            cmbCategoria.SelectedIndex = 0;
        }

        public ClienteForm(
            string ragione,
            string nome,
            string referente,
            string fatturato,
            string categoria,
            string indirizzo,
            int anno
        )
        {
            InitializeComponent();

            txtRagione.Text = ragione;
            txtNome.Text = nome;
            txtReferente.Text = referente;
            txtFatturato.Text = fatturato;
            cmbCategoria.Text = categoria;
            txtIndirizzo.Text = indirizzo;
            txtAnno.Text = anno.ToString();
        }

        private void ClienteForm_Load(object sender, EventArgs e)
        {
            if (IsModifica)
                this.Text = "Modifica cliente";
            else
                this.Text = "Aggiungi cliente";
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(Nome))
            {
                MessageBox.Show("Il nome del cliente è obbligatorio");
                return;
            }

            if (!Anno.HasValue)
            {
                MessageBox.Show("Inserisci un anno valido");
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
