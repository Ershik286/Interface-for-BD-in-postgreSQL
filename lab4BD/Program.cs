using Microsoft.VisualBasic;
using System;
using System.IO;
using System.Windows.Forms;

namespace lab4BD
{
    public static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        public static Form1 mainForm;
        public static bool firstLaunch = true;
        public const string pathFirstRun = "NoFirstStart.txt";

        // Параметры подключения
        public static string DatabaseName = "lab1";
        public static string UserLogin = "postgres";
        public static string UserPassword = "postgres";
        public static string Server = "localhost";
        public static string Port = "5432";

        [STAThread]
        public static void Main()
        {
            ApplicationConfiguration.Initialize();

            if (!File.Exists(pathFirstRun))
            {
                inputKeyDB();
            }
            else
            {
                firstLaunch = false;
            }

            mainForm = new Form1();
            Application.Run(mainForm);
        }

        public static string ShowInputBox(string prompt, string title = "", string defaultValue = "")
        {
            return Interaction.InputBox(prompt, title, defaultValue);
        }

        public static string GetConnectionString()
        {
            return $"Server={Server};Port={Port};User Id={UserLogin};Password={UserPassword};Database={DatabaseName};";
        }


        public static void inputKeyDB() {
            try {
                Server = ShowInputBox("Введите адрес сервера:", "Настройка подключения", "localhost");
                Port = ShowInputBox("Введите порт:", "Настройка подключения", "5432");
                DatabaseName = ShowInputBox("Введите название БД:", "Настройка подключения", "lab1");
                UserLogin = ShowInputBox("Введите логин:", "Настройка подключения", "postgres");
                UserPassword = ShowInputBox("Введите пароль:", "Настройка подключения", "");

                File.WriteAllText(pathFirstRun, "first_launch_completed");
                firstLaunch = false;

                MessageBox.Show("Настройки подключения сохранены!", "Успех",
                              MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) {
                MessageBox.Show($"Ошибка при сохранении настроек: {ex.Message}\nБудут использованы настройки по умолчанию.",
                              "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            mainForm = new Form1();
            Application.Run(mainForm);
        }
    }
}
