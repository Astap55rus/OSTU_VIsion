using System.Collections.Generic;

namespace OSTUVision.Models // Пространство имен для моделей данных
{
    public class CustomObject
    {
        public string Name { get; set; } = ""; //имя для объекта
        public List<string> ImagePaths { get; set; } = new();
    }
}
