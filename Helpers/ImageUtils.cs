// Подключаем библиотеки 
using System.Drawing; // Для работы с изображениями (Bitmap, Graphics)
using System.IO; // Для работы с файлами и папками (File, Directory)

namespace OSTUVision.Helpers // Пространство имен для вспомогательных класс
{
    public static class ImageUtils
    {
        public static Bitmap? LoadImageSafe(string path)
        {
            try
            {
                return new Bitmap(path); // Пытаемся загрузить изображение
            }
            catch
            {
                return null; // Если ошибка - возвращаем пустое значение
            }
        }

        public static void SaveAnnotatedImage(Bitmap image, string outputPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            image.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png); // Сохраняем изображение в формате PNG
        }
    }
}
