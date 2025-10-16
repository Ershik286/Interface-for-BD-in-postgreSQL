using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Npgsql;
using System.Data;

namespace lab4BD
{
    public partial class Form1 : Form
    {
        public string connectionString => Program.GetConnectionString();

        private List<Table> tables = new List<Table>();
        private MenuStrip mainMenu;

        public Form1()
        {
            InitializeComponent();
            this.Size = new Size(1700, 700);
            this.Text = $"База данных: {Program.DatabaseName}@ {Program.Server}:{Program.Port}";

            ConnectAndLoadDatabase();
        }

        private async void ConnectAndLoadDatabase()
        {
            try
            {
                ShowConnectionInfo();

                await ConnectToDatabase();

                LoadTablesFromDatabase();
                CreateMenuStrip();

                this.Invalidate();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка подключения к БД: {ex.Message}\n\n" +
                               $"Проверьте правильность введенных данных:\n" +
                               $"Сервер: {Program.Server}:{Program.Port}\n" +
                               $"База данных: {Program.DatabaseName}\n" +
                               $"Пользователь: {Program.UserLogin}",
                               "Ошибка подключения",
                               MessageBoxButtons.OK, MessageBoxIcon.Error);

                // Предлагаем изменить настройки подключения
                var result = MessageBox.Show("Хотите изменить настройки подключения?",
                                           "Настройки подключения",
                                           MessageBoxButtons.YesNo,
                                           MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    ResetConnectionSettings();
                }
                else
                {
                    CreateTestTable();
                    CreateMenuStrip();
                }
            }
        }

