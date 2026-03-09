using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;  // Для работы с файлами
using OpenCvSharp;  // OpenCV для работы с видео 
using OSTUVision.Models;

namespace OSTUVision.Services // Пространство имен для сервисов
{
    public class VideoProcessor
    {
        private readonly IDetector _detector;
        private readonly SpeechService _speech;
        private readonly string _outputFolder;

        public VideoProcessor(IDetector detector, SpeechService speech, string outputFolder)
        {
            _detector = detector;
            _speech = speech;
            _outputFolder = outputFolder;
        }

        public void ProcessVideo(string inputPath, Action<int, int>? progressCallback)
        {
            using var capture = new VideoCapture(inputPath);
            int totalFrames = (int)capture.Get(VideoCaptureProperties.FrameCount);
            double fps = capture.Get(VideoCaptureProperties.Fps);
            int width = (int)capture.Get(VideoCaptureProperties.FrameWidth);
            int height = (int)capture.Get(VideoCaptureProperties.FrameHeight);

            string outputPath = Path.Combine(_outputFolder, "Videos", 
                Path.GetFileNameWithoutExtension(inputPath) + "_annotated.mp4");
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            
            using var writer = new VideoWriter(outputPath, FourCC.MP4V, fps, new OpenCvSharp.Size(width, height));

            int frameIndex = 0;
            using var mat = new Mat();
            
            while (capture.Read(mat))
            {
                if (mat.Empty()) break;

                using var bitmap = MatToBitmap(mat);
                if (bitmap != null)
                {
                    var detections = _detector.Detect(bitmap);
                    DrawDetections(mat, detections);
                }

                writer.Write(mat);
                frameIndex++;
                progressCallback?.Invoke(frameIndex, totalFrames);
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

        private void DrawDetections(Mat mat, List<DetectionResult> detections)
        {
            foreach (var det in detections)
            {
                Scalar color = det.ClassName.Contains("светофор") ? Scalar.Green : // Выбираем цвет рамки (OpenCV использует Scalar в формате BGR)
                               det.ClassName.Contains("поезд") ? Scalar.Blue : Scalar.Red;
                
                Cv2.Rectangle(mat, 
                    new OpenCvSharp.Rect(det.BoundingBox.X, det.BoundingBox.Y,
                                         det.BoundingBox.Width, det.BoundingBox.Height), 
                    color, 2);
                
                string label = $"{det.ClassName} {det.Confidence:P0}";
                if (!string.IsNullOrEmpty(det.SignalState))
                    label += $" {det.SignalState}";
                
                Cv2.PutText(mat, label,     // Рисуем текст над рамкой
                    new OpenCvSharp.Point(det.BoundingBox.X, det.BoundingBox.Y - 5),  // Позиция текста
                    HersheyFonts.HersheySimplex, 0.5, color, 1);// Шрифт // Масштаб шрифта // Цвет текста // Толщина текста
            }
        }
    }
}