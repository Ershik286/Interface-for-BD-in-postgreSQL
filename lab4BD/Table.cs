using System.Windows.Forms;
using Npgsql;
using System.Data;
using System.IO;
using System.Text.Json;
using System.Linq;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace lab4BD
{
    // Класс для хранения переводов из JSON
    public class TranslationData
    {
        public Dictionary<string, string> Columns { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, string> Tables { get; set; } = new Dictionary<string, string>();
    }

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

        public void AddRows(bool loadFromDB = false, params string[] values) {
            if (values.Length != columnCount) {
                throw new ArgumentException($"Ожидается {columnCount} значений, получено {values.Length}");
            }

            try {
                if (loadFromDB) {
                    dataRows.Add(values);
                    dataGrid.Rows.Add(values);
                    return;
                }

                using (var connection = new NpgsqlConnection(parentsTable.parentForm.connectionString)) {
                    connection.Open();

                    var columnTypes = GetColumnTypes(parentsTable.nameTable, connection);

                    if (CheckForDuplicate(parentsTable.nameTable, values, connection)) {
                        MessageBox.Show($"Обнаружена дублирующаяся запись. Пропуск строки: {string.Join(", ", values)}",
                                      "Дублирующаяся запись",
                                      MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    string insertSql = $"INSERT INTO {parentsTable.nameTable} VALUES (";
                    for (int col = 0; col < columnCount; col++) {
                        string value = values[col] ?? "";
                        value = FormatValueForSql(value, columnTypes[col.ToString()]);
                        insertSql += value;
                        if (col < columnCount - 1) insertSql += ", ";
                    }
                    insertSql += ")";

                    using (var command = new NpgsqlCommand(insertSql, connection)) {
                        command.ExecuteNonQuery();
                    }

                    dataRows.Add(values);
                    dataGrid.Rows.Add(values);

                    MessageBox.Show("Запись успешно добавлена!", "Успех",
                                  MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex) {
                MessageBox.Show($"Ошибка при добавлении записи: {ex.Message}",
                              "Ошибка",
                              MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void deleteRows() {
            if (dataGrid.SelectedRows.Count == 0) {
                MessageBox.Show("Выберите строки для удаления");
                return;
            }

            var result = MessageBox.Show($"Вы уверены, что хотите удалить {dataGrid.SelectedRows.Count} строк из БД?",
                                       "Подтверждение удаления",
                                       MessageBoxButtons.YesNo,
                                       MessageBoxIcon.Question);

            if (result != DialogResult.Yes)
                return;

            try {
                using (var connection = new NpgsqlConnection(parentsTable.parentForm.connectionString)) {
                    connection.Open();

                    string primaryKeyColumnName = GetPrimaryKeyColumn(parentsTable.nameTable, connection);
                    var columnTypes = GetColumnTypes(parentsTable.nameTable, connection);

                    int deletedCount = 0;
                    List<int> rowIndices = new List<int>();

                    foreach (DataGridViewRow row in dataGrid.SelectedRows) {
                        if (!row.IsNewRow) {
                            rowIndices.Add(row.Index);
                        }
                    }

                    rowIndices.Sort((a, b) => b.CompareTo(a));

                    foreach (int rowIndex in rowIndices) {
                        if (rowIndex < dataGrid.Rows.Count && rowIndex < dataRows.Count) {
                            string[] rowData = dataRows[rowIndex];

                            if (DeleteRowFromDatabase(parentsTable.nameTable, primaryKeyColumnName, rowData, connection, columnTypes)) {
                                dataGrid.Rows.RemoveAt(rowIndex);
                                dataRows.RemoveAt(rowIndex);
                                deletedCount++;
                            }
                        }
                    }

                    if (deletedCount > 0) {
                        MessageBox.Show($"Удалено {deletedCount} строк из БД.", "Успех",
                                       MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else {
                        MessageBox.Show("Не удалось удалить строки из БД.", "Предупреждение",
                                       MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }
            catch (Exception ex) {
                MessageBox.Show($"Ошибка при удалении строк: {ex.Message}", "Ошибка",
                               MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private bool DeleteRowFromDatabase(string tableName, string primaryKeyColumn, string[] rowData, NpgsqlConnection connection, Dictionary<string, string> columnTypes) {
            try {
                int primaryKeyIndex = -1;
                var allColumnTypes = GetColumnTypes(tableName, connection);

                for (int i = 0; i < columnCount; i++) {
                    string columnName = dataGrid.Columns[i].Name;
                    if (columnName.Equals(primaryKeyColumn, StringComparison.OrdinalIgnoreCase)) {
                        primaryKeyIndex = i;
                        break;
                    }
                }

                if (primaryKeyIndex == -1) {
                    primaryKeyIndex = 0;
                }

                string primaryKeyValue = rowData[primaryKeyIndex] ?? "";

                if (string.IsNullOrEmpty(primaryKeyValue)) {
                    MessageBox.Show("Не удалось определить значение первичного ключа для удаления", "Ошибка",
                                  MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }

                string primaryKeyType = columnTypes.ContainsKey(primaryKeyColumn) ?
                                      columnTypes[primaryKeyColumn] : "text";

                string formattedValue = FormatValueForSql(primaryKeyValue, primaryKeyType);

                string deleteSql = $"DELETE FROM {tableName} WHERE {primaryKeyColumn} = {formattedValue}";

                using (var command = new NpgsqlCommand(deleteSql, connection)) {
                    int affectedRows = command.ExecuteNonQuery();
                    return affectedRows > 0;
                }
            }
            catch (Exception ex) {
                MessageBox.Show($"Ошибка при удалении строки из БД: {ex.Message}", "Ошибка",
                              MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
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
                            AddRows(true, rowData);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных: {ex.Message}");
            }
        }

        private bool CheckForDuplicate(string tableName, string[] rowData, NpgsqlConnection connection)
        {
            try
            {
                // Получаем названия столбцов
                var columnNames = GetColumnNames(tableName, connection);

                // Строим условие для проверки дубликатов
                string whereClause = "";
                for (int i = 0; i < columnNames.Count; i++)
                {
                    if (i > 0) whereClause += " AND ";
                    whereClause += $"{columnNames[i]} = @param{i}";
                }

                string checkSql = $"SELECT COUNT(*) FROM {tableName} WHERE {whereClause}";
                using (var command = new NpgsqlCommand(checkSql, connection))
                {
                    for (int i = 0; i < rowData.Length; i++)
                    {
                        command.Parameters.AddWithValue($"@param{i}", rowData[i] ?? (object)DBNull.Value);
                    }

                    long count = (long)command.ExecuteScalar();
                    return count > 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при проверке дубликатов: {ex.Message}");
                return false;
            }
        }

        private List<string> GetColumnNames(string tableName, NpgsqlConnection connection)
        {
            var columnNames = new List<string>();

            string sql = $@"
                SELECT column_name 
                FROM information_schema.columns 
                WHERE table_name = '{tableName}' 
                ORDER BY ordinal_position";

            using (var command = new NpgsqlCommand(sql, connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    columnNames.Add(reader.GetString(0));
                }
            }

            return columnNames;
        }

        private string GetPrimaryKeyColumn(string tableName, NpgsqlConnection connection) {
            try {
                string sql = $@"
            SELECT column_name 
            FROM information_schema.key_column_usage 
            WHERE table_name = '{tableName}' 
            AND constraint_name LIKE '%pkey%'
            LIMIT 1";

                using (var command = new NpgsqlCommand(sql, connection)) {
                    var result = command.ExecuteScalar();
                    return result?.ToString() ?? "id"; // По умолчанию используем 'id'
                }
            }
            catch {
                return "id"; // Fallback на стандартный первичный ключ
            }
        }

        private Dictionary<string, string> GetColumnTypes(string tableName, NpgsqlConnection connection) {
            var columnTypes = new Dictionary<string, string>();

            string sql = $@"
                SELECT column_name, data_type 
                FROM information_schema.columns 
                WHERE table_name = '{tableName}' 
                ORDER BY ordinal_position";

            using (var command = new NpgsqlCommand(sql, connection))
            using (var reader = command.ExecuteReader()) {
                int index = 0;
                while (reader.Read()) {
                    string columnName = reader.GetString(0);
                    string dataType = reader.GetString(1);
                    columnTypes[index.ToString()] = dataType;
                    // Также сохраняем по имени колонки для удобства
                    columnTypes[columnName] = dataType;
                    index++;
                }
            }

            return columnTypes;
        }

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
        private List<string> originalColumnNames; // Оригинальные названия столбцов из БД
        private Dictionary<string, string> columnDataTypes; // Типы данных столбцов
        public Form1 parentForm;
        private stringGrid gridView;
        private TranslationData translations;

        private Label mainLabel;
        private Label settingsSelect;
        private Label appendLabel;

        private Button deleteRow;
        private Button insertRow;
        private Button saveButton;
        private Button loadFromDBButton;
        //private Button saveToDBButton;
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
            this.originalColumnNames = new List<string>(column);
            this.columnDataTypes = new Dictionary<string, string>();
            this.parentForm = form;
            this.translations = LoadTranslations();

            for (int i = 0; i < column; i++)
            {
                string columnName = $"Поле_{i + 1}";
                nameColumns.Add(columnName);
                originalColumnNames.Add(columnName);
                columnDataTypes[columnName] = "text"; // По умолчанию текст
            }

            CreateInterface();
            CreateTableInDatabase();
        }

        public Table(string tableName, Form1 form)
        {
            this.nameTable = tableName;
            this.parentForm = form;
            this.translations = LoadTranslations();
            this.originalColumnNames = new List<string>();
            this.columnDataTypes = new Dictionary<string, string>();
            LoadTableStructureFromDatabase();
            CreateInterface();
            LoadDataFromDatabase();
        }

        public string GetTranslatedTableName()
        {
            if (translations.Tables != null && translations.Tables.ContainsKey(nameTable.ToLower()))
            {
                return translations.Tables[nameTable.ToLower()];
            }
            return FormatTableName(nameTable);
        }

        private TranslationData LoadTranslations()
        {
            var translationData = new TranslationData();

            try
            {
                if (File.Exists("translations.json"))
                {
                    string json = File.ReadAllText("translations.json");

                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        ReadCommentHandling = JsonCommentHandling.Skip,
                        AllowTrailingCommas = true
                    };

                    translationData = JsonSerializer.Deserialize<TranslationData>(json, options);

                    if (translationData == null)
                    {
                        translationData = new TranslationData();
                    }
                }
                else
                {
                    translationData = new TranslationData();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки переводов: {ex.Message}");
                translationData = new TranslationData();
            }

            return translationData;
        }

        private string TranslateColumnName(string originalName)
        {
            if (translations.Columns != null && translations.Columns.ContainsKey(originalName.ToLower()))
            {
                return translations.Columns[originalName.ToLower()];
            }
            return FormatColumnName(originalName);
        }

        private string TranslateTableName(string originalName)
        {
            if (translations.Tables != null && translations.Tables.ContainsKey(originalName.ToLower()))
            {
                return translations.Tables[originalName.ToLower()];
            }
            return FormatTableName(originalName);
        }

        private string FormatColumnName(string name)
        {
            name = name.Replace('_', ' ');
            if (name.Length > 0)
            {
                name = char.ToUpper(name[0]) + name.Substring(1).ToLower();
            }
            return name;
        }

        private string FormatTableName(string name)
        {
            name = name.Replace('_', ' ');
            if (name.Length > 0)
            {
                name = char.ToUpper(name[0]) + name.Substring(1).ToLower();
            }
            return name;
        }

        private void LoadTableStructureFromDatabase()
        {
            try
            {
                using (var connection = new NpgsqlConnection(parentForm.connectionString))
                {
                    connection.Open();

                    string sql = $@"
                        SELECT column_name, data_type 
                        FROM information_schema.columns 
                        WHERE table_name = '{nameTable}' 
                        ORDER BY ordinal_position";

                    nameColumns = new List<string>();
                    originalColumnNames = new List<string>();
                    columnDataTypes = new Dictionary<string, string>();

                    using (var command = new NpgsqlCommand(sql, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string originalName = reader.GetString(0);
                            string dataType = reader.GetString(1);

                            originalColumnNames.Add(originalName);
                            string translatedName = TranslateColumnName(originalName);
                            nameColumns.Add(translatedName);
                            columnDataTypes[translatedName] = dataType;
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

        private bool ValidateNumericInput(string value, string dataType)
        {
            if (string.IsNullOrWhiteSpace(value))
                return true;

            if (dataType == "numeric" || dataType == "decimal" ||
                dataType == "real" || dataType == "double precision" ||
                dataType == "integer" || dataType == "bigint" ||
                dataType == "smallint")
            {
                // Заменяем запятую на точку для корректного парсинга
                string normalizedValue = value.Replace(",", ".");

                if (dataType == "integer" || dataType == "bigint" || dataType == "smallint")
                {
                    return int.TryParse(normalizedValue, out _);
                }
                else
                {
                    return decimal.TryParse(normalizedValue, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out _);
                }
            }

            return true; // Для нечисловых типов проверка не требуется
        }

        // Проверка на дубликаты при добавлении
        private bool CheckForDuplicateInGrid(string[] newRowData)
        {
            foreach (var existingRow in gridView.dataGrid.Rows)
            {
                if (existingRow is DataGridViewRow row && !row.IsNewRow)
                {
                    bool isDuplicate = true;
                    for (int i = 0; i < column; i++)
                    {
                        string existingValue = row.Cells[i].Value?.ToString() ?? "";
                        string newValue = newRowData[i] ?? "";

                        if (existingValue != newValue)
                        {
                            isDuplicate = false;
                            break;
                        }
                    }

                    if (isDuplicate)
                    {
                        return true;
                    }
                }
            }
            return false;
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
                        createSql += $", {originalColumnNames[i]} TEXT";
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
        public void insert(string[] rowData) {
            bool hasData = false;

            for (int i = 0; i < column; i++) {
                rowData[i] = textBoxesAppend[i].Text;
                if (!string.IsNullOrWhiteSpace(rowData[i]))
                    hasData = true;
                textBoxesAppend[i].Clear();
            }
        }

        public void CreateInterface()
        {
            // Переводим название таблицы для интерфейса
            string translatedTableName = TranslateTableName(nameTable);

            mainLabel = new Label();
            mainLabel.Text = $"Таблица: \"{translatedTableName}\"";
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
                label.Text = nameColumns[i]; // Уже переведенные названия
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
                labelAppend.Text = nameColumns[i]; // Уже переведенные названия
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

            //saveToDBButton = new Button();
            //saveToDBButton.Size = new Size(180, 30);
            //saveToDBButton.Text = "Сохранить в БД";
            //saveToDBButton.Location = new Point(saveButtonX + 300, 450);
            //saveToDBButton.Font = new Font("Arial", 9);
            //saveToDBButton.BackColor = Color.LightGreen;
            //saveToDBButton.FlatStyle = FlatStyle.Flat;
            //parentForm.Controls.Add(saveToDBButton);

            reloadButton = new Button();
            reloadButton.Size = new Size(180, 30);
            reloadButton.Text = "Перезагрузить";
            reloadButton.Location = new Point(saveButtonX + 500, startY - 20);
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
            //saveToDBButton.Click += SaveToDBButton_Click;
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
            bool validationFailed = false;
            string errorMessage = "";
            // Проверяем данные перед добавлением
            for (int i = 0; i < column; i++)
            {
                string inputValue = textBoxesAppend[i].Text;
                rowData[i] = inputValue;

                if (!string.IsNullOrWhiteSpace(inputValue))
                    hasData = true;

                string columnName = nameColumns[i];
                if (columnDataTypes.ContainsKey(columnName))
                {
                    string dataType = columnDataTypes[columnName];

                    if (!ValidateNumericInput(inputValue, dataType))
                    {
                        validationFailed = true;
                        errorMessage += $"Столбец '{columnName}': ожидается числовое значение\n";
                    }
                }
            }

            if (!hasData)
            {
                MessageBox.Show("Введите данные для добавления");
                return;
            }

            if (validationFailed)
            {
                MessageBox.Show($"Ошибка проверки данных:\n{errorMessage}",
                               "Некорректные данные",
                               MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Если все проверки пройдены, добавляем запись
            gridView.AddRows(false, rowData);

            // Очищаем поля ввода после успешного добавления
            for (int i = 0; i < column; i++)
            {
                textBoxesAppend[i].Clear();
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

        //private void SaveToDBButton_Click(object sender, EventArgs e)
        //{
        //    if (gridView.GetRowCount() == 0)
        //    {
        //        MessageBox.Show("Нет данных для сохранения. Таблица пустая.", "Предупреждение",
        //                       MessageBoxButtons.OK, MessageBoxIcon.Warning);
        //        return;
        //    }

        //    gridView.SaveDataToDatabase(nameTable, parentForm.connectionString);
        //}

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

            // Проверяем данные в фильтрах
            bool validationFailed = false;
            string errorMessage = "";

            for (int i = 0; i < textBoxesFilter.Count; i++)
            {
                string filterValue = textBoxesFilter[i].Text;

                if (!string.IsNullOrEmpty(filterValue))
                {
                    // Проверяем корректность данных для числовых полей в фильтрах
                    string columnName = nameColumns[i];
                    if (columnDataTypes.ContainsKey(columnName))
                    {
                        string dataType = columnDataTypes[columnName];

                        if (!ValidateNumericInput(filterValue, dataType))
                        {
                            validationFailed = true;
                            errorMessage += $"Фильтр в столбце '{columnName}': ожидается числовое значение\n";
                            continue; // Пропускаем этот фильтр
                        }
                    }

                    dataFilter.Add(nameColumns[i]);
                    filterValues.Add(filterValue);
                }
            }

            if (validationFailed)
            {
                MessageBox.Show($"Ошибка проверки данных в фильтрах:\n{errorMessage}",
                               "Некорректные данные в фильтрах",
                               MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            int visibleRows = 0;

            foreach (DataGridViewRow row in gridView.dataGrid.Rows)
            {
                if (row.IsNewRow) continue; // Пропускаем новую строку

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
                if (shouldShow) visibleRows++;
            }

            // Проверяем, остались ли видимые строки после фильтрации
            if (visibleRows == 0)
            {
                MessageBox.Show("После применения фильтра таблица пуста. Попробуйте изменить условия фильтрации.",
                               "Результаты фильтрации",
                               MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show($"Фильтр применен. Найдено записей: {visibleRows}");
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
            //saveToDBButton.Visible = false;
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
            //saveToDBButton.Visible = true;
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
