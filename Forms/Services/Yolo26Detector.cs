using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using OSTUVision.Models;
using Rectangle = System.Drawing.Rectangle;

namespace OSTUVision.Services
{
    /// <summary>
    /// Детектор для YOLO26 (2026) - самая новая версия
    /// Особенности: end-to-end детекция, MuSGD оптимизатор, ProgLoss
    /// </summary>
    public class Yolo26Detector : IDisposable, IDetector
    {
        private readonly InferenceSession? _session;
        private readonly float _confidenceThreshold;
        private readonly string[] _targetClasses;
        private readonly string[] _cocoClasses;
        private readonly int _inputSize = 640;
        private readonly bool _isInitialized;
        private readonly bool _isEnd2End;

        public Yolo26Detector(string modelPath, float confidenceThreshold, string[] targetClasses, bool end2end = true)
        {
            _confidenceThreshold = confidenceThreshold;
            _targetClasses = targetClasses ?? Array.Empty<string>();
            _isEnd2End = end2end;

            // Загружаем COCO классы
            _cocoClasses = LoadCocoClasses();

            var sessionOptions = new SessionOptions();
            sessionOptions.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
            sessionOptions.EnableCpuMemArena = true;
            sessionOptions.ExecutionMode = ExecutionMode.ORT_SEQUENTIAL;

            try
            {
                _session = new InferenceSession(modelPath, sessionOptions);
                _isInitialized = true;
                Logger.Log($"✅ YOLO26 загружена: {Path.GetFileName(modelPath)}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"❌ Ошибка загрузки YOLO26: {ex.Message}");
                _isInitialized = false;
                _session = null;
            }
        }

        private string[] LoadCocoClasses()
        {
            return new string[] 
            { 
                "person", "bicycle", "car", "motorcycle", "airplane", "bus", "train", "truck", "boat",
                "traffic light", "fire hydrant", "stop sign", "parking meter", "bench", "bird", "cat",
                "dog", "horse", "sheep", "cow", "elephant", "bear", "zebra", "giraffe", "backpack",
                "umbrella", "handbag", "tie", "suitcase", "frisbee", "skis", "snowboard", "sports ball",
                "kite", "baseball bat", "baseball glove", "skateboard", "surfboard", "tennis racket",
                "bottle", "wine glass", "cup", "fork", "knife", "spoon", "bowl", "banana", "apple",
                "sandwich", "orange", "broccoli", "carrot", "hot dog", "pizza", "donut", "cake",
                "chair", "couch", "potted plant", "bed", "dining table", "toilet", "tv", "laptop",
                "mouse", "remote", "keyboard", "cell phone", "microwave", "oven", "toaster", "sink",
                "refrigerator", "book", "clock", "vase", "scissors", "teddy bear", "hair drier",
                "toothbrush"
            };
        }

        public List<DetectionResult> Detect(Bitmap image)
        {
            return DetectAsync(image).GetAwaiter().GetResult();
        }

        public async Task<List<DetectionResult>> DetectAsync(Bitmap image)
        {
            if (!_isInitialized || _session == null)
            {
                Logger.LogError("YOLO26 не инициализирован");
                return new List<DetectionResult>();
            }

            return await Task.Run(() =>
            {
                var results = new List<DetectionResult>();

                try
                {
                    var (tensor, originalWidth, originalHeight, scale, padX, padY) = PreprocessImage(image);

                    var inputs = new List<NamedOnnxValue>
                    {
                        NamedOnnxValue.CreateFromTensor("images", tensor)
                    };

                    using var runResults = _session.Run(inputs);
                    
                    if (_isEnd2End)
                    {
                        results = PostprocessEnd2End(runResults, originalWidth, originalHeight, scale, padX, padY);
                    }
                    else
                    {
                        results = PostprocessClassic(runResults, originalWidth, originalHeight, scale, padX, padY);
                    }

                    Logger.Log($"YOLO26: найдено {results.Count} объектов");
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Ошибка в YOLO26: {ex.Message}");
                }

                return results;
            });
        }

        private (DenseTensor<float> tensor, int originalWidth, int originalHeight, float scale, int padX, int padY) 
            PreprocessImage(Bitmap bitmap)
        {
            int originalWidth = bitmap.Width;
            int originalHeight = bitmap.Height;

            using var ms = new MemoryStream();
            bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            ms.Position = 0;
            using var image = SixLabors.ImageSharp.Image.Load<Rgb24>(ms);

            float scale = Math.Min((float)_inputSize / image.Width, (float)_inputSize / image.Height);
            int newWidth = (int)(image.Width * scale);
            int newHeight = (int)(image.Height * scale);

            image.Mutate(x => x.Resize(newWidth, newHeight));

            var tensor = new DenseTensor<float>(new[] { 1, 3, _inputSize, _inputSize });

            int padX = (_inputSize - newWidth) / 2;
            int padY = (_inputSize - newHeight) / 2;

            for (int y = 0; y < newHeight; y++)
            {
                for (int x = 0; x < newWidth; x++)
                {
                    var pixel = image[x, y];
                    tensor[0, 0, padY + y, padX + x] = pixel.R / 255.0f;
                    tensor[0, 1, padY + y, padX + x] = pixel.G / 255.0f;
                    tensor[0, 2, padY + y, padX + x] = pixel.B / 255.0f;
                }
            }

            return (tensor, originalWidth, originalHeight, scale, padX, padY);
        }

        private List<DetectionResult> PostprocessEnd2End(IDisposableReadOnlyCollection<DisposableNamedOnnxValue> outputs, 
            int originalWidth, int originalHeight, float scale, int padX, int padY)
        {
            var detections = new List<DetectionResult>();

            try
            {
                var output = outputs.First().AsTensor<float>();
                var dims = output.Dimensions;

                // YOLO26 end-to-end output: [batch, num_detections, 6]
                if (dims.Length == 3 && dims[1] > 0 && dims[2] == 6)
                {
                    int numDetections = dims[1];

                    for (int i = 0; i < numDetections; i++)
                    {
                        float x1 = output[0, i, 0];
                        float y1 = output[0, i, 1];
                        float x2 = output[0, i, 2];
                        float y2 = output[0, i, 3];
                        float confidence = output[0, i, 4];
                        int classId = (int)output[0, i, 5];

                        if (confidence < _confidenceThreshold)
                            continue;

                        float left = (x1 - padX) / scale;
                        float top = (y1 - padY) / scale;
                        float right = (x2 - padX) / scale;
                        float bottom = (y2 - padY) / scale;

                        left = Math.Max(0, Math.Min(left, originalWidth));
                        top = Math.Max(0, Math.Min(top, originalHeight));
                        right = Math.Max(0, Math.Min(right, originalWidth));
                        bottom = Math.Max(0, Math.Min(bottom, originalHeight));

                        float width = right - left;
                        float height = bottom - top;

                        if (width <= 0 || height <= 0)
                            continue;

                        string className = GetClassName(classId);

                        detections.Add(new DetectionResult
                        {
                            ClassName = className,
                            Confidence = confidence,
                            BoundingBox = new Rectangle(
                                (int)left,
                                (int)top,
                                (int)width,
                                (int)height
                            ),
                            SignalState = className.Contains("светофор") ? "красный" : null
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Ошибка в PostprocessEnd2End: {ex.Message}");
            }

            return detections;
        }

        private List<DetectionResult> PostprocessClassic(IDisposableReadOnlyCollection<DisposableNamedOnnxValue> outputs,
            int originalWidth, int originalHeight, float scale, int padX, int padY)
        {
            var detections = new List<DetectionResult>();

            try
            {
                var output = outputs.First().AsTensor<float>();
                var dims = output.Dimensions;

                if (dims.Length == 3 && dims[0] == 1 && dims[1] > 4)
                {
                    int numPredictions = dims[2];
                    int numClasses = dims[1] - 4;

                    for (int i = 0; i < numPredictions; i++)
                    {
                        float maxConfidence = 0;
                        int bestClassId = -1;

                        for (int j = 4; j < dims[1]; j++)
                        {
                            float conf = output[0, j, i];
                            if (conf > maxConfidence)
                            {
                                maxConfidence = conf;
                                bestClassId = j - 4;
                            }
                        }

                        if (maxConfidence < _confidenceThreshold || bestClassId < 0)
                            continue;

                        float cx = output[0, 0, i];
                        float cy = output[0, 1, i];
                        float w = output[0, 2, i];
                        float h = output[0, 3, i];

                        float left = ((cx * _inputSize - padX) / scale - (w * _inputSize) / (2 * scale));
                        float top = ((cy * _inputSize - padY) / scale - (h * _inputSize) / (2 * scale));
                        float right = ((cx * _inputSize - padX) / scale + (w * _inputSize) / (2 * scale));
                        float bottom = ((cy * _inputSize - padY) / scale + (h * _inputSize) / (2 * scale));

                        left = Math.Max(0, Math.Min(left, originalWidth));
                        top = Math.Max(0, Math.Min(top, originalHeight));
                        right = Math.Max(0, Math.Min(right, originalWidth));
                        bottom = Math.Max(0, Math.Min(bottom, originalHeight));

                        float width = right - left;
                        float height = bottom - top;

                        if (width <= 0 || height <= 0)
                            continue;

                        string className = GetClassName(bestClassId);

                        detections.Add(new DetectionResult
                        {
                            ClassName = className,
                            Confidence = maxConfidence,
                            BoundingBox = new Rectangle(
                                (int)left,
                                (int)top,
                                (int)width,
                                (int)height
                            ),
                            SignalState = className.Contains("светофор") ? "красный" : null
                        });
                    }

                    detections = NonMaxSuppression(detections, 0.5f);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Ошибка в PostprocessClassic: {ex.Message}");
            }

            return detections;
        }

        private string GetClassName(int classId)
        {
            if (classId >= 0 && classId < _cocoClasses.Length)
            {
                string engName = _cocoClasses[classId];
                
                return engName switch
                {
                    "person" => "человек",
                    "bicycle" => "велосипед",
                    "car" => "машина",
                    "motorcycle" => "мотоцикл",
                    "airplane" => "самолет",
                    "bus" => "автобус",
                    "train" => "поезд",
                    "truck" => "грузовик",
                    "boat" => "лодка",
                    "traffic light" => "светофор",
                    "fire hydrant" => "пожарный гидрант",
                    "stop sign" => "знак стоп",
                    "parking meter" => "паркомат",
                    "bench" => "скамейка",
                    _ => engName
                };
            }
            
            return classId < _targetClasses.Length ? _targetClasses[classId] : $"класс_{classId}";
        }

        private List<DetectionResult> NonMaxSuppression(List<DetectionResult> detections, float iouThreshold)
        {
            if (detections.Count == 0) return detections;

            var sorted = detections.OrderByDescending(d => d.Confidence).ToList();
            var result = new List<DetectionResult>();

            while (sorted.Count > 0)
            {
                var best = sorted[0];
                result.Add(best);
                sorted.RemoveAt(0);
                sorted.RemoveAll(d => ComputeIoU(best.BoundingBox, d.BoundingBox) > iouThreshold);
            }

            return result;
        }

        private float ComputeIoU(Rectangle a, Rectangle b)
        {
            int x1 = Math.Max(a.Left, b.Left);
            int y1 = Math.Max(a.Top, b.Top);
            int x2 = Math.Min(a.Right, b.Right);
            int y2 = Math.Min(a.Bottom, b.Bottom);

            int intersection = Math.Max(0, x2 - x1) * Math.Max(0, y2 - y1);
            int union = a.Width * a.Height + b.Width * b.Height - intersection;

            return union == 0 ? 0 : (float)intersection / union;
        }

        public void Dispose()
        {
            _session?.Dispose();
        }
    }
}
