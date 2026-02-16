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
            using (SQLiteConnection conn = new SQLiteConnection(connString))
            {
                conn.Open();

                using (var tx = conn.BeginTransaction())
                {
                    var wsClienti = wb.Worksheets.FirstOrDefault(w =>
                        w.Name.Equals("Clienti", StringComparison.OrdinalIgnoreCase)
                    ) ?? wb.Worksheet(1);
                    var wsStorico = wb.Worksheets.FirstOrDefault(w =>
                        w.Name.Equals("Storico Regali", StringComparison.OrdinalIgnoreCase)
                    );

                    bool storicoPresente = wsStorico != null;

                    var headersClienti = wsClienti.Row(1).CellsUsed()
                            .ToDictionary(
                                c => c.GetString().Trim(),
                                c => c.Address.ColumnNumber,
                                StringComparer.OrdinalIgnoreCase
                            );

                    int lastRowClienti = wsClienti.LastRowUsed()?.RowNumber() ?? 1;

                    for (int r = 2; r <= lastRowClienti; r++)
                    {
                        string nome = GetCellString(wsClienti, headersClienti, r, "Nome");
                        if (string.IsNullOrWhiteSpace(nome))
                            continue;

                        string ragione = GetCellString(wsClienti, headersClienti, r, "Ragione Sociale");
                        string referente = GetCellString(wsClienti, headersClienti, r, "Referente Commerciale");
                        string fatturato = GetCellString(wsClienti, headersClienti, r, "Fatturato");
                        string categoria = GetCellString(wsClienti, headersClienti, r, "Categoria");
                        string indirizzo = GetCellString(wsClienti, headersClienti, r, "Indirizzo");

                        int clienteId = GetOrCreateCliente(conn, ragione, nome, referente, fatturato, categoria, indirizzo);

                        if (storicoPresente)
                            continue;

                        string annoText = GetCellString(wsClienti, headersClienti, r, "Anno");
                        if (int.TryParse(annoText, out int anno))
                            InserisciRegaloSeNonEsiste(conn, clienteId, anno, null, null);
                    }

                    if (storicoPresente)
                    {
                        var headersStorico = wsStorico.Row(1).CellsUsed()
                            .ToDictionary(
                                c => c.GetString().Trim(),
                                c => c.Address.ColumnNumber,
                                StringComparer.OrdinalIgnoreCase
                            );
                        int lastRowStorico = wsStorico.LastRowUsed()?.RowNumber() ?? 1;

                        for (int r = 2; r <= lastRowStorico; r++)
                        {
                            string nome = GetCellString(wsStorico, headersStorico, r, "Nome");
                            string regalo = GetCellString(wsStorico, headersStorico, r, "Regalo");
                            string categoriaRegalo = GetCellString(wsStorico, headersStorico, r, "Categoria Regalo");
                            if (string.IsNullOrWhiteSpace(categoriaRegalo))
                                categoriaRegalo = GetCellString(wsStorico, headersStorico, r, "Categoria");

                            string annoText = GetCellString(wsStorico, headersStorico, r, "Anno");

                            if (string.IsNullOrWhiteSpace(nome) || !int.TryParse(annoText, out int anno))
                                continue;

                            int clienteId = GetOrCreateCliente(conn, "", nome, "", "", "", "");
                            InserisciRegaloSeNonEsiste(conn, clienteId, anno, regalo, categoriaRegalo);
                        }
                    }
                    tx.Commit();
                }
            }

            CaricaDati();
            MessageBox.Show("Importazione Excel completata!", "Import");
        }

        string GetCellString(IXLWorksheet ws, Dictionary<string, int> headers, int row, string headerName)
        {
            if (!headers.TryGetValue(headerName, out int col))
                return "";

            return ws.Cell(row, col).GetString().Trim();
        }

        int GetOrCreateCliente(SQLiteConnection conn, string ragione, string nome, string referente, string fatturato, string categoria, string indirizzo)
        {
            SQLiteCommand check = new SQLiteCommand(@"
                SELECT Id
                FROM Clienti
                WHERE lower(trim(Nome)) = lower(trim(@nome))
                ORDER BY Id
                LIMIT 1
            ", conn);
            check.Parameters.AddWithValue("@nome", nome);

            object existing = check.ExecuteScalar();
            if (existing != null && existing != DBNull.Value)
            {
                int id = Convert.ToInt32(existing);

                SQLiteCommand update = new SQLiteCommand(@"
                    UPDATE Clienti
                    SET
                        RagioneSociale = CASE WHEN @ragione <> '' THEN @ragione ELSE RagioneSociale END,
                        Referente = CASE WHEN @referente <> '' THEN @referente ELSE Referente END,
                        Fatturato = CASE WHEN @fatturato <> '' THEN @fatturato ELSE Fatturato END,
                        Categoria = CASE WHEN @categoria <> '' THEN @categoria ELSE Categoria END,
                        Indirizzo = CASE WHEN @indirizzo <> '' THEN @indirizzo ELSE Indirizzo END
                    WHERE Id = @id
                ", conn);

                update.Parameters.AddWithValue("@ragione", ragione ?? "");
                update.Parameters.AddWithValue("@referente", referente ?? "");
                update.Parameters.AddWithValue("@fatturato", fatturato ?? "");
                update.Parameters.AddWithValue("@categoria", categoria ?? "");
                update.Parameters.AddWithValue("@indirizzo", indirizzo ?? "");
                update.Parameters.AddWithValue("@id", id);
                update.ExecuteNonQuery();

                return id;
            }

            SQLiteCommand insert = new SQLiteCommand(@"
                INSERT INTO Clienti
                (RagioneSociale, Nome, Referente, Fatturato, Categoria, Indirizzo)
                VALUES (@ragione, @nome, @referente, @fatturato, @categoria, @indirizzo);
                SELECT last_insert_rowid();
            ", conn);

            insert.Parameters.AddWithValue("@ragione", ragione ?? "");
            insert.Parameters.AddWithValue("@nome", nome ?? "");
            insert.Parameters.AddWithValue("@referente", referente ?? "");
            insert.Parameters.AddWithValue("@fatturato", fatturato ?? "");
            insert.Parameters.AddWithValue("@categoria", categoria ?? "");
            insert.Parameters.AddWithValue("@indirizzo", indirizzo ?? "");

            return Convert.ToInt32(insert.ExecuteScalar());
        }

        void InserisciRegaloSeNonEsiste(SQLiteConnection conn, int clienteId, int anno, string regalo, string categoria)
        {
            SQLiteCommand check = new SQLiteCommand(@"
                SELECT COUNT(*)
                FROM Regali
                WHERE ClienteId = @id
                  AND Anno = @anno
                  AND ifnull(Regalo, '') = ifnull(@regalo, '')
                  AND ifnull(Categoria, '') = ifnull(@categoria, '')
            ", conn);

            check.Parameters.AddWithValue("@id", clienteId);
            check.Parameters.AddWithValue("@anno", anno);
            check.Parameters.AddWithValue("@regalo", (object)(regalo ?? ""));
            check.Parameters.AddWithValue("@categoria", (object)(categoria ?? ""));

            long exists = (long)check.ExecuteScalar();
            if (exists > 0)
                return;

            SQLiteCommand insert = new SQLiteCommand(@"
                INSERT INTO Regali (ClienteId, Anno, Regalo, Categoria)
                VALUES (@id, @anno, @regalo, @categoria)
            ", conn);

            insert.Parameters.AddWithValue("@id", clienteId);
            insert.Parameters.AddWithValue("@anno", anno);
            insert.Parameters.AddWithValue("@regalo", string.IsNullOrWhiteSpace(regalo) ? (object)DBNull.Value : regalo);
            insert.Parameters.AddWithValue("@categoria", string.IsNullOrWhiteSpace(categoria) ? (object)DBNull.Value : categoria);
            insert.ExecuteNonQuery();
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

                //wsStorico.Cell(1, 1).Value = "Cliente ID";
                wsStorico.Cell(1, 1).Value = "Nome";
                wsStorico.Cell(1, 2).Value = "Regalo";
                wsStorico.Cell(1, 3).Value = "Categoria Regalo";
                wsStorico.Cell(1, 4).Value = "Anno";

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
                    c.Nome,
                    r.Regalo,
                    r.Categoria,
                    r.Anno
                FROM Regali r
                INNER JOIN Clienti c ON c.Id = r.ClienteId
                ORDER BY c.Nome, r.Anno DESC";

                    using (SQLiteCommand cmdStorico = new SQLiteCommand(queryStorico, conn))
                    using (SQLiteDataReader readerStorico = cmdStorico.ExecuteReader())
                    {
                        while (readerStorico.Read())
                        {
                            wsStorico.Cell(rowStorico, 1).Value = readerStorico["Nome"].ToString();
                            wsStorico.Cell(rowStorico, 2).Value = readerStorico["Regalo"] == DBNull.Value ? "" : readerStorico["Regalo"].ToString();
                            wsStorico.Cell(rowStorico, 3).Value = readerStorico["Categoria"] == DBNull.Value ? "" : readerStorico["Categoria"].ToString();
                            wsStorico.Cell(rowStorico, 4).Value = readerStorico["Anno"] == DBNull.Value ? "" : readerStorico["Anno"].ToString();

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
