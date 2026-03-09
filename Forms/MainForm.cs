// Подключаем библиотеки 
using System; // Библиотеки базовых типов (int, string, Exception)
using System.Collections.Generic;  // Списки, коллекции (List<T>)
using System.Drawing;// библиотека Графики (Bitmap, Color, Rectangle)
using System.IO;// Библиотека работа с файлами (File, Path)
using System.Linq; // Работа с коллекциями (Where, Contains)
using System.Threading.Tasks;// Асинхронность (async/await)
using System.Windows.Forms;// Элементы интерфейса (Form, Button)
using Newtonsoft.Json; // Работа с JSON (сериализация)
using Newtonsoft.Json.Linq;// Работа с JSON объектами (JObject)
using OSTUVision.Models;// Модели данных (DetectionResult)
using OSTUVision.Services; // Сервисы (IDetector, SpeechService)
using OSTUVision.Helpers;// Вспомогательные утилиты (ImageUtils)
using OpenCvSharp; // Компьютерное зрение (VideoCapture, Mat)
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;
namespace OSTUVision
{
    public partial class MainForm : Form
    {
        //Блок элементов управления 
        private TabControl tabControl = null!; // Контейнер для всех вкладок
        private TabPage tabUpload = null!;  // Вкладка 1: Загрузка материалов
        private TabPage tabTypes = null!;  // Вкладка 2: Типы светофоров
        private TabPage tabTraining = null!; // Вкладка 3: Обучение модели
        private TabPage tabAbout = null!; // Вкладка 4: О программе

        // Вкладка "Загрузка материалов"
        private Button btnLoadFile = null!; // Кнопка "Загрузить фото"
        private Button btnLoadVideo = null!;// Кнопка "Загрузить видео"
        private Button btnAnalyze = null!; // Кнопка "Анализировать"
        private Button btnSaveResult = null!; // Кнопка "Сохранить результат"
        private TextBox txtFilePath = null!; // Поле с путем к файлу
        private ProgressBar progressBar = null!; // Поле с путем к файлу
        private Label statusLabel = null!; // Поле с путем к файлу
        private PictureBox pictureBoxOriginal = null!; // Оригинальное изображение левое окно 
        private PictureBox pictureBoxResult = null!; // Результат с рамками правое окно
        private ListBox listBoxFiles = null!;   // Список выбранных файлов

        // Вкладка "Типы светофоров" (информационные сведения о типах светофорах)
        private TextBox txtSearchType = null!; // Поле поиска по типам
        private DataGridView dgvTrafficLights = null!; // Таблица со светофорами
        private DataGridViewImageColumn imageColumn = null!; // Колонка для фото

        // Вкладка "Обучение модели"
        private TextBox txtCustomName = null!; // Название нового объекта
        private Button btnAddCustomObject = null!; // Кнопка добавления объекта
        private Button btnRetrain = null!; // Кнопка переобучения
        private ListBox listBoxCustomObjects = null!; // Список добавленных объектов
        private Button btnRemoveCustomObject = null!; // Кнопка удаления объекта
        private RichTextBox txtTrainingLog = null!; // Лог обучения
        private Button btnUseTemporaryDetector = null!; // Кнопка тестового режима
        private ProgressBar progressBarAdd = null!;  // Прогресс добавления фото

        // Сервисы
        private JObject _config = null!; // Конфигурация из appsettings.json
        private IDetector _detector = null!; // Детектор (YOLO или тестовый)
        private SpeechService _speech = null!; // Сервис озвучивания.
        private TrainingService _training = null!; // Сервис обучения
        private List<string> _selectedFiles = new(); // Выбранные файлы для анализа
        private bool _useTemporaryDetector = false; // Флаг тестового режима

        // Данные о светофорах
        private List<TrafficLightInfo> _trafficLights = new List<TrafficLightInfo>();

        public MainForm()
        {
            try
            {
                LoadConfig();
                InitializeComponent();
                LoadApplicationIcon();
                InitServices();
                LoadTrafficLightData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при создании формы: {ex.Message}\n\nСтек: {ex.StackTrace}",
                    "Критическая ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                throw;
            }
        }

        private void LoadApplicationIcon()
        {
            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "railway.ico");
                if (File.Exists(iconPath))
                {
                    this.Icon = new Icon(iconPath);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Ошибка загрузки иконки: {ex.Message}");
            }
        }

        private void InitializeComponent()
        {
            this.Text = "OSTU Vision  - Анализ железнодорожной обстановки"; // Заголовок
            this.Size = new Size(1920, 1080);  // Размер окна
            this.StartPosition = FormStartPosition.CenterScreen;// Размещение по центру экрана

            tabControl = new TabControl { Dock = DockStyle.Fill };
            this.Controls.Add(tabControl); // Добавляем вкладки на форму

            tabUpload = new TabPage("1. Загрузка материалов");
            CreateUploadTab(); // Наполняем вкладку элементами
            tabControl.TabPages.Add(tabUpload);

            tabTypes = new TabPage("2. Типы светофоров");
            CreateTypesTab(); // Наполняем вкладку элементами
            tabControl.TabPages.Add(tabTypes);

            tabTraining = new TabPage("3. Обучение модели");
            CreateTrainingTab(); // Наполняем вкладку элементами
            tabControl.TabPages.Add(tabTraining);

            tabAbout = new TabPage("4. О программе");
            CreateAboutTab(); // Наполняем вкладку элементами
            tabControl.TabPages.Add(tabAbout);
        }

