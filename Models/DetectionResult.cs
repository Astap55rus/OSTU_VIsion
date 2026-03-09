using System.Drawing;

namespace OSTUVision.Models // Пространство имен для моделей данных
{
    public class DetectionResult
    {
        public string ClassName { get; set; } = "";
        public float Confidence { get; set; }
        public Rectangle BoundingBox { get; set; }
        public string? SignalState { get; set; }
    }
}