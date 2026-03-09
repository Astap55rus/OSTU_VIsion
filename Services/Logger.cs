using System;
using System.IO;

namespace OSTUVision.Services // Пространство имен для сервисов
{
    public static class Logger
    {
        private static readonly string LogPath = "log.txt"; // Путь к файлу лога (в папке с программой)
        private static readonly object LockObj = new object(); // Объект для синхронизации потоков (чтобы лог не "перемешивался")

        public static void Log(string message)
        {
            try
            {
                lock (LockObj)
                {
                    string entry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}";
                    File.AppendAllText(LogPath, entry + Environment.NewLine);  // Добавляем запись в конец файла (если файла нет - создается)
                    Console.WriteLine(entry); // Дублируем в консоль (для отладки
                }
            }
            catch { } // Игнорируем ошибки логирования (чтобы программа не падала)
        }

        public static void LogError(string error)
        {
            Log($"❌ ERROR: {error}"); // Переиспользуем основной метод, добавляя эмодзи и пометку ERROR
        }
    }
}