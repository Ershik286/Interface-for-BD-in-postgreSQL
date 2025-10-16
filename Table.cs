using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Npgsql;
using System.Data;

namespace lab4BD
{
    public class stringGrid
    {
        public DataGridView dataGrid;
        public int columnCount;
        public Table parentsTable;
        private List<string[]> dataRows;

        public stringGrid(int colCount, string[] nameColumnTable, Table mainTable)
        {
            this.dataGrid = new DataGridView();
            this.dataRows = new List<string[]>();
            this.columnCount = colCount;
            this.parentsTable = mainTable;

            // Настройка DataGridView
            dataGrid.AllowUserToAddRows = false;
            dataGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dataGrid.BackgroundColor = Color.White;
            dataGrid.RowHeadersVisible = false;

            for (int i = 0; i < columnCount; i++)
            {
                dataGrid.Columns.Add($"{nameColumnTable[i]}", nameColumnTable[i]);
            }
        }

        public void AddRows(params string[] values)
        {
            if (values.Length != columnCount)
            {
                throw new ArgumentException($"Ожидается {columnCount} значений, получено {values.Length}");
            }

            dataRows.Add(values);
            dataGrid.Rows.Add(values);
        }

        public void deleteRows()
        {
            if (dataGrid.SelectedRows.Count == 0)
            {
                MessageBox.Show("Выберите строки для удаления");
                return;
            }

            try
            {
                List<int> rowIndices = new List<int>();
                foreach (DataGridViewRow row in dataGrid.SelectedRows)
                {
                    if (!row.IsNewRow)
                    {
                        rowIndices.Add(row.Index);
                    }
                }

                rowIndices.Sort((a, b) => b.CompareTo(a));

                foreach (int rowIndex in rowIndices)
                {
                    if (rowIndex < dataGrid.Rows.Count && rowIndex < dataRows.Count)
                    {
                        dataGrid.Rows.RemoveAt(rowIndex);
                        dataRows.RemoveAt(rowIndex);
                    }
                }

                MessageBox.Show($"Удалено {rowIndices.Count} строк из интерфейса.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении строк: {ex.Message}", "Ошибка",
                               MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void LoadDataFromDatabase(string tableName, string connectionString)
        {
            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();

                    string sql = $"SELECT * FROM {tableName}";
                    using (var command = new NpgsqlCommand(sql, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        dataGrid.Rows.Clear();
                        dataRows.Clear();

                        while (reader.Read())
                        {
                            string[] rowData = new string[columnCount];
                            for (int i = 0; i < columnCount; i++)
                            {
                                rowData[i] = reader.IsDBNull(i) ? "" : reader.GetValue(i).ToString();
                            }
                            AddRows(rowData);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных: {ex.Message}");
            }
        }

        public void SaveDataToDatabase(string tableName, string connectionString)
        {
            if (dataRows.Count == 0)
            {
                MessageBox.Show("Нет данных для сохранения. Таблица пустая.", "Предупреждение",
                               MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();

                    var columnTypes = GetColumnTypes(tableName, connection);

                    bool tableHasData = CheckIfTableHasData(tableName, connection);

                    if (tableHasData)
                    {
                        var result = MessageBox.Show("В таблице БД уже есть данные. Хотите их перезаписать?",
                                                   "Подтверждение",
                                                   MessageBoxButtons.YesNo,
                                                   MessageBoxIcon.Question);

                        if (result != DialogResult.Yes)
                            return;
                    }

                    string clearSql = $"DELETE FROM {tableName}";
                    using (var command = new NpgsqlCommand(clearSql, connection))
                    {
                        command.ExecuteNonQuery();
                    }

                    int savedRows = 0;
                    foreach (var rowData in dataRows)
                    {
                        if (!IsRowEmpty(rowData))
                        {
                            string insertSql = $"INSERT INTO {tableName} VALUES (";
                            for (int col = 0; col < columnCount; col++)
                            {
                                string value = rowData[col] ?? "";

                                value = FormatValueForSql(value, columnTypes[col.ToString()]);

                                insertSql += value;
                                if (col < columnCount - 1) insertSql += ", ";
                            }
                            insertSql += ")";

                            using (var command = new NpgsqlCommand(insertSql, connection))
                            {
                                command.ExecuteNonQuery();
                                savedRows++;
                            }
                        }
                    }

                    MessageBox.Show($"Успешно сохранено {savedRows} строк в БД", "Сохранение",
                                   MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения данных: {ex.Message}\n\n" +
                               $"Данные в БД не были изменены.",
                               "Ошибка сохранения",
                               MessageBoxButtons.OK, MessageBoxIcon.Error);

                // Перезагрузка БД на случай ошибки
                parentsTable.ReloadTableData();
            }
        }

        // Метод для получения типов столбцов
        private Dictionary<string, string> GetColumnTypes(string tableName, NpgsqlConnection connection)
        {
            var columnTypes = new Dictionary<string, string>();

            string sql = $@"
        SELECT column_name, data_type 
        FROM information_schema.columns 
        WHERE table_name = '{tableName}' 
        ORDER BY ordinal_position";

            using (var command = new NpgsqlCommand(sql, connection))
            using (var reader = command.ExecuteReader())
            {
                int index = 0;
                while (reader.Read())
                {
                    string columnName = reader.GetString(0);
                    string dataType = reader.GetString(1);
                    columnTypes[index.ToString()] = dataType; // Используем индекс как ключ
                    index++;
                }
            }

            return columnTypes;
        }

        // Метод для форматирования значений в зависимости от типа столбца
        private string FormatValueForSql(string value, string dataType)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "NULL";

            value = value.Replace("'", "''");

            if (dataType == "numeric" || dataType == "decimal" ||
                dataType == "real" || dataType == "double precision" ||
                dataType == "integer" || dataType == "bigint" ||
                dataType == "smallint")
            {
                value = value.Replace(",", ".");

                if (decimal.TryParse(value, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out decimal numericValue))
                {
                    return value;
                }
                else
                {
                    return $"'{value}'";
                }
            }
            return $"'{value}'";
        }

        private bool CheckIfTableHasData(string tableName, NpgsqlConnection connection)
        {
            string checkSql = $"SELECT COUNT(*) FROM {tableName}";
            using (var command = new NpgsqlCommand(checkSql, connection))
            {
                long count = (long)command.ExecuteScalar();
                return count > 0;
            }
        }

        private bool IsRowEmpty(string[] row)
        {
            foreach (string cell in row)
            {
                if (!string.IsNullOrWhiteSpace(cell))
                    return false;
            }
            return true;
        }

        public void Clear()
        {
            dataGrid.Rows.Clear();
            dataRows.Clear();
        }
        public int GetRowCount()
        {
            return dataRows.Count;
        }
    }

    public class Table
    {
        public string nameTable { get; private set; }
        private int column = 0;
        private List<string> nameColumns;
        public Form1 parentForm;
        private stringGrid gridView;

        private Label mainLabel;
        private Label settingsSelect;
        private Label appendLabel;

        private Button deleteRow;
        private Button insertRow;
        private Button saveButton;
        private Button loadFromDBButton;
        private Button saveToDBButton;
        private Button reloadButton;

        private List<Label> labelsAppend;
        private List<Label> labelsSettings;
        private List<TextBox> textBoxesFilter;
        private List<TextBox> textBoxesAppend;

        public Table(int col, int row, string nameTable, Form1 form)
        {
            this.nameTable = nameTable;
            this.column = col > 0 ? col : 1;
            this.nameColumns = new List<string>(column);
            this.parentForm = form;

            for (int i = 0; i < column; i++)
            {
                nameColumns.Add($"Column_{i + 1}");
            }

            CreateInterface();
            CreateTableInDatabase();
        }

        public Table(string tableName, Form1 form)
        {
            this.nameTable = tableName;
            this.parentForm = form;
            LoadTableStructureFromDatabase();
            CreateInterface();
            LoadDataFromDatabase();
        }

        private void LoadTableStructureFromDatabase()
        {
            try
            {
                using (var connection = new NpgsqlConnection(parentForm.connectionString))
                {
                    connection.Open();

                    string sql = $@"
                        SELECT column_name 
                        FROM information_schema.columns 
                        WHERE table_name = '{nameTable}' 
                        ORDER BY ordinal_position";

                    nameColumns = new List<string>();
                    using (var command = new NpgsqlCommand(sql, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            nameColumns.Add(reader.GetString(0));
                        }
                    }

                    column = nameColumns.Count;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки структуры таблицы: {ex.Message}");
            }
        }

        private void CreateTableInDatabase()
        {
            try
            {
                using (var connection = new NpgsqlConnection(parentForm.connectionString))
                {
                    connection.Open();

                    string createSql = $@"
                        CREATE TABLE IF NOT EXISTS {nameTable} (
                            id SERIAL PRIMARY KEY";

                    for (int i = 0; i < column; i++)
                    {
                        createSql += $", {nameColumns[i]} TEXT";
                    }
                    createSql += ")";

                    using (var command = new NpgsqlCommand(createSql, connection))
                    {
                        command.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка создания таблицы в БД: {ex.Message}");
            }
        }

        public void CreateInterface()
        {
            mainLabel = new Label();
            mainLabel.Text = "Таблица: \"" + nameTable + "\"";
            mainLabel.Font = new Font("Arial", 12, FontStyle.Bold);
            mainLabel.AutoSize = true;
            mainLabel.Location = new Point(80, 45);
            parentForm.Controls.Add(mainLabel);

            settingsSelect = new Label();
            settingsSelect.Text = "Настройка выборки";
            settingsSelect.Font = new Font("Arial", 8, FontStyle.Bold);
            settingsSelect.AutoSize = true;
            settingsSelect.Location = new Point(80, 80);
            parentForm.Controls.Add(settingsSelect);

            appendLabel = new Label();
            appendLabel.Text = "Добавление записи";
            appendLabel.Font = new Font("Arial", 10, FontStyle.Bold);
            appendLabel.AutoSize = true;
            appendLabel.Location = new Point(100, 470);
            parentForm.Controls.Add(appendLabel);

            labelsAppend = new List<Label>();
            labelsSettings = new List<Label>();
            textBoxesFilter = new List<TextBox>();
            textBoxesAppend = new List<TextBox>();

            int startX = 60;
            int startY = 110;
            int labelWidth = 175;
            int labelHeight = 25;

            int saveButtonX = 0;

            for (int i = 0; i < column; i++)
            {
                Label label = new Label();
                label.Text = nameColumns[i];
                label.Size = new Size(labelWidth, labelHeight);
                label.Location = new Point(startX + i * (labelWidth), startY);
                label.Font = new Font("Arial", 9);
                labelsSettings.Add(label);
                parentForm.Controls.Add(label);

                TextBox textBox = new TextBox();
                textBox.Size = new Size(labelWidth, labelHeight);
                textBox.Location = new Point(startX + i * (labelWidth) + 1, startY + 30);
                textBox.Font = new Font("Arial", 10);
                textBox.BorderStyle = BorderStyle.FixedSingle;
                saveButtonX = startX + (i + 1) * (labelWidth);
                textBoxesFilter.Add(textBox);
                parentForm.Controls.Add(textBox);

                Label labelAppend = new Label();
                labelAppend.Text = nameColumns[i];
                labelAppend.Size = new Size(labelWidth, labelHeight);
                labelAppend.Location = new Point(startX + i * (labelWidth), startY + 410);
                labelAppend.Font = new Font("Arial", 9);
                labelsAppend.Add(labelAppend);
                parentForm.Controls.Add(labelAppend);

                TextBox textBoxAppend = new TextBox();
                textBoxAppend.Size = new Size(labelWidth, labelHeight);
                textBoxAppend.Location = new Point(startX + i * (labelWidth) + 1, startY + 440);
                textBoxAppend.Font = new Font("Arial", 10);
                textBoxAppend.BorderStyle = BorderStyle.FixedSingle;
                textBoxesAppend.Add(textBoxAppend);
                parentForm.Controls.Add(textBoxAppend);
            }

            saveButton = new Button();
            saveButton.Size = new Size(120, 30);
            saveButton.Text = "Применить фильтр";
            saveButton.Location = new Point(saveButtonX + 100, startY - 20);
            saveButton.Font = new Font("Arial", 9);
            saveButton.BackColor = Color.LightGray;
            saveButton.FlatStyle = FlatStyle.Flat;
            parentForm.Controls.Add(saveButton);

            deleteRow = new Button();
            deleteRow.Size = new Size(180, 30);
            deleteRow.Text = "Удалить выбранные строки";
            deleteRow.Location = new Point(saveButtonX + 100, 450);
            deleteRow.Font = new Font("Arial", 9);
            deleteRow.BackColor = Color.LightGray;
            deleteRow.FlatStyle = FlatStyle.Flat;
            parentForm.Controls.Add(deleteRow);

            insertRow = new Button();
            insertRow.Size = new Size(180, 30);
            insertRow.Text = "Добавить запись";
            insertRow.Location = new Point(saveButtonX + 100, 550);
            insertRow.Font = new Font("Arial", 9);
            insertRow.BackColor = Color.LightGray;
            insertRow.FlatStyle = FlatStyle.Flat;
            parentForm.Controls.Add(insertRow);

            loadFromDBButton = new Button();
            loadFromDBButton.Size = new Size(180, 30);
            loadFromDBButton.Text = "Загрузить из БД";
            loadFromDBButton.Location = new Point(saveButtonX + 300, startY - 20);
            loadFromDBButton.Font = new Font("Arial", 9);
            loadFromDBButton.BackColor = Color.LightBlue;
            loadFromDBButton.FlatStyle = FlatStyle.Flat;
            parentForm.Controls.Add(loadFromDBButton);

            saveToDBButton = new Button();
            saveToDBButton.Size = new Size(180, 30);
            saveToDBButton.Text = "Сохранить в БД";
            saveToDBButton.Location = new Point(saveButtonX + 300, 450);
            saveToDBButton.Font = new Font("Arial", 9);
            saveToDBButton.BackColor = Color.LightGreen;
            saveToDBButton.FlatStyle = FlatStyle.Flat;
            parentForm.Controls.Add(saveToDBButton);

            reloadButton = new Button();
            reloadButton.Size = new Size(180, 30);
            reloadButton.Text = "Перезагрузить";
            reloadButton.Location = new Point(saveButtonX + 300, 500);
            reloadButton.Font = new Font("Arial", 9);
            reloadButton.BackColor = Color.LightYellow;
            reloadButton.FlatStyle = FlatStyle.Flat;
            parentForm.Controls.Add(reloadButton);

            gridView = new stringGrid(column, nameColumns.ToArray(), this);
            gridView.dataGrid.Location = new Point(63, 200);
            gridView.dataGrid.Size = new Size(875, 200);
            gridView.dataGrid.BackgroundColor = Color.White;
            parentForm.Controls.Add(gridView.dataGrid);

            deleteRow.Click += DeleteRow_Click;
            insertRow.Click += InsertRow_Click;
            saveButton.Click += SaveButton_Click;
            loadFromDBButton.Click += LoadFromDBButton_Click;
            saveToDBButton.Click += SaveToDBButton_Click;
            reloadButton.Click += (s, e) => ReloadTableData();
        }

        private void DeleteRow_Click(object sender, EventArgs e)
        {
            gridView.deleteRows();
        }

        private void InsertRow_Click(object sender, EventArgs e)
        {
            string[] rowData = new string[column];
            bool hasData = false;

            for (int i = 0; i < column; i++)
            {
                rowData[i] = textBoxesAppend[i].Text;
                if (!string.IsNullOrWhiteSpace(rowData[i]))
                    hasData = true;
                textBoxesAppend[i].Clear();
            }

            if (hasData)
            {
                gridView.AddRows(rowData);
                MessageBox.Show("Запись добавлена в интерфейс.");
            }
            else
            {
                MessageBox.Show("Введите данные для добавления");
            }
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            ApplyFilter();
        }

        private void LoadFromDBButton_Click(object sender, EventArgs e)
        {
            LoadDataFromDatabase();
        }
        private void SaveToDBButton_Click(object sender, EventArgs e)
        {
            if (gridView.GetRowCount() == 0)
            {
                MessageBox.Show("Нет данных для сохранения. Таблица пустая.", "Предупреждение",
                               MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            gridView.SaveDataToDatabase(nameTable, parentForm.connectionString);
        }

        private void LoadDataFromDatabase()
        {
            gridView.LoadDataFromDatabase(nameTable, parentForm.connectionString);
        }

        public void ReloadTableData()
        {
            try
            {
                gridView.Clear();
                LoadDataFromDatabase();
                ResetFilters();
                MessageBox.Show("Данные перезагружены из БД", "Перезагрузка",
                               MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при перезагрузке данных: {ex.Message}", "Ошибка",
                               MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ResetFilters()
        {
            foreach (var textBox in textBoxesFilter)
            {
                textBox.Clear();
            }

            foreach (DataGridViewRow row in gridView.dataGrid.Rows)
            {
                row.Visible = true;
            }
        }

        private void ApplyFilter()
        {
            List<string> dataFilter = new List<string>();
            List<string> filterValues = new List<string>();

            for (int i = 0; i < textBoxesFilter.Count; i++)
            {
                if (!string.IsNullOrEmpty(textBoxesFilter[i].Text))
                {
                    dataFilter.Add(nameColumns[i]);
                    filterValues.Add(textBoxesFilter[i].Text);
                }
            }

            foreach (DataGridViewRow row in gridView.dataGrid.Rows)
            {
                bool shouldShow = true;

                for (int count = 0; count < dataFilter.Count; count++)
                {
                    string columnName = dataFilter[count];
                    string filterValue = filterValues[count];

                    if (row.Cells[columnName].Value != null)
                    {
                        string cellValue = row.Cells[columnName].Value.ToString();
                        if (!cellValue.ToLower().Contains(filterValue.ToLower()))
                        {
                            shouldShow = false;
                            break;
                        }
                    }
                    else
                    {
                        shouldShow = false;
                        break;
                    }
                }

                row.Visible = shouldShow;
            }
            MessageBox.Show("Фильтр применен");
        }

        public void insert(string[] rowData)
        {
            bool hasData = false;

            for (int i = 0; i < column; i++)
            {
                rowData[i] = textBoxesAppend[i].Text;
                if (!string.IsNullOrWhiteSpace(rowData[i]))
                    hasData = true;
                textBoxesAppend[i].Clear();
            }
        }

        public void Hide()
        {
            gridView.dataGrid.Visible = false;
            mainLabel.Visible = false;
            settingsSelect.Visible = false;
            appendLabel.Visible = false;
            deleteRow.Visible = false;
            insertRow.Visible = false;
            saveButton.Visible = false;
            loadFromDBButton.Visible = false;
            saveToDBButton.Visible = false;
            reloadButton.Visible = false;

            foreach (var label in labelsSettings) label.Visible = false;
            foreach (var label in labelsAppend) label.Visible = false;
            foreach (var textBox in textBoxesFilter) textBox.Visible = false;
            foreach (var textBox in textBoxesAppend) textBox.Visible = false;
        }

        public void Show()
        {
            gridView.dataGrid.Visible = true;
            mainLabel.Visible = true;
            settingsSelect.Visible = true;
            appendLabel.Visible = true;
            deleteRow.Visible = true;
            insertRow.Visible = true;
            saveButton.Visible = true;
            loadFromDBButton.Visible = true;
            saveToDBButton.Visible = true;
            reloadButton.Visible = true;

            foreach (var label in labelsSettings) label.Visible = true;
            foreach (var label in labelsAppend) label.Visible = true;
            foreach (var textBox in textBoxesFilter) textBox.Visible = true;
            foreach (var textBox in textBoxesAppend) textBox.Visible = true;
        }

        public void Dispose()
        {
            gridView?.dataGrid?.Dispose();
        }
    }
}