        private void ShowConnectionInfo()
        {
            // Можно добавить статус бар или лейбл для отображения информации о подключении
            ToolStripStatusLabel statusLabel = new ToolStripStatusLabel();
            statusLabel.Text = $"Подключение: {Program.Server}:{Program.Port} | БД: {Program.DatabaseName} | Пользователь: {Program.UserLogin}";

            StatusStrip statusStrip = new StatusStrip();
            statusStrip.Items.Add(statusLabel);
            statusStrip.Dock = DockStyle.Bottom;
            this.Controls.Add(statusStrip);
        }
        private void ResetConnectionSettings()
        {
            try
            {
                // Удаляем файл настроек
                if (File.Exists(Program.pathFirstRun))
                {
                    File.Delete(Program.pathFirstRun);
                }

                // Перезапускаем приложение
                Application.Restart();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сбросе настроек: {ex.Message}", "Ошибка",
                              MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        private async Task ConnectToDatabase()
        {
            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    // Проверяем подключение
                    using (var command = new NpgsqlCommand("SELECT 1", connection))
                    {
                        await command.ExecuteScalarAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Не удалось подключиться к базе данных: {ex.Message}");
            }
        }

        private void LoadTablesFromDatabase()
        {
            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();

                    // Получаем список всех таблиц в базе данных
                    string sql = @"
                        SELECT table_name 
                        FROM information_schema.tables 
                        WHERE table_schema = 'public' 
                        AND table_type = 'BASE TABLE'
                        ORDER BY table_name";

                    using (var command = new NpgsqlCommand(sql, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string tableName = reader.GetString(0);
                            //MessageBox.Show($"Найдена таблица: {tableName}"); // Отладочное сообщение

                            // Создаем объект Table для каждой таблицы
                            Table table = new Table(tableName, this);
                            tables.Add(table);

                            // Загружаем данные из таблицы
                            LoadTableData(table);
                        }
                    }

                    // Если таблиц не найдено, создаем тестовую
                    if (tables.Count == 0)
                    {
                        MessageBox.Show("Таблиц в базе данных не найдено. Создайте новую таблицу.");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки таблиц из БД: {ex.Message}");
                throw;
            }
        }

        private void LoadTableData(Table table)
        {
            try
            {
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    connection.Open();

                    // Получаем структуру таблицы (столбцы)
                    string columnsSql = $@"
                        SELECT column_name, data_type 
                        FROM information_schema.columns 
                        WHERE table_name = '{table.nameTable}' 
                        ORDER BY ordinal_position";

                    List<string> columnNames = new List<string>();
                    using (var command = new NpgsqlCommand(columnsSql, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            columnNames.Add(reader.GetString(0));
                        }
                    }

                    // Получаем данные из таблицы
                    string dataSql = $"SELECT * FROM {table.nameTable}";
                    using (var command = new NpgsqlCommand(dataSql, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string[] rowData = new string[columnNames.Count];
                            for (int i = 0; i < columnNames.Count; i++)
                            {
                                rowData[i] = reader.IsDBNull(i) ? "" : reader.GetValue(i).ToString();
                            }
                            table.insert(rowData);
                        }
                    }

                    //MessageBox.Show($"Загружено данных из таблицы {table.nameTable}: {table.GetRowCount()} строк"); // Отладочное сообщение
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных таблицы {table.nameTable}: {ex.Message}");
            }
        }

        private void CreateTestTable()
        {
            // Создаем тестовую таблицу если нет подключения к БД
            Table testTable = new Table("test_table", this);
            testTable.insert(new string[] { "Один", "два", "три", "четыре", "пять" });
            tables.Add(testTable);
            MessageBox.Show("Создана тестовая таблица"); // Отладочное сообщение
        }

        private void CreateMenuStrip()
        {
            // Создаем главное меню
            mainMenu = new MenuStrip();
            mainMenu.Dock = DockStyle.Top;

            // Пункт меню "Файл"
            ToolStripMenuItem fileMenu = new ToolStripMenuItem("Файл");
            ToolStripMenuItem refreshItem = new ToolStripMenuItem("Обновить");
            ToolStripMenuItem exitItem = new ToolStripMenuItem("Выход");

            refreshItem.Click += (s, e) => RefreshTables();
            exitItem.Click += (s, e) => Application.Exit();

            fileMenu.DropDownItems.AddRange(new ToolStripItem[] {
                refreshItem, new ToolStripSeparator(), exitItem
            });

            // Пункт меню "Таблицы"
            ToolStripMenuItem tablesMenu = new ToolStripMenuItem("Таблицы");

            // Динамически создаем пункты меню для каждой таблицы
            foreach (Table table in tables)
            {
                ToolStripMenuItem tableItem = new ToolStripMenuItem(table.nameTable);
                tableItem.Click += (s, e) => ShowTable(table);
                tablesMenu.DropDownItems.Add(tableItem);
                //MessageBox.Show($"Добавлен пункт меню для таблицы: {table.nameTable}"); // Отладочное сообщение
            }

            // Если таблиц нет, добавляем заглушку
            if (tables.Count == 0)
            {
                ToolStripMenuItem noTablesItem = new ToolStripMenuItem("Нет таблиц");
                noTablesItem.Enabled = false;
                tablesMenu.DropDownItems.Add(noTablesItem);
                //MessageBox.Show("Добавлен пункт 'Нет таблиц'"); // Отладочное сообщение
            }

            // Пункт меню "Действия"
            ToolStripMenuItem actionsMenu = new ToolStripMenuItem("Действия");
            ToolStripMenuItem createTableItem = new ToolStripMenuItem("Создать таблицу");
            ToolStripMenuItem deleteTableItem = new ToolStripMenuItem("Удалить таблицу");

            createTableItem.Click += (s, e) => CreateNewTable();
            deleteTableItem.Click += (s, e) => DeleteTable();

            actionsMenu.DropDownItems.AddRange(new ToolStripItem[] {
                createTableItem, deleteTableItem
            });

            // Добавляем все пункты в главное меню
            mainMenu.Items.AddRange(new ToolStripItem[] {
                fileMenu, tablesMenu, actionsMenu
            });

            this.Controls.Add(mainMenu);
            this.MainMenuStrip = mainMenu;

            //MessageBox.Show($"Создано меню с {tables.Count} таблицами"); // Отладочное сообщение

            // Показываем первую таблицу по умолчанию
            if (tables.Count > 0)
            {
                ShowTable(tables[0]);
            }
        }

        private void ShowTable(Table table)
        {
            // Скрываем все таблицы
            foreach (Table t in tables)
            {
                t.Hide();
            }

            // Показываем выбранную таблицу
            table.Show();
            //MessageBox.Show($"Показана таблица: {table.nameTable}"); // Отладочное сообщение
        }

        private async void RefreshTables()
        {
            try
            {
                // Скрываем и удаляем все текущие таблицы
                foreach (Table table in tables)
                {
                    table.Hide();
                    table.Dispose();
                }
                tables.Clear();

                // Удаляем старое меню
                if (mainMenu != null)
                {
                    this.Controls.Remove(mainMenu);
                    mainMenu.Dispose();
                }

                // Переподключаемся к БД и загружаем таблицы
                await ConnectToDatabase();
                LoadTablesFromDatabase();
                CreateMenuStrip();
                this.Invalidate();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка обновления: {ex.Message}");
            }
        }

        private async void CreateNewTable()
        {
            string tableName = Microsoft.VisualBasic.Interaction.InputBox(
                "Введите название новой таблицы:", "Создание таблицы", "new_table");

            if (!string.IsNullOrEmpty(tableName))
            {
                try
                {
                    using (var connection = new NpgsqlConnection(connectionString))
                    {
                        await connection.OpenAsync();

                        // Создаем таблицу с несколькими колонками
                        string createSql = $@"
                            CREATE TABLE {tableName} (
                                id SERIAL PRIMARY KEY,
                                name TEXT,
                                description TEXT,
                                value NUMERIC
                            )";

                        using (var command = new NpgsqlCommand(createSql, connection))
                        {
                            await command.ExecuteNonQueryAsync();
                        }

                        // Добавляем тестовые данные
                        string insertSql = $@"
                            INSERT INTO {tableName} (name, description, value) VALUES 
                            ('Тестовая запись 1', 'Описание 1', 100.50),
                            ('Тестовая запись 2', 'Описание 2', 200.75)";

                        using (var command = new NpgsqlCommand(insertSql, connection))
                        {
                            await command.ExecuteNonQueryAsync();
                        }
                    }

                    RefreshTables();
                    MessageBox.Show($"Таблица {tableName} создана успешно!");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка создания таблицы: {ex.Message}");
                }
            }
        }

        private async void DeleteTable()
        {
            if (tables.Count == 0)
            {
                MessageBox.Show("Нет таблиц для удаления");
                return;
            }

            // Показываем список доступных таблиц
            string tableList = "Доступные таблицы:\n";
            foreach (Table table in tables)
            {
                tableList += $"- {table.nameTable}\n";
            }

            string tableName = Microsoft.VisualBasic.Interaction.InputBox(
                $"{tableList}\nВведите название таблицы для удаления:", "Удаление таблицы", "");

            if (!string.IsNullOrEmpty(tableName))
            {
                try
                {
                    using (var connection = new NpgsqlConnection(connectionString))
                    {
                        await connection.OpenAsync();

                        string deleteSql = $"DROP TABLE IF EXISTS {tableName}";

                        using (var command = new NpgsqlCommand(deleteSql, connection))
                        {
                            await command.ExecuteNonQueryAsync();
                        }
                    }

                    RefreshTables();
                    MessageBox.Show($"Таблица {tableName} удалена успешно!");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка удаления таблицы: {ex.Message}");
                }
            }
        }
    }
}