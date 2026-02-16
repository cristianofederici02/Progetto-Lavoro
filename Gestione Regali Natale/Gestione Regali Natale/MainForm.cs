using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Data.SQLite;

namespace Gestione_Regali_Natale
{
    public partial class MainForm : Form
    {
        string dbPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
           "GestioneRegali",
           "regali.db"
        );
        string connString;

        public MainForm()
        {
            InitializeComponent();
            connString = "Data Source=" + dbPath;
            InitializeDatabase();
            CaricaDati();
        }

        private void btnImportExcel_Click(object sender, EventArgs e)
        {
            ImportaDaExcel();
        }

        void InitializeDatabase()
        {
            string folder = Path.GetDirectoryName(dbPath);
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            if (!File.Exists(dbPath))
                SQLiteConnection.CreateFile(dbPath);

            using (SQLiteConnection conn = new SQLiteConnection(connString))
            {
                conn.Open();

                SQLiteCommand cmd = conn.CreateCommand();
                cmd.CommandText = @"
        CREATE TABLE IF NOT EXISTS Clienti (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            RagioneSociale TEXT,
            Nome TEXT NOT NULL,
            Referente TEXT,
            Fatturato REAL,
            Categoria TEXT,
            Indirizzo TEXT

        );

            CREATE TABLE IF NOT EXISTS Regali (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            ClienteId INTEGER,
            Anno INTEGER,
            Regalo TEXT,
            Categoria TEXT,
            FOREIGN KEY(ClienteId) REFERENCES Clienti(Id)
        );";
                try
                {
                    new SQLiteCommand(
                        "ALTER TABLE Regali ADD COLUMN Categoria TEXT DEFAULT",
                        conn
                    ).ExecuteNonQuery();
                }
                catch
                {
                    // già esistente
                }

                cmd.ExecuteNonQuery();
            }
        }


