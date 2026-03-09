using System;
using System.Collections.Generic;
using System.Diagnostics; // Для Process (запуск Python)
using System.IO; // Для работы с файлами и папками
using System.Linq; // Для LINQ (FirstOrDefault)
using Newtonsoft.Json; // Для сериализации в JSON
using OSTUVision.Models; // Для CustomObject

namespace OSTUVision.Services // Пространство имен для сервисов
{
    public class TrainingService
    {
        private readonly string _customDataFolder; // Папка для хранения пользовательских данных (из конфига)
        private readonly string _dbPath; // Путь к JSON-файлу с описанием объектов (из конфига)
        private List<CustomObject> _customObjects = new(); // Список пользовательских объектов в памяти

        public TrainingService(string customDataFolder, string dbPath)
        {
            _customDataFolder = customDataFolder;
            _dbPath = dbPath;
            LoadObjects(); // Загружаем существующие объекты из JSON
        }

        private void LoadObjects()
        {
            if (File.Exists(_dbPath))
            {
                string json = File.ReadAllText(_dbPath);
                _customObjects = JsonConvert.DeserializeObject<List<CustomObject>>(json) ?? new List<CustomObject>(); // Десериализуем JSON в список CustomObject
            }
        }

        private void SaveObjects()
        {
            string json = JsonConvert.SerializeObject(_customObjects, Formatting.Indented); // Сериализуем в JSON с отступами (читаемый формат)
            File.WriteAllText(_dbPath, json);
        }

        public void AddCustomObject(string name, string imagePath)
        {
            if (string.IsNullOrEmpty(name) || !File.Exists(imagePath)) // Проверка входных данных
                return;

            string destDir = Path.Combine(_customDataFolder, "train", name);  // Создаем папку для этого объекта: CustomData/train/НазваниеОбъекта/
            Directory.CreateDirectory(destDir);
            
            string destFile = Path.Combine(destDir, Guid.NewGuid() + Path.GetExtension(imagePath)); // Генерируем уникальное имя файла с помощью GUID
            File.Copy(imagePath, destFile);

            var obj = _customObjects.FirstOrDefault(o => o.Name == name);  // Ищем существующий объект с таким именем
            if (obj == null)  //Если такого объекта еще нет
            {
                obj = new CustomObject { Name = name }; // Создаем новый
                _customObjects.Add(obj);  // Добавляем в список
            }
            obj.ImagePaths.Add(destFile);  // Добавляем путь к файлу в список изображений объекта
            
            SaveObjects();
            Logger.Log($"Добавлено изображение для {name}");
        }

        public void RemoveCustomObject(string name)
        {
            var obj = _customObjects.FirstOrDefault(o => o.Name == name);
            if (obj != null)
            {
                string dir = Path.Combine(_customDataFolder, "train", name);
                if (Directory.Exists(dir))
                    Directory.Delete(dir, true);
                
                _customObjects.Remove(obj);
                SaveObjects();  // Сохраняем обновленный список в JSON
            }
        }

        public List<CustomObject> GetAllObjects() => _customObjects;

        public void StartRetraining(Action<string>? onProgress, Action<bool>? onComplete)
        {
            try
            {
                string scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "train_yolo26.py");
                string dataYaml = Path.Combine(_customDataFolder, "dataset.yaml");

                if (!File.Exists(scriptPath))
                {
                    onProgress?.Invoke("❌ Не найден train_yolo26.py");
                    onComplete?.Invoke(false);
                    return;
                }

                CreateDatasetYaml(dataYaml); // Создаем YAML-файл с описанием датасета

                ProcessStartInfo psi = new ProcessStartInfo // Настройка запуска Python-процесса
                {
                    FileName = "python", // Исполняемый файл Python
                    Arguments = $"\"{scriptPath}\" --data \"{dataYaml}\" --epochs 100 --model n",
                    UseShellExecute = false, // Не использовать shell
                    RedirectStandardOutput = true, // Перехватывать stdout
                    RedirectStandardError = true, // Перехватывать stderr
                    CreateNoWindow = true, // Не показывать окно консоли
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
                };

                onProgress?.Invoke("🚀 Запуск обучения YOLO26...");
                
                using var process = new Process { StartInfo = psi };  // Запускаем процесс
                process.OutputDataReceived += (s, e) => onProgress?.Invoke(e.Data ?? "");
                process.ErrorDataReceived += (s, e) => onProgress?.Invoke($"ERR: {e.Data}");
                
                process.Start();
                process.BeginOutputReadLine(); // Начинаем асинхронное чтение вывода
                process.BeginErrorReadLine();// Начинаем асинхронное чтение ошибок
                process.WaitForExit(); // Ждем завершения процесса

                onComplete?.Invoke(process.ExitCode == 0); // Вызываем callback с результатом (код 0 = успех)
            }
            catch (Exception ex)
            {
                Logger.LogError($"Ошибка обучения: {ex.Message}");
                onComplete?.Invoke(false);
            }
        }

        private void CreateDatasetYaml(string path)
        {
            var allClasses = new List<string>();
            
            // Собираем классы из train папки
            string trainDir = Path.Combine(_customDataFolder, "train");  // Собираем все классы из папки train
            if (Directory.Exists(trainDir))
            {
                foreach (var dir in Directory.GetDirectories(trainDir)) // Каждая подпапка = один класс
                {
                    allClasses.Add(Path.GetFileName(dir));
                }
            }

            using var sw = new StreamWriter(path);  // Создаем YAML-файл в формате для YOLO
            sw.WriteLine($"path: {Path.GetFullPath(_customDataFolder).Replace("\\", "/")}"); // Корневой путь к данным (в UNIX-стиле с прямыми слешами)
            sw.WriteLine("train: train"); // Подпапка с обучающими данными
            sw.WriteLine("val: valid"); // Подпапка с валидационными данными
            sw.WriteLine($"nc: {allClasses.Count}");
            sw.Write("names: [");
            for (int i = 0; i < allClasses.Count; i++)
            {
                sw.Write($"'{allClasses[i]}'");
                if (i < allClasses.Count - 1) sw.Write(", ");
            }
            sw.WriteLine("]");
        }
    }
}