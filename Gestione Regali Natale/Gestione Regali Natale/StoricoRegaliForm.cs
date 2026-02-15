using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SQLite;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Gestione_Regali_Natale
{
    public partial class StoricoRegaliForm : Form
    {
        string connString;
        int _clienteId;

        public StoricoRegaliForm(int clienteId, string nomeCliente, string connectionString)
        {
            InitializeComponent();
            _clienteId = clienteId;
            connString = connectionString;

            lblCliente.Text = nomeCliente;
            CaricaRegali();
        }

        private void StoricoRegaliForm_Load(object sender, EventArgs e)
        {

        }
        void CaricaRegali()
        {
            dataGridView1.Rows.Clear();

            using (SQLiteConnection conn = new SQLiteConnection(connString))
            {
                conn.Open();

                SQLiteCommand cmd = new SQLiteCommand(@"
            SELECT Id, Regalo, Categoria, Anno
            FROM Regali
            WHERE ClienteId = @id
            ORDER BY Anno DESC
        ", conn);

                cmd.Parameters.AddWithValue("@id", _clienteId);

                using (SQLiteDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        dataGridView1.Rows.Add(
                            r["Id"],
                            r["Regalo"],
                            r["Categoria"],
                            r["Anno"]
                        );
                    }
                }
            }
        }

        private void btnAggiungiRegalo_Click(object sender, EventArgs e)
        {
            using (var f = new RegaloForm())
            {
                if (f.ShowDialog() == DialogResult.OK)
                {
                    using (SQLiteConnection conn = new SQLiteConnection(connString))
                    {
                        conn.Open();

                        SQLiteCommand cmd = new SQLiteCommand(@"
                    INSERT INTO Regali (ClienteId, Anno, Regalo, Categoria)
                    VALUES (@id,@a,@r)", conn);

                        cmd.Parameters.AddWithValue("@id", _clienteId);
                        cmd.Parameters.AddWithValue("@a", f.Anno);
                        cmd.Parameters.AddWithValue("@c", f.Categoria);
                        cmd.Parameters.AddWithValue("@r", f.Regalo);

                        cmd.ExecuteNonQuery();
                    }

                    CaricaRegali();
                }
            }
        }
        private void btnModificaRegalo_Click(object sender, EventArgs e)
        {
            if (dataGridView1.SelectedRows.Count == 0)
                return;

            var row = dataGridView1.SelectedRows[0];

            int regaloId = Convert.ToInt32(row.Cells["colId"].Value);

            string regalo = row.Cells["colRegali"].Value?.ToString() ?? "";

            int anno = 0;
            if (row.Cells["colAnno"].Value != null &&
                row.Cells["colAnno"].Value != DBNull.Value)
            {
                int.TryParse(row.Cells["colAnno"].Value.ToString(), out anno);
            }

            string categoria = row.Cells["colCategoria"].Value?.ToString() ?? "";

            using (var f = new RegaloForm(regalo, anno, categoria))
            {



                if (f.ShowDialog() == DialogResult.OK)
                {
                    using (SQLiteConnection conn = new SQLiteConnection(connString))
                    {
                        conn.Open();

                        SQLiteCommand cmd = new SQLiteCommand(@"
                    UPDATE Regali
                    SET Regalo=@r, Categoria=@c, Anno=@a
                    WHERE Id=@id", conn);

                        cmd.Parameters.AddWithValue("@r", f.Regalo);
                        cmd.Parameters.AddWithValue("@c", f.Categoria);
                        cmd.Parameters.AddWithValue("@a", f.Anno);
                        cmd.Parameters.AddWithValue("@id", regaloId);

                        cmd.ExecuteNonQuery();
                    }

                    CaricaRegali();
                }
            }
        }
    }
}