        void ImportaDaExcel()
        {
            OpenFileDialog open = new OpenFileDialog
            {
                Filter = "File Excel|*.xlsx"
            };

            if (open.ShowDialog() != DialogResult.OK)
                return;

            using (var wb = new XLWorkbook(open.FileName))
            {
                var ws = wb.Worksheet(1);

                // Mappa intestazioni → colonna
                var headers = ws.Row(1).Cells()
                    .ToDictionary(
                        c => c.GetString().Trim(),
                        c => c.Address.ColumnNumber
                    );

                int lastRow = ws.LastRowUsed().RowNumber();

                using (SQLiteConnection conn = new SQLiteConnection(connString))
                {
                    conn.Open();

                    for (int r = 2; r <= lastRow; r++)
                    {
                        string ragione = ws.Cell(r, headers["Ragione Sociale"]).GetString().Trim();
                        string nome = ws.Cell(r, headers["Nome"]).GetString().Trim();
                        string referente = ws.Cell(r, headers["Referente"]).GetString().Trim();
                        string fatturato = ws.Cell(r, headers["Fatturato"]).GetString().Trim();
                        string categoria = ws.Cell(r, headers["Categoria"]).GetString().Trim();
                        string indirizzo = ws.Cell(r, headers["Indirizzo"]).GetString().Trim();
                        string annoText = ws.Cell(r, headers["Anno"]).GetString().Trim();

                        if (string.IsNullOrWhiteSpace(nome))
                            continue;

                        if (!int.TryParse(annoText, out int anno))
                            continue;

                        // controllo duplicato (Nome + Anno)
                        SQLiteCommand check = new SQLiteCommand(@"
                    SELECT COUNT(*) 
                    FROM Clienti c
                    JOIN Regali r ON r.ClienteId = c.Id
                    WHERE c.Nome=@n AND r.Anno=@a
                ", conn);

                        check.Parameters.AddWithValue("@n", nome);
                        check.Parameters.AddWithValue("@a", anno);

                        long exists = (long)check.ExecuteScalar();
                        if (exists > 0)
                            continue;

                        // inserisci cliente
                        SQLiteCommand insertCliente = new SQLiteCommand(@"
                    INSERT INTO Clienti
                    (RagioneSociale, Nome, Referente, Fatturato, Categoria, Indirizzo)
                    VALUES (@r,@n,@ref,@f,@c,@i);
                    SELECT last_insert_rowid();
                ", conn);

                        insertCliente.Parameters.AddWithValue("@r", ragione);
                        insertCliente.Parameters.AddWithValue("@n", nome);
                        insertCliente.Parameters.AddWithValue("@ref", referente);
                        insertCliente.Parameters.AddWithValue("@f", fatturato);
                        insertCliente.Parameters.AddWithValue("@c", categoria);
                        insertCliente.Parameters.AddWithValue("@i", indirizzo);

                        int clienteId = Convert.ToInt32(insertCliente.ExecuteScalar());

                        // anno
                        SQLiteCommand insertAnno = new SQLiteCommand(@"
                    INSERT INTO Regali (ClienteId, Anno)
                    VALUES (@id,@a)
                ", conn);

                        insertAnno.Parameters.AddWithValue("@id", clienteId);
                        insertAnno.Parameters.AddWithValue("@a", anno);
                        insertAnno.ExecuteNonQuery();
                    }
                }
            }

            CaricaDati();
            MessageBox.Show("Importazione Excel completata!", "Import");
        }

        void EsportaExcel()
        {
            SaveFileDialog save = new SaveFileDialog
            {
                Filter = "File Excel (*.xlsx)|*.xlsx",
                FileName = "Clienti.xlsx"
            };

            if (save.ShowDialog() != DialogResult.OK)
                return;

            using (var wb = new XLWorkbook())
            {
                var wsClienti = wb.Worksheets.Add("Clienti");
                var wsStorico = wb.Worksheets.Add("Storico Regali");

                // intestazioni
                wsClienti.Cell(1, 1).Value = "ID";
                wsClienti.Cell(1, 2).Value = "Ragione Sociale";
                wsClienti.Cell(1, 3).Value = "Nome";
                wsClienti.Cell(1, 4).Value = "Referente";
                wsClienti.Cell(1, 5).Value = "Fatturato";
                wsClienti.Cell(1, 6).Value = "Categoria";
                wsClienti.Cell(1, 7).Value = "Indirizzo";
                wsClienti.Cell(1, 8).Value = "Anno";

                //intestazioni Storico

                wsStorico.Cell(1, 1).Value = "Cliente ID";
                wsStorico.Cell(1, 2).Value = "Nome";
                wsStorico.Cell(1, 3).Value = "Regalo";
                wsStorico.Cell(1, 4).Value = "Categoria Regalo";
                wsStorico.Cell(1, 5).Value = "Anno";

                int rowClienti = 2;
                int rowStorico = 2;

                using (SQLiteConnection conn = new SQLiteConnection(connString))
                {
                    conn.Open();

                    string queryClienti = @"
                SELECT 
                    c.Id,
                    c.RagioneSociale,
                    c.Nome,
                    c.Referente,
                    c.Fatturato,
                    c.Categoria,
                    c.Indirizzo,
                    MAX(r.Anno) AS Anno
                FROM Clienti c
                LEFT JOIN Regali r ON r.ClienteId = c.Id
                GROUP BY c.Id, c.RagioneSociale, c.Nome, c.Referente, c.Fatturato, c.Categoria, c.Indirizzo
                ORDER BY c.Id";

                    using (SQLiteCommand cmdClienti = new SQLiteCommand(queryClienti, conn))
                    using (SQLiteDataReader reader = cmdClienti.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            wsClienti.Cell(rowClienti, 1).Value = Convert.ToInt32(reader["Id"]);
                            wsClienti.Cell(rowClienti, 2).Value = reader["RagioneSociale"].ToString();
                            wsClienti.Cell(rowClienti, 3).Value = reader["Nome"].ToString();
                            wsClienti.Cell(rowClienti, 4).Value = reader["Referente"].ToString();
                            wsClienti.Cell(rowClienti, 5).Value = reader["Fatturato"].ToString();
                            wsClienti.Cell(rowClienti, 6).Value = reader["Categoria"].ToString();
                            wsClienti.Cell(rowClienti, 7).Value = reader["Indirizzo"].ToString();

                            if (reader["Anno"] == DBNull.Value)
                                wsClienti.Cell(rowClienti, 8).Value = "";
                            else
                                wsClienti.Cell(rowClienti, 8).Value = Convert.ToInt32(reader["Anno"]);

                            rowClienti++;
                        }
                    }
                    string queryStorico = @"
                SELECT
                    r.ClienteId,
                    c.Nome,
                    r.Regalo,
                    r.CategoriaRegalo,
                    r.Anno
                FROM Regali r
                INNER JOIN Clienti c ON c.Id = r.ClienteId
                ORDER BY c.Nome, r.Anno DESC";

                    using (SQLiteCommand cmdStorico = new SQLiteCommand(queryStorico, conn))
                    using (SQLiteDataReader readerStorico = cmdStorico.ExecuteReader())
                    {
                        while (readerStorico.Read())
                        {
                            wsStorico.Cell(rowStorico, 1).Value = Convert.ToInt32(readerStorico["ClienteId"]);
                            wsStorico.Cell(rowStorico, 2).Value = readerStorico["Nome"].ToString();
                            wsStorico.Cell(rowStorico, 3).Value = readerStorico["Regalo"] == DBNull.Value ? "" : readerStorico["Regalo"].ToString();
                            wsStorico.Cell(rowStorico, 4).Value = readerStorico["CategoriaRegalo"] == DBNull.Value ? "" : readerStorico["CategoriaRegalo"].ToString();
                            wsStorico.Cell(rowStorico, 5).Value = readerStorico["Anno"] == DBNull.Value ? "" : readerStorico["Anno"].ToString();

                            rowStorico++;
                        }
                    }
                }

                wsClienti.Columns().AdjustToContents();
                wsStorico.Columns().AdjustToContents();
                wb.SaveAs(save.FileName);
            }

            MessageBox.Show("Esportazione completata!", "Excel");
        }

        void CaricaDati(string filtro = "")
        {
            if (dataGridView2 == null)
                return;

            dataGridView2.Rows.Clear();

            using (SQLiteConnection conn = new SQLiteConnection(connString))
            {
                conn.Open();

                string query = @"
            SELECT
            c.Id,
            c.Nome,
            c.Referente,
            c.Categoria,
            c.Fatturato,
            c.RagioneSociale,
            c.Indirizzo,
            MAX(r.Anno) AS Anno
            FROM Clienti c
            LEFT JOIN Regali r ON r.ClienteId = c.Id
            WHERE
            c.Nome LIKE @filtro OR
            c.RagioneSociale LIKE @filtro OR
            c.Referente LIKE @filtro OR
            c.Fatturato LIKE @filtro OR
            c.Categoria LIKE @filtro OR
            c.Indirizzo LIKE @filtro OR
            r.Anno LIKE @filtro
            GROUP BY
            c.Id, c.Nome, c.Referente, c.Categoria, c.Fatturato, c.RagioneSociale, c.Indirizzo
            ORDER BY c.Id";

                using (SQLiteCommand cmd = new SQLiteCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@filtro", "%" + filtro + "%");

                    using (SQLiteDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int rowIndex = dataGridView2.Rows.Add();
                            var row = dataGridView2.Rows[rowIndex];

                            row.Cells["colId"].Value = reader["Id"];
                            row.Cells["colNome"].Value = reader["Nome"];
                            row.Cells["colReferente"].Value = reader["Referente"];
                            row.Cells["colCategoria"].Value = reader["Categoria"];
                            row.Cells["colFatturato"].Value = reader["Fatturato"];
                            row.Cells["colRagione"].Value = reader["RagioneSociale"];
                            row.Cells["colIndirizzo"].Value = reader["Indirizzo"];
                            row.Cells["colAnno"].Value = reader["Anno"];
                        }
                    }
                }
            }
        }



        private void btnAggiungiCliente_Click(object sender, EventArgs e)
        {
            using (var f = new ClienteForm())
            {
                f.IsModifica = false;

                if (f.ShowDialog() == DialogResult.OK)
                {
                    int clienteId = InserisciCliente(
                        f.RagioneSociale,
                        f.Nome,
                        f.Referente,
                        f.Fatturato,
                        f.Categoria,
                        f.Indirizzo
                    );

                    InserisciAnno(clienteId, f.Anno.Value);

                    CaricaDati();
                }
            }
        }


        void InserisciAnno(int clienteId, int anno)
        {
            using (SQLiteConnection conn = new SQLiteConnection(connString))
            {
                conn.Open();

                SQLiteCommand cmd = new SQLiteCommand(@"
            INSERT INTO Regali (ClienteId, Anno)
            VALUES (@id, @anno)
        ", conn);

                cmd.Parameters.AddWithValue("@id", clienteId);
                cmd.Parameters.AddWithValue("@anno", anno);

                cmd.ExecuteNonQuery();
            }
        }


        int InserisciCliente(string ragione, string nome, string referente, decimal fatturato, string categoria, string indirizzo)
        {
            using (SQLiteConnection conn = new SQLiteConnection(connString))
            {
                conn.Open();

                SQLiteCommand cmd = new SQLiteCommand(@"
                INSERT INTO Clienti
                (RagioneSociale, Nome, Referente, Fatturato, Categoria, Indirizzo)
                VALUES (@r,@n,@ref,@f,@c,@i);
                SELECT last_insert_rowid();
                ", conn);

                cmd.Parameters.AddWithValue("@r", ragione);
                cmd.Parameters.AddWithValue("@n", nome);
                cmd.Parameters.AddWithValue("@ref", referente);
                cmd.Parameters.AddWithValue("@f", fatturato);
                cmd.Parameters.AddWithValue("@c", categoria);
                cmd.Parameters.AddWithValue("@i", indirizzo);

                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

            private void btnModificaCliente_Click(object sender, EventArgs e)
        {
            if (dataGridView2.SelectedRows.Count == 0)
            {
                MessageBox.Show("Seleziona un cliente");
                return;
            }

            DataGridViewRow row = dataGridView2.SelectedRows[0];

            int id = Convert.ToInt32(row.Cells["colId"].Value);

            string ragione = row.Cells["colRagione"].Value?.ToString();
            string nome = row.Cells["colNome"].Value?.ToString();
            string referente = row.Cells["colReferente"].Value?.ToString();
            string fatturato = row.Cells["colFatturato"].Value?.ToString();
            string categoria = row.Cells["colCategoria"].Value?.ToString();
            string indirizzo = row.Cells["colIndirizzo"].Value?.ToString();

            int anno = 0;
            if (row.Cells["colAnno"].Value != null)
                int.TryParse(row.Cells["colAnno"].Value.ToString(), out anno);

            using (var f = new ClienteForm(
                ragione,
                nome,
                referente,
                fatturato,
                categoria,
                indirizzo,
                anno
            ))
            {
                f.IsModifica = true;

                if (f.ShowDialog() == DialogResult.OK)
                {
                    AggiornaCliente(
                        id,
                        f.RagioneSociale,
                        f.Nome,
                        f.Referente,
                        f.Fatturato,
                        f.Categoria,
                        f.Indirizzo
                    );

                    CaricaDati();
                }
            }
        }
        void AggiornaCliente(int id, string ragione, string nome, string referente, decimal fatturato, string categoria, string indirizzo)
        {
            using (SQLiteConnection conn = new SQLiteConnection(connString))
            {
                conn.Open();

                SQLiteCommand cmd = new SQLiteCommand(@"
                UPDATE Clienti SET
                RagioneSociale=@r,
                Nome=@n,
                Referente=@ref,
                Fatturato=@f,
                Categoria=@c,
                Indirizzo=@i
                WHERE Id=@id", conn);

                cmd.Parameters.AddWithValue("@r", ragione);
                cmd.Parameters.AddWithValue("@n", nome);
                cmd.Parameters.AddWithValue("@ref", referente);
                cmd.Parameters.AddWithValue("@f", fatturato);
                cmd.Parameters.AddWithValue("@c", categoria);
                cmd.Parameters.AddWithValue("@i", indirizzo);
                cmd.Parameters.AddWithValue("@id", id);

                cmd.ExecuteNonQuery();
            }
        }

        private void btnEliminaCliente_Click(object sender, EventArgs e)
        {
            if (dataGridView2.SelectedRows.Count == 0)
            {
                MessageBox.Show("Seleziona un cliente");
                return;
            }

            DataGridViewRow row = dataGridView2.SelectedRows[0];

            if (row.Cells["colId"].Value == null)
            {
                MessageBox.Show("ID non valido");
                return;
            }

            int id = Convert.ToInt32(row.Cells["colId"].Value);
            string nome = row.Cells["colNome"].Value?.ToString();

            if (MessageBox.Show(
                $"Vuoi eliminare il cliente '{nome}'?",
                "Conferma",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            using (SQLiteConnection conn = new SQLiteConnection(connString))
            {
                conn.Open();

                new SQLiteCommand("DELETE FROM Regali WHERE ClienteId=@id", conn)
                {
                    Parameters = { new SQLiteParameter("@id", id) }
                }.ExecuteNonQuery();

                new SQLiteCommand("DELETE FROM Clienti WHERE Id=@id", conn)
                {
                    Parameters = { new SQLiteParameter("@id", id) }
                }.ExecuteNonQuery();
            }

            CaricaDati();
        }

        private void txtFiltro_TextChanged(object sender, EventArgs e)
        {
            CaricaDati(txtFiltro.Text.Trim());
        }

        private void btnEsportaExcel_Click(object sender, EventArgs e)
        {
            EsportaExcel();
        }

        private void dataGridView2_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
                return;

            int clienteId = Convert.ToInt32(
                dataGridView2.Rows[e.RowIndex].Cells["colId"].Value
            );

            string nome = dataGridView2.Rows[e.RowIndex]
                .Cells["colNome"].Value.ToString();

            using (var f = new StoricoRegaliForm(clienteId, nome, connString))
            {
                f.ShowDialog();
            }
        }
    }
}
