using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using OSTUVision.Models; 

namespace OSTUVision.Services
{
    public interface IDetector
    {
        List<DetectionResult> Detect(Bitmap image);
        Task<List<DetectionResult>> DetectAsync(Bitmap image);
    }
}