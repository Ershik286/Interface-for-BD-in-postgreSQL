using Microsoft.VisualBasic;
using System;
using System.IO;
using System.Windows.Forms;
using System.Text.Json;
using System.Text;

namespace lab4BD {
    public static class Program {
        public static Form1 mainForm;
        public static bool firstLaunch = true;

        // Параметры подключения
        public static string DatabaseName = "lab1";
        public static string UserLogin = "postgres";
        public static string UserPassword = "postgres";
        public static string Server = "localhost";
        public static string Port = "5432";

        public static string pathFirstRun = "app_settings.config";

        public static void Main() {
            ApplicationConfiguration.Initialize();

            if (!File.Exists(pathFirstRun)) {
                try {
                    Server = ShowInputBox("Введите адрес сервера:", "Настройка подключения", "localhost");
                    Port = ShowInputBox("Введите порт:", "Настройка подключения", "5432");
                    DatabaseName = ShowInputBox("Введите название БД:", "Настройка подключения", "lab1");
                    UserLogin = ShowInputBox("Введите логин:", "Настройка подключения", "postgres");
                    UserPassword = ShowInputBox("Введите пароль:", "Настройка подключения", "");

                    SaveSettings();
                    firstLaunch = false;

                    MessageBox.Show("Настройки подключения сохранены!", "Успех",
                                  MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex) {
                    MessageBox.Show($"Ошибка при сохранении настроек: {ex.Message}\nБудут использованы настройки по умолчанию.",
                                  "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            else {
                LoadSettings();
                firstLaunch = false;
            }

            mainForm = new Form1();
            Application.Run(mainForm);
        }

        private static void SaveSettings() {
            try {
                var settings = new StringBuilder();
                settings.AppendLine($"Server={Server}");
                settings.AppendLine($"Port={Port}");
                settings.AppendLine($"DatabaseName={DatabaseName}");
                settings.AppendLine($"UserLogin={UserLogin}");
                settings.AppendLine($"UserPassword={UserPassword}");

                File.WriteAllText(pathFirstRun, settings.ToString(), Encoding.UTF8);
            }
            catch (Exception ex) {
                MessageBox.Show($"Ошибка сохранения настроек: {ex.Message}", "Ошибка",
                              MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static void LoadSettings() {
            try {
                if (File.Exists(pathFirstRun)) {
                    var lines = File.ReadAllLines(pathFirstRun, Encoding.UTF8);
                    foreach (var line in lines) {
                        var parts = line.Split('=');
                        if (parts.Length == 2) {
                            string key = parts[0].Trim();
                            string value = parts[1].Trim();

                            switch (key) {
                                case "Server": Server = value; break;
                                case "Port": Port = value; break;
                                case "DatabaseName": DatabaseName = value; break;
                                case "UserLogin": UserLogin = value; break;
                                case "UserPassword": UserPassword = value; break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) {
                MessageBox.Show($"Ошибка загрузки настроек: {ex.Message}\nБудут использованы настройки по умолчанию.",
                              "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        public static string ShowInputBox(string prompt, string title = "", string defaultValue = "") {
            return Interaction.InputBox(prompt, title, defaultValue);
        }

        public static string GetConnectionString() {
            return $"Server={Server};Port={Port};User Id={UserLogin};Password={UserPassword};Database={DatabaseName};";
        }

        public static void InputKeyDB(Form1 mainForm) {
            try {
                Server = ShowInputBox("Введите адрес сервера:", "Настройка подключения", Server);
                Port = ShowInputBox("Введите порт:", "Настройка подключения", Port);
                DatabaseName = ShowInputBox("Введите название БД:", "Настройка подключения", DatabaseName);
                UserLogin = ShowInputBox("Введите логин:", "Настройка подключения", UserLogin);
                UserPassword = ShowInputBox("Введите пароль:", "Настройка подключения", UserPassword);

                SaveSettings();
                firstLaunch = false;

                MessageBox.Show("Настройки подключения сохранены!", "Успех",
                              MessageBoxButtons.OK, MessageBoxIcon.Information);

                mainForm.RefreshDatabaseConnection();
            }
            catch (Exception ex) {
                MessageBox.Show($"Ошибка при сохранении настроек: {ex.Message}",
                              "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