        private void CreateUploadTab()
        {
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 8 };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));

            var buttonsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                Height = 40,
                Padding = new Padding(0)
            };

            btnLoadFile = new Button 
            { 
                Text = "📷 Загрузить фото", 
                Size = new Size(140, 35),
                Margin = new Padding(0, 0, 5, 0),
                BackColor = Color.FromArgb(240, 240, 255)
            };
            btnLoadFile.Click += BtnLoadFile_Click!;
            buttonsPanel.Controls.Add(btnLoadFile);

            btnLoadVideo = new Button 
            { 
                Text = "🎬 Загрузить видео", 
                Size = new Size(140, 35),
                BackColor = Color.FromArgb(255, 240, 240)
            };
            btnLoadVideo.Click += BtnLoadVideo_Click!;
            buttonsPanel.Controls.Add(btnLoadVideo);

            panel.Controls.Add(buttonsPanel, 0, 0);
            panel.SetColumnSpan(buttonsPanel, 2);

            txtFilePath = new TextBox 
            { 
                ReadOnly = true, 
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Text = "Выберите фото или видео для анализа"
            };
            panel.Controls.Add(txtFilePath, 0, 1);
            panel.SetColumnSpan(txtFilePath, 2);

            btnAnalyze = new Button 
            { 
                Text = "🔍 Анализировать", 
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Height = 35,
                BackColor = Color.FromArgb(220, 255, 220)
            };
            btnAnalyze.Click += BtnAnalyze_Click!;
            panel.Controls.Add(btnAnalyze, 0, 2);
            panel.SetColumnSpan(btnAnalyze, 2);

            progressBar = new ProgressBar { Anchor = AnchorStyles.Left | AnchorStyles.Right };
            panel.Controls.Add(progressBar, 0, 3);
            panel.SetColumnSpan(progressBar, 2);

            statusLabel = new Label { Text = "Готов", Anchor = AnchorStyles.Left };
            panel.Controls.Add(statusLabel, 0, 4);
            panel.SetColumnSpan(statusLabel, 2);

            listBoxFiles = new ListBox 
            { 
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom, 
                Height = 80 
            };
            panel.Controls.Add(listBoxFiles, 0, 5);
            panel.SetColumnSpan(listBoxFiles, 2);

            var split = new SplitContainer { Dock = DockStyle.Fill };
            pictureBoxOriginal = new PictureBox { SizeMode = PictureBoxSizeMode.Zoom, Dock = DockStyle.Fill };
            pictureBoxResult = new PictureBox { SizeMode = PictureBoxSizeMode.Zoom, Dock = DockStyle.Fill };
            split.Panel1.Controls.Add(pictureBoxOriginal);
            split.Panel2.Controls.Add(pictureBoxResult);
            panel.Controls.Add(split, 0, 6);
            panel.SetColumnSpan(split, 2);

            btnSaveResult = new Button 
            { 
                Text = "💾 Сохранить результат", 
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Height = 35
            };
            btnSaveResult.Click += BtnSaveResult_Click!;
            panel.Controls.Add(btnSaveResult, 0, 7);
            panel.SetColumnSpan(btnSaveResult, 2);

            tabUpload.Controls.Add(panel);
        }

        // Обновленная вкладка типы светофоров 
        private void CreateTypesTab()
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(10)
            };
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); // Панель поиска
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Таблица

            // Верхняя панель с поиском
            var searchPanel = new Panel { Dock = DockStyle.Fill, Height = 40 };
            var lblSearch = new Label
            {
                // Задание положения, типа шрифта и его размера.
                Text = "Поиск:",
                Location = new Point(5, 10),
                AutoSize = true,
                Font = new Font("Times New Roman", 12, FontStyle.Bold)
            };
            txtSearchType = new TextBox
            {
                Location = new Point(70, 8),
                Width = 300,
                Font = new Font("Times New Roman", 10)
            };
            txtSearchType.TextChanged += TxtSearchType_TextChanged!;
            searchPanel.Controls.Add(lblSearch);
            searchPanel.Controls.Add(txtSearchType);
            panel.Controls.Add(searchPanel, 0, 0);

            // DataGridView для отображения светофоров
            dgvTrafficLights = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                Font = new Font("Times New Roman", 12),
                DefaultCellStyle = { 
                    WrapMode = DataGridViewTriState.True,  // Включаем перенос текста
                    Alignment = DataGridViewContentAlignment.TopLeft
                },
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells, // Автоматическая высота строк
                ColumnHeadersHeight = 40
            };

            // Колонка "Тип и название" - 20% ширины
            var typeNameColumn = new DataGridViewTextBoxColumn
            {
                Name = "TypeName",
                HeaderText = "Тип и название",
                FillWeight = 20, // 20% ширины
                MinimumWidth = 150,
                DefaultCellStyle = { 
                    Font = new Font("Times New Roman", 12, FontStyle.Bold),
                    ForeColor = Color.FromArgb(0, 51, 102)
                }
            };

            // Колонка "Описание" - 50% ширины с переносом текста
            var descColumn = new DataGridViewTextBoxColumn
            {
                Name = "Description",
                HeaderText = "Описание",
                FillWeight = 50, // 50% ширины
                MinimumWidth = 300,
                DefaultCellStyle = { 
                    WrapMode = DataGridViewTriState.True,
                    Alignment = DataGridViewContentAlignment.TopLeft
                }
            };

            // Колонка "Фото" - 30% ширины
            imageColumn = new DataGridViewImageColumn
            {
                Name = "Image",
                HeaderText = "Фото",
                ImageLayout = DataGridViewImageCellLayout.Zoom,
                FillWeight = 30, // 30% ширины
                MinimumWidth = 200
            };

            dgvTrafficLights.Columns.AddRange(new DataGridViewColumn[] { 
                typeNameColumn, descColumn, imageColumn 
            });

            dgvTrafficLights.SelectionChanged += DgvTrafficLights_SelectionChanged!;

            panel.Controls.Add(dgvTrafficLights, 0, 1);
            tabTypes.Controls.Add(panel);
        }

        private void LoadTrafficLightData()
        {
            _trafficLights = new List<TrafficLightInfo>
            {
                new TrafficLightInfo
                {
                    // Блок для описание 13 типов светофоров
                    Type = "Тип 1",
                    ShortName = "Входной мачтовый",
                    FullDescription = "ВХОДНОЙ МАЧТОВЫЙ СВЕТОФОР\n─────────────────────────\n\n" +
                                     "Назначение: Разрешает или запрещает поезду следовать с перегона на станцию.\n" +
                                     "Устанавливается перед входной стрелкой станции.\n\n" +
                                     "СИГНАЛЬНЫЕ ПОКАЗАНИЯ:\n" +
                                     "● Красный: СТОЙ! Запрещается проезжать светофор.\n" +
                                     "● Жёлтый: Разрешается следовать с готовностью остановиться; следующий светофор закрыт.\n" +
                                     "● Зелёный: Разрешается следовать с установленной скоростью; путь свободен.\n" +
                                     "● Два жёлтых: Разрешается следовать на боковой путь с уменьшенной скоростью.\n" +
                                     "● Лунно-белый мигающий: Пригласительный сигнал — разрешается проследование светофора с красным огнём со скоростью не более 20 км/ч.",
                    ImagePath = "Resources/traffic_lights/type1.png"
                },
                new TrafficLightInfo
                {
                    Type = "Тип 2",
                    ShortName = "Выходной мачтовый",
                    FullDescription = "ВЫХОДНОЙ МАЧТОВЫЙ СВЕТОФОР\n────────────────────────────\n\n" +
                                     "Назначение: Разрешает или запрещает поезду отправиться со станции на перегон.\n\n" +
                                     "СИГНАЛЬНЫЕ ПОКАЗАНИЯ:\n" +
                                     "● Красный: СТОЙ! Отправление запрещено.\n" +
                                     "● Жёлтый: Разрешается отправление, следующий светофор закрыт.\n" +
                                     "● Зелёный: Разрешается отправление, путь на перегоне свободен.\n" +
                                     "● Два жёлтых: Разрешается отправление на ответвление с уменьшенной скоростью.",
                    ImagePath = "Resources/traffic_lights/type2.png"
                },
                new TrafficLightInfo
                {
                    Type = "Тип 3",
                    ShortName = "Маршрутный мачтовый",
                    FullDescription = "МАРШРУТНЫЙ МАЧТОВЫЙ СВЕТОФОР\n────────────────────────────────\n\n" +
                                     "Назначение: Разрешает или запрещает поезду следовать из одного района станции в другой.\n" +
                                     "Указывает на готовность маршрута внутри станции.\n\n" +
                                     "СИГНАЛЬНЫЕ ПОКАЗАНИЯ:\n" +
                                     "● Красный: СТОЙ! Маршрут не готов.\n" +
                                     "● Жёлтый: Разрешается следовать на боковой путь.\n" +
                                     "● Зелёный: Разрешается следовать по главному пути.",
                    ImagePath = "Resources/traffic_lights/type3.png"
                },
                new TrafficLightInfo
                {
                    Type = "Тип 4",
                    ShortName = "Предупредительный",
                    FullDescription = "ПРЕДУПРЕДИТЕЛЬНЫЙ СВЕТОФОР\n─────────────────────────────\n\n" +
                                     "Назначение: Предупреждает машиниста о показании основного светофора, к которому он приближается.\n" +
                                     "Устанавливается перед входным или проходным светофором.\n\n" +
                                     "СИГНАЛЬНЫЕ ПОКАЗАНИЯ:\n" +
                                     "● Жёлтый: Впереди светофор закрыт (горит красный).\n" +
                                     "● Зелёный: Впереди светофор открыт (горит зелёный).",
                    ImagePath = "Resources/traffic_lights/type4.png"
                },
                new TrafficLightInfo
                {
                    Type = "Тип 5",
                    ShortName = "Проходной",
                    FullDescription = "ПРОХОДНОЙ СВЕТОФОР\n─────────────────────\n\n" +
                                     "Назначение: Делят перегон на блок-участки при автоблокировке.\n" +
                                     "Указывает, свободен или занят впередилежащий блок-участок.\n\n" +
                                     "СИГНАЛЬНЫЕ ПОКАЗАНИЯ:\n" +
                                     "● Красный: СТОЙ! Впередилежащий блок-участок занят.\n" +
                                     "● Жёлтый: Разрешается следовать с готовностью остановиться; следующий блок-участок свободен, но следующий светофор закрыт.\n" +
                                     "● Зелёный: Впереди свободны два и более блок-участка.",
                    ImagePath = "Resources/traffic_lights/type5.png"
                },
                new TrafficLightInfo
                {
                    Type = "Тип 6",
                    ShortName = "Заградительный",
                    FullDescription = "ЗАГРАДИТЕЛЬНЫЙ СВЕТОФОР\n──────────────────────────\n\n" +
                                     "Назначение: Требует остановки при опасности для движения, возникающей на переездах, крупных мостах, в тоннелях, обвальных местах.\n\n" +
                                     "СИГНАЛЬНЫЕ ПОКАЗАНИЯ:\n" +
                                     "● Красный: СТОЙ! Опасность для движения.\n" +
                                     "● Нет огня: Опасности нет.\n" +
                                     "● Лунно-белый: Разрешает движение со скоростью не более 20 км/ч при обесточенном светофоре.",
                    ImagePath = "Resources/traffic_lights/type6.png"
                },
                new TrafficLightInfo
                {
                    Type = "Тип 7",
                    ShortName = "Повторительный",
                    FullDescription = "ПОВТОРИТЕЛЬНЫЙ СВЕТОФОР\n───────────────────────────\n\n" +
                                     "Назначение: Служит для повторения показаний основного светофора в условиях плохой видимости.\n" +
                                     "Устанавливается под основным светофором или на отдельной мачте.\n\n" +
                                     "СИГНАЛЬНЫЕ ПОКАЗАНИЯ:\n" +
                                     "● Зелёный: Основной светофор открыт.\n" +
                                     "● Жёлтый: Основной светофор требует уменьшения скорости.\n" +
                                     "● Нет огня: Основным светофором показывается запрещающее показание.",
                    ImagePath = "Resources/traffic_lights/type7.png"
                },
                new TrafficLightInfo
                {
                    Type = "Тип 8",
                    ShortName = "Локомотивный",
                    FullDescription = "ЛОКОМОТИВНЫЙ СВЕТОФОР\n─────────────────────────\n\n" +
                                     "Назначение: Устанавливается в кабине машиниста и дублирует показания путевых светофоров.\n" +
                                     "Работает в системах АЛС (автоматическая локомотивная сигнализация).\n\n" +
                                     "СИГНАЛЬНЫЕ ПОКАЗАНИЯ:\n" +
                                     "● Красный: СТОЙ! Проезд светофора запрещён.\n" +
                                     "● Жёлтый: Разрешается движение с готовностью остановиться.\n" +
                                     "● Зелёный: Разрешается движение с установленной скоростью.\n" +
                                     "● Белый: Система АЛС включена, но информация с пути не передаётся.",
                    ImagePath = "Resources/traffic_lights/type8.png"
                },
                new TrafficLightInfo
                {
                    Type = "Тип 9",
                    ShortName = "Маневровый карликовый",
                    FullDescription = "МАНЕВРОВЫЙ КАРЛИКОВЫЙ СВЕТОФОР\n──────────────────────────────────\n\n" +
                                     "Назначение: Регулирует маневровые передвижения на станционных путях.\n" +
                                     "Имеет низкую мачту (карлик) для установки в междупутье.\n\n" +
                                     "СИГНАЛЬНЫЕ ПОКАЗАНИЯ:\n" +
                                     "● Красный: СТОЙ! Запрещается маневрировать.\n" +
                                     "● Лунно-белый: Разрешается маневрировать.\n" +
                                     "● Синий: Запрещается маневрировать (на некоторых дорогах).",
                    ImagePath = "Resources/traffic_lights/type9.png"
                },
                new TrafficLightInfo
                {
                    Type = "Тип 10",
                    ShortName = "Маневровый мачтовый",
                    FullDescription = "МАНЕВРОВЫЙ МАЧТОВЫЙ СВЕТОФОР\n─────────────────────────────────\n\n" +
                                     "Назначение: То же, что и карликовый, но устанавливается на высокой мачте для лучшей видимости.\n\n" +
                                     "СИГНАЛЬНЫЕ ПОКАЗАНИЯ:\n" +
                                     "● Красный: СТОЙ! Запрещается маневрировать.\n" +
                                     "● Лунно-белый: Разрешается маневрировать.\n" +
                                     "● Синий: Запрещается маневрировать (применяется на некоторых дорогах вместо красного).",
                    ImagePath = "Resources/traffic_lights/type10.png"
                },
                new TrafficLightInfo
                {
                    Type = "Тип 11",
                    ShortName = "Горочный",
                    FullDescription = "ГОРОЧНЫЙ СВЕТОФОР\n────────────────────\n\n" +
                                     "Назначение: Регулирует роспуск вагонов с сортировочной горки.\n" +
                                     "Устанавливается на вершине горки.\n\n" +
                                     "СИГНАЛЬНЫЕ ПОКАЗАНИЯ:\n" +
                                     "● Красный: СТОЙ! Роспуск запрещён.\n" +
                                     "● Жёлтый: Разрешается роспуск с уменьшенной скоростью.\n" +
                                     "● Зелёный: Разрешается роспуск с установленной скоростью.\n" +
                                     "● Буква 'Н' на светофоре: Осаживание вагонов назад.",
                    ImagePath = "Resources/traffic_lights/type11.png"
                },
                new TrafficLightInfo
                {
                    Type = "Тип 12",
                    ShortName = "Светофор прикрытия",
                    FullDescription = "СВЕТОФОР ПРИКРЫТИЯ\n─────────────────────\n\n" +
                                     "Назначение: Ограждает места, опасные для движения (переезды, мосты, тоннели, обвальные участки).\n" +
                                     "Устанавливается с обеих сторон опасного места.\n\n" +
                                     "СИГНАЛЬНЫЕ ПОКАЗАНИЯ:\n" +
                                     "● Красный: СТОЙ! Впереди опасное место.\n" +
                                     "● Жёлтый: Следовать с особой бдительностью, опасное место впереди.\n" +
                                     "● Зелёный: Опасности нет, разрешается движение с установленной скоростью.",
                    ImagePath = "Resources/traffic_lights/type12.png"
                },
                new TrafficLightInfo
                {
                    Type = "Тип 13",
                    ShortName = "Повторительный на мосту",
                    FullDescription = "ПОВТОРИТЕЛЬНЫЙ СВЕТОФОР НА МОСТУ\n────────────────────────────────────\n\n" +
                                     "Назначение: Устанавливается перед длинными мостами и в тоннелях для повышения безопасности.\n" +
                                     "Повторяет показания основного светофора, который может быть плохо виден.\n\n" +
                                     "СИГНАЛЬНЫЕ ПОКАЗАНИЯ:\n" +
                                     "Полностью соответствуют показаниям основного светофора, перед которым он установлен.\n" +
                                     "● Может иметь дополнительную белую мигающую лампу для привлечения внимания.",
                    ImagePath = "Resources/traffic_lights/type13.png"
                }
            };

            RefreshTypesList();
        }

        private void RefreshTypesList()
        {
            dgvTrafficLights.Rows.Clear();

            foreach (var light in _trafficLights)
            {
                string typeName = $"{light.Type}\n{light.ShortName}";
                Image? img = LoadTrafficLightImage(light.ImagePath);
                
                int rowIndex = dgvTrafficLights.Rows.Add(typeName, light.FullDescription, img);
                dgvTrafficLights.Rows[rowIndex].Tag = light;
            }

            // Автоматически подбираем высоту строк под содержимое
            dgvTrafficLights.AutoResizeRows(DataGridViewAutoSizeRowsMode.AllCellsExceptHeaders);
        }

        private Image? LoadTrafficLightImage(string imagePath)
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                
                string[] possiblePaths = new[]
                {
                    Path.Combine(baseDir, imagePath),
                    Path.Combine(baseDir, "Resources", "traffic_lights", Path.GetFileName(imagePath)),
                    Path.Combine(baseDir, "Resources", Path.GetFileName(imagePath)),
                    Path.Combine(Directory.GetCurrentDirectory(), imagePath)
                };

                foreach (string path in possiblePaths)
                {
                    if (File.Exists(path))
                    {
                        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
                        return Image.FromStream(fs);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Ошибка загрузки фото: {ex.Message}");
            }

            return CreatePlaceholderImage();
        }

        private Image CreatePlaceholderImage()
        {
            var bmp = new Bitmap(200, 200);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.LightGray);
                using (var font = new Font("Arial", 10, FontStyle.Bold))
                using (var brush = new SolidBrush(Color.Gray))
                {
                    g.DrawString("Фото\nотсутствует", font, brush, new RectangleF(50, 70, 100, 60));
                    
                    // Рисуем простую схему светофора если нет фото
                    using (var pen = new Pen(Color.Black, 2))
                    {
                        g.DrawRectangle(pen, 60, 20, 80, 100);
                        g.FillEllipse(Brushes.Red, 75, 30, 50, 30);
                        g.FillEllipse(Brushes.Yellow, 75, 60, 50, 30);
                        g.FillEllipse(Brushes.Green, 75, 90, 50, 30);
                    }
                }
            }
            return bmp;
        }

        private void TxtSearchType_TextChanged(object sender, EventArgs e)
        {
            string filter = txtSearchType.Text.ToLower();
            dgvTrafficLights.Rows.Clear();

            foreach (var light in _trafficLights)
            {
                string typeName = $"{light.Type} {light.ShortName}";
                if (typeName.ToLower().Contains(filter) ||
                    light.FullDescription.ToLower().Contains(filter))
                {
                    Image? img = LoadTrafficLightImage(light.ImagePath);
                    int rowIndex = dgvTrafficLights.Rows.Add(
                        $"{light.Type}\n{light.ShortName}", 
                        light.FullDescription, 
                        img
                    );
                    dgvTrafficLights.Rows[rowIndex].Tag = light;
                }
            }
            
            dgvTrafficLights.AutoResizeRows(DataGridViewAutoSizeRowsMode.AllCellsExceptHeaders);
        }

        private void DgvTrafficLights_SelectionChanged(object sender, EventArgs e)
        {
        
            if (dgvTrafficLights.SelectedRows.Count > 0)
            {
                var light = dgvTrafficLights.SelectedRows[0].Tag as TrafficLightInfo;
                if (light != null)
                {
                    
                }
            }
        }

        private void CreateTrainingTab()
        {
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 8 };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));

            var lblName = new Label { Text = "Название объекта:", Anchor = AnchorStyles.Left };
            panel.Controls.Add(lblName, 0, 0);
            txtCustomName = new TextBox { Anchor = AnchorStyles.Left | AnchorStyles.Right };
            panel.Controls.Add(txtCustomName, 1, 0);

            btnAddCustomObject = new Button { Text = "➕ Добавить объект", Anchor = AnchorStyles.Left | AnchorStyles.Right };
            btnAddCustomObject.Click += BtnAddCustomObject_Click!;
            panel.Controls.Add(btnAddCustomObject, 0, 1);
            panel.SetColumnSpan(btnAddCustomObject, 2);

            progressBarAdd = new ProgressBar 
            { 
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Height = 20,
                Visible = false
            };
            panel.Controls.Add(progressBarAdd, 0, 2);
            panel.SetColumnSpan(progressBarAdd, 2);

            var lblList = new Label { Text = "Добавленные объекты:", Anchor = AnchorStyles.Left };
            panel.Controls.Add(lblList, 0, 3);
            listBoxCustomObjects = new ListBox { Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom, Height = 100 };
            panel.Controls.Add(listBoxCustomObjects, 1, 3);

            btnRemoveCustomObject = new Button { Text = "❌ Удалить выбранный", Anchor = AnchorStyles.Left | AnchorStyles.Right };
            btnRemoveCustomObject.Click += BtnRemoveCustomObject_Click!;
            panel.Controls.Add(btnRemoveCustomObject, 0, 4);
            panel.SetColumnSpan(btnRemoveCustomObject, 2);

            btnUseTemporaryDetector = new Button
            {
                Text = "🧪 Тестовый детектор (для проверки)",
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.LightYellow
            };
            btnUseTemporaryDetector.Click += BtnUseTemporaryDetector_Click!;
            panel.Controls.Add(btnUseTemporaryDetector, 0, 5);
            panel.SetColumnSpan(btnUseTemporaryDetector, 2);

            btnRetrain = new Button { Text = "🔄 Переобучить YOLO26", Anchor = AnchorStyles.Left | AnchorStyles.Right };
            btnRetrain.Click += BtnRetrain_Click!;
            panel.Controls.Add(btnRetrain, 0, 6);
            panel.SetColumnSpan(btnRetrain, 2);

            txtTrainingLog = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true };
            panel.Controls.Add(txtTrainingLog, 0, 7);
            panel.SetColumnSpan(txtTrainingLog, 2);

            tabTraining.Controls.Add(panel);
        }

        private void CreateAboutTab()
        {
            var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 1 };

            var aboutBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.White,
                Font = new Font("Arial", 11)
            };

            aboutBox.Text =
                "═══════════════════════════════════════════════════════════════════════\n" +
                "                    OSTU VISION \n" +
                "═══════════════════════════════════════════════════════════════════════\n\n" +

                "📌 ОПИСАНИЕ ПРОГРАММЫ\n" +
                "Программа для автоматического анализа фото и видео с целью\n" +
                "распознавания железнодорожных светофоров, поездов и\n" +
                "нестандартных объектов на железных путях.\n\n" +

                "🚀 YOLO26 - НОВЕЙШАЯ ВЕРСИЯ 2026\n" +
                "├── End-to-end детекция (без NMS)\n" +
                "├── MuSGD оптимизатор\n" +
                "├── ProgLoss + STAL для мелких объектов\n" +
                "├── +43% скорости на CPU\n" +
                "└── Лучшая детекция светофоров и шкафов\n\n" +

                "⚙️ ИСПОЛЬЗУЕМЫЕ ТЕХНОЛОГИИ\n" +
                "├── Платформа: .NET 10.0\n" +
                "├── Интерфейс: Windows Forms\n" +
                "├── Нейросеть: YOLO26 (ONNX Runtime)\n" +
                "├── Обработка изображений: ImageSharp\n" +
                "├── Синтез речи: System.Speech\n" +
                "└── Формат данных: JSON\n\n" +

                "👨‍💻 АВТОР\n" +
                "Разработчик: \n" +
                "Минаков Виталий Анатольевич\n" +
                "Носков Виталий Олегович \n" +
                "Астапенко Владислав Сергеевич \n" +
                 "Версия: 2.0.0 \n" +
                "Дата выпуска: 2026\n\n" +

                "📞 ПОДДЕРЖКА\n" +
                "По вопросам работы программы обращайтесь:\n" +
                "GitHub: https://github.com/Astap55rus/OSTU_VIsion.git\n\n" +

                "═══════════════════════════════════════════════════════════════════════\n" +
                "© 2026-2027 ОмГУПС. Все права защищены.\n" +
                "═══════════════════════════════════════════════════════════════════════";

            panel.Controls.Add(aboutBox);
            tabAbout.Controls.Add(panel);
        }

        //  ОБРАБОТЧИКИ СОБЫТИЙ

        private void BtnLoadFile_Click(object sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "Изображения|*.jpg;*.jpeg;*.png;*.bmp;*.webp;*.gif;*.tiff",
                Title = "Выберите изображения для анализа"
            };
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                _selectedFiles = ofd.FileNames.ToList();
                txtFilePath.Text = string.Join("; ", _selectedFiles);
                listBoxFiles.Items.Clear();
                foreach (var file in _selectedFiles)
                {
                    listBoxFiles.Items.Add($"📷 {Path.GetFileName(file)}");
                }
                ShowPreview(_selectedFiles[0]);
                statusLabel.Text = $"Выбрано фото: {_selectedFiles.Count} шт.";
            }
        }

        private void BtnLoadVideo_Click(object sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "Видеофайлы|*.mp4;*.avi;*.mkv;*.mov;*.wmv;*.flv",
                Title = "Выберите видеофайлы для анализа"
            };
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                _selectedFiles = ofd.FileNames.ToList();
                txtFilePath.Text = string.Join("; ", _selectedFiles);
                listBoxFiles.Items.Clear();
                foreach (var file in _selectedFiles)
                {
                    listBoxFiles.Items.Add($"🎬 {Path.GetFileName(file)}");
                }
                ShowPreview(_selectedFiles[0]);
                statusLabel.Text = $"Выбрано видео: {_selectedFiles.Count} шт.";
            }
        }

        private async void BtnAnalyze_Click(object sender, EventArgs e)
        {
            if (_selectedFiles.Count == 0)
            {
                MessageBox.Show("Сначала выберите файлы для анализа!", "Информация",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            btnAnalyze.Enabled = false;
            btnLoadFile.Enabled = false;
            btnLoadVideo.Enabled = false;
            progressBar.Value = 0;
            statusLabel.Text = "Обработка...";
// Типы поддерживаемых файлов 
            var imageExts = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".webp", ".gif", ".tiff" };
            var videoExts = new[] { ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv" };

            try
            {
                int totalFiles = _selectedFiles.Count;
                int processed = 0;

                foreach (var file in _selectedFiles)
                {
                    string ext = Path.GetExtension(file).ToLower();
                    
                    if (imageExts.Contains(ext))
                    {
                        await ProcessImageAsync(file);
                        statusLabel.Text = $"Обработано фото {processed + 1}/{totalFiles}";
                    }
                    else if (videoExts.Contains(ext))
                    {
                        await ProcessVideoAsync(file);
                        statusLabel.Text = $"Обработано видео {processed + 1}/{totalFiles}";
                    }
                    else
                    {
                        MessageBox.Show($"Неподдерживаемый формат: {Path.GetFileName(file)}", 
                            "Предупреждение", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }

                    processed++;
                    progressBar.Value = (processed * 100) / totalFiles;
                }

                MessageBox.Show($"Обработка всех файлов завершена!\nВсего обработано: {totalFiles}",
                    "Успех", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.Message);
                MessageBox.Show($"Ошибка при обработке: {ex.Message}", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                btnAnalyze.Enabled = true;
                btnLoadFile.Enabled = true;
                btnLoadVideo.Enabled = true;
                statusLabel.Text = "Готов";
                progressBar.Value = 0;
            }
        }

        private void BtnSaveResult_Click(object sender, EventArgs e)
        {
            if (pictureBoxResult.Image != null)
            {
                using var sfd = new SaveFileDialog { Filter = "PNG|*.png" };
                if (sfd.ShowDialog() == DialogResult.OK)
                    pictureBoxResult.Image.Save(sfd.FileName);
            }
        }

        private void BtnAddCustomObject_Click(object sender, EventArgs e)
        {
            if (_training == null)
            {
                MessageBox.Show("Сервис обучения недоступен", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string name = txtCustomName.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Введите название объекта");
                return;
            }

            using var ofd = new OpenFileDialog 
            { 
                Filter = "Изображения|*.jpg;*.jpeg;*.png;*.bmp;*.webp",
                Multiselect = true,
                Title = $"Выберите фото для класса: {name}"
            };
            
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                int successCount = 0;
                int errorCount = 0;
                int totalFiles = ofd.FileNames.Length;
                
                progressBarAdd.Visible = true;
                progressBarAdd.Maximum = totalFiles;
                progressBarAdd.Value = 0;
                
                foreach (string fileName in ofd.FileNames)
                {
                    try
                    {
                        _training.AddCustomObject(name, fileName);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        Logger.LogError($"Ошибка добавления {Path.GetFileName(fileName)}: {ex.Message}");
                    }
                    progressBarAdd.Value++;
                    Application.DoEvents();
                }
                
                progressBarAdd.Visible = false;
                RefreshCustomObjectsList();
                
                string message = $"Успешно добавлено: {successCount} из {totalFiles} фото";
                if (errorCount > 0)
                    message += $"\nНе удалось добавить: {errorCount} фото";
                
                MessageBox.Show(message, "Результат", 
                    MessageBoxButtons.OK, 
                    errorCount > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
                
                txtCustomName.Clear();
            }
        }

        private void BtnRemoveCustomObject_Click(object sender, EventArgs e)
        {
            if (_training == null)
            {
                MessageBox.Show("Сервис обучения недоступен", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (listBoxCustomObjects.SelectedItem is string name)
            {
                var result = MessageBox.Show($"Удалить объект '{name}'?", "Подтверждение",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                {
                    _training.RemoveCustomObject(name);
                    RefreshCustomObjectsList();
                }
            }
        }

        private void BtnUseTemporaryDetector_Click(object sender, EventArgs e)
        {
            _useTemporaryDetector = true;
            _detector = new TemporaryDetector();
            MessageBox.Show("Включен тестовый режим! Теперь при анализе будут показываться тестовые объекты.",
                "Тестовый режим", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private async void BtnRetrain_Click(object sender, EventArgs e)
        {
            if (_training == null)
            {
                MessageBox.Show("Сервис обучения недоступен", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var result = MessageBox.Show("Запустить переобучение модели YOLO26? Это может занять длительное время.",
                                         "Подтверждение", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result == DialogResult.Yes)
            {
                btnRetrain.Enabled = false;
                txtTrainingLog.Clear();
                txtTrainingLog.AppendText("🚀 Начало переобучения YOLO26...\n");

                await Task.Run(() =>
                {
                    _training.StartRetraining(
                        onProgress: msg => BeginInvoke(new Action(() =>
                        {
                            if (msg != null)
                                txtTrainingLog.AppendText(msg + Environment.NewLine);
                        })),
                        onComplete: success => BeginInvoke(new Action(() =>
                        {
                            btnRetrain.Enabled = true;
                            txtTrainingLog.AppendText(success ?
                                "✅ Модель YOLO26 успешно переобучена!\n" :
                                "❌ Ошибка при переобучении.\n");
                            if (success)
                            {
                                _useTemporaryDetector = false;
                                string modelPath = _config["ModelPath"]?.ToString() ?? "Models/yolo26n.onnx";
                                float threshold = float.Parse(_config["ConfidenceThreshold"]?.ToString() ?? "0.3");
                                string[] labels = _config["ClassLabels"]?.ToObject<string[]>() ?? Array.Empty<string>();
                                
                                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                                string fullPath = Path.Combine(baseDir, modelPath);
                                
                                _detector = new Yolo26Detector(fullPath, threshold, labels);
                            }
                            MessageBox.Show(success ?
                                "Модель успешно переобучена!" :
                                "Ошибка при переобучении.",
                                "Результат", MessageBoxButtons.OK,
                                success ? MessageBoxIcon.Information : MessageBoxIcon.Error);
                        }))
                    );
                });
            }
        }

        //  ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ 

        private void LoadConfig()
        {
            try
            {
                string configPath = "appsettings.json";
                if (!File.Exists(configPath))
                {
                    var defaultConfig = new JObject
                    {
                        ["ModelPath"] = "Models/yolo26n.onnx",  // Путь к модели
                        ["CustomModelPath"] = "Models/custom_yolo26.pt",
                        ["CustomObjectsDb"] = "Data/custom_objects.json",
                        ["ConfidenceThreshold"] = 0.3,  // Порог уверенности (30%)
                        ["ResultsFolder"] = "Results",  // Папка для результатов
                        ["CustomDataFolder"] = "CustomData", // Папка для пользовательских моделей
                        ["ClassLabels"] = new JArray // Все классы, которые умеет распознавать
                        {
                            "светофор_тип1", "светофор_тип2", "светофор_тип3", "светофор_тип4",
                            "светофор_тип5", "светофор_тип6", "светофор_тип7", "светофор_тип8",
                            "светофор_тип9", "светофор_тип10", "светофор_тип11", "светофор_тип12",
                            "светофор_тип13", "поезд", "нестандартный_объект"
                        }
                    };

                    File.WriteAllText(configPath, defaultConfig.ToString());
                    _config = defaultConfig;
                }
                else
                {
                    string json = File.ReadAllText(configPath);
                    _config = JObject.Parse(json);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки конфигурации: {ex.Message}", "Ошибка",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                throw;
            }
        }

// Блок инициализации сервисов
        private void InitServices()
        {
            try
            {
                string modelPath = _config["ModelPath"]?.ToString() ?? "Models/yolo26n.onnx";
                float threshold = float.Parse(_config["ConfidenceThreshold"]?.ToString() ?? "0.3");
                string[] labels = _config["ClassLabels"]?.ToObject<string[]>() ?? Array.Empty<string>();

                string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                 // поиск файлов модели по нескольким путям
                string[] possiblePaths = new[]
                {
                    Path.Combine(baseDirectory, modelPath),
                    Path.Combine(baseDirectory, "Models", "yolo26n.onnx"),
                    Path.Combine(baseDirectory, "Models", "yolov8n.onnx"), // Запасной вариант
                    Path.Combine(Directory.GetCurrentDirectory(), modelPath)
                };

                string? foundPath = null;
                foreach (string path in possiblePaths)
                {
                    if (File.Exists(path))
                    {
                        foundPath = path;
                        break;
                    }
                }

                if (foundPath == null)
                {
                    string message = "Модель YOLO26 не найдена! Искали в:\n" + 
                                   string.Join("\n", possiblePaths) + 
                                   "\n\nХотите использовать тестовый детектор для проверки интерфейса?";
                    
                    var result = MessageBox.Show(message, "Модель не найдена", 
                        MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    
                    if (result == DialogResult.Yes)
                    {
                        _useTemporaryDetector = true;
                        _detector = new TemporaryDetector(); // Тестовый детектор если не найдет основновной
                        _speech = new SpeechService();
                        
                        string customDataFolder1 = _config["CustomDataFolder"]?.ToString() ?? "CustomData";
                        string dbPath1 = _config["CustomObjectsDb"]?.ToString() ?? "Data/custom_objects.json";
                        
                        Directory.CreateDirectory(customDataFolder1);
                        Directory.CreateDirectory(Path.GetDirectoryName(dbPath1) ?? "Data");
                        Directory.CreateDirectory(_config["ResultsFolder"]?.ToString() ?? "Results");
                        
                        _training = new TrainingService(customDataFolder1, dbPath1);
                        RefreshCustomObjectsList();
                        
                        statusLabel.Text = "Тестовый режим (модель не загружена)";
                        return;
                    }
                    else
                    {
                        Application.Exit();
                        return;
                    }
                }

                _detector = new Yolo26Detector(foundPath, threshold, labels);
                _speech = new SpeechService();

                string customDataFolder2 = _config["CustomDataFolder"]?.ToString() ?? "CustomData";
                string dbPath2 = _config["CustomObjectsDb"]?.ToString() ?? "Data/custom_objects.json";

                Directory.CreateDirectory(customDataFolder2);
                Directory.CreateDirectory(Path.GetDirectoryName(dbPath2) ?? "Data");
                Directory.CreateDirectory(_config["ResultsFolder"]?.ToString() ?? "Results");
                Directory.CreateDirectory(Path.Combine(_config["ResultsFolder"]?.ToString() ?? "Results", "Images"));
                Directory.CreateDirectory(Path.Combine(_config["ResultsFolder"]?.ToString() ?? "Results", "Videos"));

                _training = new TrainingService(customDataFolder2, dbPath2);
                RefreshCustomObjectsList();

                statusLabel.Text = "✅ YOLO26 загружена, программа готова к работе";
                Logger.Log($"Модель загружена из {foundPath}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при инициализации: {ex.Message}\n\nХотите продолжить в тестовом режиме?",
                    "Ошибка", MessageBoxButtons.YesNo, MessageBoxIcon.Error);
                
                var result = MessageBox.Show("Продолжить в тестовом режиме?", "Ошибка", 
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                
                if (result == DialogResult.Yes)
                {
                    _useTemporaryDetector = true;
                    _detector = new TemporaryDetector();
                    _speech = new SpeechService();
                    
                    string customDataFolder3 = _config["CustomDataFolder"]?.ToString() ?? "CustomData";
                    string dbPath3 = _config["CustomObjectsDb"]?.ToString() ?? "Data/custom_objects.json";
                    
                    Directory.CreateDirectory(customDataFolder3);
                    Directory.CreateDirectory(Path.GetDirectoryName(dbPath3) ?? "Data");
                    Directory.CreateDirectory(_config["ResultsFolder"]?.ToString() ?? "Results");
                    
                    _training = new TrainingService(customDataFolder3, dbPath3);
                    RefreshCustomObjectsList();
                    
                    statusLabel.Text = "Тестовый режим (ошибка загрузки модели)";
                }
                else
                {
                    Application.Exit();
                }
            }
        }

        private void RefreshCustomObjectsList()
        {
            try
            {
                if (listBoxCustomObjects != null && _training != null)
                {
                    listBoxCustomObjects.Items.Clear();
                    foreach (var obj in _training.GetAllObjects())
                    {
                        listBoxCustomObjects.Items.Add(obj.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Ошибка обновления списка объектов: {ex.Message}");
            }
        }

        private void ShowPreview(string file)
        {
            try
            {
                string ext = Path.GetExtension(file).ToLower();
                var imageExts = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".webp", ".gif", ".tiff" };
                var videoExts = new[] { ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv" };
                
                if (imageExts.Contains(ext))
                {
                    var img = ImageUtils.LoadImageSafe(file);
                    if (pictureBoxOriginal.Image != null)
                    {
                        pictureBoxOriginal.Image.Dispose();
                    }
                    pictureBoxOriginal.Image = img;
                    pictureBoxResult.Image = null;
                    statusLabel.Text = $"Фото: {Path.GetFileName(file)}";
                }
                else if (videoExts.Contains(ext))
                {
                    using var capture = new VideoCapture(file);
                    using var mat = new Mat();
                    if (capture.Read(mat))
                    {
                        var bmp = MatToBitmap(mat);
                        if (pictureBoxOriginal.Image != null)
                        {
                            pictureBoxOriginal.Image.Dispose();
                        }
                        pictureBoxOriginal.Image = bmp;
                        statusLabel.Text = $"Видео: {Path.GetFileName(file)} (первый кадр)";
                    }
                    else
                    {
                        pictureBoxOriginal.Image = null;
                        statusLabel.Text = $"Не удалось прочитать видео: {Path.GetFileName(file)}";
                    }
                    pictureBoxResult.Image = null;
                }
                else
                {
                    pictureBoxOriginal.Image = null;
                    pictureBoxResult.Image = null;
                    statusLabel.Text = "Неподдерживаемый формат файла";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при предпросмотре: {ex.Message}");
                Logger.LogError($"Ошибка предпросмотра: {ex.Message}");
            }
        }

        private Bitmap? MatToBitmap(Mat mat)
        {
            try
            {
                if (mat.Type() != MatType.CV_8UC3)
                    return null;

                var bitmap = new Bitmap(mat.Width, mat.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
                var bmpData = bitmap.LockBits(rect, System.Drawing.Imaging.ImageLockMode.WriteOnly,
                    System.Drawing.Imaging.PixelFormat.Format24bppRgb);

                try
                {
                    int stride = bmpData.Stride;
                    int step = (int)mat.Step();
                    int height = mat.Height;

                    for (int y = 0; y < height; y++)
                    {
                        long dstOffset = y * stride;
                        long srcOffset = y * step;

                        IntPtr dstPtr = IntPtr.Add(bmpData.Scan0, (int)dstOffset);
                        IntPtr srcPtr = IntPtr.Add(mat.Data, (int)srcOffset);

                        byte[] rowData = new byte[step];
                        System.Runtime.InteropServices.Marshal.Copy(srcPtr, rowData, 0, step);
                        System.Runtime.InteropServices.Marshal.Copy(rowData, 0, dstPtr, Math.Min(step, stride));
                    }
                }
                finally
                {
                    bitmap.UnlockBits(bmpData);
                }

                return bitmap;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Ошибка конвертации Mat в Bitmap: {ex.Message}");
                return null;
            }
        }

        private async Task ProcessImageAsync(string path)
        {
            statusLabel.Text = $"Детекция {Path.GetFileName(path)}...";

            using var img = new Bitmap(path);
            List<DetectionResult> detections;

            if (_useTemporaryDetector)
            {
                detections = ((TemporaryDetector)_detector).Detect(img);
            }
            else
            {
                detections = await _detector.DetectAsync(img);
            }

            Logger.Log($"ProcessImageAsync: найдено {detections.Count} объектов");

            if (detections.Count == 0)
            {
                if (pictureBoxResult.Image != null)
                {
                    pictureBoxResult.Image.Dispose();
                }
                pictureBoxResult.Image = (Bitmap)img.Clone();
                statusLabel.Text = "Объектов не обнаружено";
                return;
            }

            using var annotated = new Bitmap(img);
            using (var g = Graphics.FromImage(annotated))
            {
                int drawnCount = 0;
                foreach (var det in detections)
                {
                    var rect = det.BoundingBox;
                    
                    rect.X = Math.Max(0, Math.Min(rect.X, img.Width - 1));
                    rect.Y = Math.Max(0, Math.Min(rect.Y, img.Height - 1));
                    rect.Width = Math.Min(rect.Width, img.Width - rect.X);
                    rect.Height = Math.Min(rect.Height, img.Height - rect.Y);
                    
                    if (rect.Width <= 0 || rect.Height <= 0)
                    {
                        continue;
                    }

                    Color color = det.ClassName.Contains("светофор") ? Color.Green :
                                 det.ClassName.Contains("поезд") ? Color.Blue :
                                 det.ClassName.Contains("машина") ? Color.Orange : Color.Red;

                    using var pen = new Pen(color, 3);
                    g.DrawRectangle(pen, rect);

                    string label = $"{det.ClassName} {det.Confidence:P0}";
                    if (!string.IsNullOrEmpty(det.SignalState))
                        label += $" {det.SignalState}";

                    var font = new Font("Arial", 10);
                    var textSize = g.MeasureString(label, font);
                    var textBg = new Rectangle(
                        rect.X, rect.Y - (int)textSize.Height - 5,
                        (int)textSize.Width + 10, (int)textSize.Height + 5
                    );

                    if (textBg.Y < 0)
                        textBg.Y = rect.Y + rect.Height + 5;

                    if (textBg.X + textBg.Width > img.Width)
                        textBg.X = img.Width - textBg.Width - 5;

                    using var bgBrush = new SolidBrush(Color.FromArgb(200, Color.Black));
                    g.FillRectangle(bgBrush, textBg);

                    using var textBrush = new SolidBrush(color);
                    g.DrawString(label, font, textBrush, textBg.X + 5, textBg.Y + 3);

                    drawnCount++;
                }
                Logger.Log($"Нарисовано объектов: {drawnCount}");
            }

            if (pictureBoxResult.Image != null)
            {
                pictureBoxResult.Image.Dispose();
            }
            pictureBoxResult.Image = (Bitmap)annotated.Clone();

            string resultsFolder = _config["ResultsFolder"]?.ToString() ?? "Results";
            string savePath = Path.Combine(resultsFolder, "Images",
                                           Path.GetFileNameWithoutExtension(path) + "_annotated.png");

            string? directory = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            annotated.Save(savePath, System.Drawing.Imaging.ImageFormat.Png);

            if (detections.Count > 0 && !_useTemporaryDetector)
            {
                try
                {
                    Task.Run(() => _speech.SpeakDetections(detections));
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Ошибка озвучивания: {ex.Message}");
                }
            }

            statusLabel.Text = $"Найдено объектов: {detections.Count}";
        }

        private async Task ProcessVideoAsync(string path)
        {
            statusLabel.Text = $"Обработка видео {Path.GetFileName(path)}...";

            string resultsFolder = _config["ResultsFolder"]?.ToString() ?? "Results";
            var processor = new VideoProcessor(_detector, _speech, resultsFolder);

            await Task.Run(() => processor.ProcessVideo(path, (current, total) =>
            {
                BeginInvoke(new Action(() =>
                {
                    progressBar.Maximum = total;
                    progressBar.Value = current;
                    statusLabel.Text = $"Обработка видео: {current}/{total} кадров";
                }));
            }));

            Logger.Log($"Обработано видео {path}");
            statusLabel.Text = "Видео обработано";
        }
    }

    //  ВРЕМЕННЫЙ ДЕТЕКТОР (при тестовом режиме)
    public class TemporaryDetector : IDetector
    {
        private Random _rand = new Random();

        public List<DetectionResult> Detect(Bitmap image)
        {
            var results = new List<DetectionResult>();

            int w = image.Width;
            int h = image.Height;

            results.Add(new DetectionResult
            {
                ClassName = "светофор_тип1",
                Confidence = 0.92f,
                BoundingBox = new Rectangle(w / 8, h / 4, w / 6, h / 3),
                SignalState = "красный"
            });

            results.Add(new DetectionResult
            {
                ClassName = "поезд",
                Confidence = 0.88f,
                BoundingBox = new Rectangle(w / 3, h / 3, w / 2, h / 4),
                SignalState = null
            });

            results.Add(new DetectionResult
            {
                ClassName = "светофор_тип3",
                Confidence = 0.79f,
                BoundingBox = new Rectangle(w * 3 / 4, h / 5, w / 8, h / 4),
                SignalState = "зелёный"
            });

            results.Add(new DetectionResult
            {
                ClassName = "нестандартный_объект",
                Confidence = 0.67f,
                BoundingBox = new Rectangle(w / 4, h * 3 / 4, w / 5, w / 5),
                SignalState = null
            });

            return results;
        }

        public Task<List<DetectionResult>> DetectAsync(Bitmap image)
        {
            return Task.FromResult(Detect(image));
        }
    }

    //  КЛАСС ДЛЯ ХРАНЕНИЯ ИНФОРМАЦИИ О СВЕТОФОРЕ 
    public class TrafficLightInfo
    {
        public string Type { get; set; } = ""; //тип светофора
        public string ShortName { get; set; } = ""; // название светофора
        public string FullDescription { get; set; } = ""; // Полное описание с сигналами
        public string ImagePath { get; set; } = ""; // Путь к фото
    }
}
