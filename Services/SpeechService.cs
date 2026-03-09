using System;
using System.Collections.Generic;
using System.Speech.Synthesis; // Для синтеза речи (SpeechSynthesizer)
using OSTUVision.Models;   // Для DetectionResult

namespace OSTUVision.Services // Пространство имен для сервисов
{
    public class SpeechService
    {
        private readonly SpeechSynthesizer _synthesizer; // Синтезатор речи (основной компонент)

        public SpeechService()
        {
            try
            {
                _synthesizer = new SpeechSynthesizer(); // Создаем объект синтезатора речи
                _synthesizer.SetOutputToDefaultAudioDevice();  // Направляем вывод на звуковую карту (динамики/наушники)
                _synthesizer.Volume = 100; // Громкость 100% (максимальная)
                _synthesizer.Rate = 0;  // Скорость речи: 0 = нормальная, >0 быстрее, <0 медленнее
            }
            catch
            {
                // Если нет звуковой карты или синтезатор не работает
                _synthesizer = null; // Отключаем озвучивание
            }
        }

        public void SpeakDetections(List<DetectionResult> detections)
        {
            if (_synthesizer == null) return; // Если синтезатор не инициализирован - ничего не делаем

            if (detections == null || detections.Count == 0)  // Если список пустой или null - сообщаем, что ничего не найдено
            {
                _synthesizer.SpeakAsync("Объектов не обнаружено.");
                return;
            }

            string message = BuildMessage(detections); // Формируем сообщение из результатов
            _synthesizer.SpeakAsync(message);  // Произносим
        }

        private string BuildMessage(List<DetectionResult> detections)
        {
            var parts = new List<string>(); // Список частей сообщения
            int lightCount = 0, trainCount = 0, anomalyCount = 0;

            foreach (var d in detections) // Подсчитываем количество объектов каждого типа
            {
                if (d.ClassName.Contains("светофор") || d.ClassName.Contains("traffic light")) // Светофоры (русский и английский варианты)
                {
                    lightCount++; // Если известно состояние сигнала - добавляем детальную информацию
                    if (!string.IsNullOrEmpty(d.SignalState))
                        parts.Add($"светофор показывает {d.SignalState}");
                }
                else if (d.ClassName.Contains("поезд") || d.ClassName.Contains("train"))  // Поезда (русский и английский)
                    trainCount++;
                else
                    anomalyCount++;
            }
// Формируем итоговое сообщение (в обратном порядке приоритета)
            // Сначала добавляем статистику по нестандартным объектам
            if (lightCount > 0) parts.Insert(0, $"обнаружено {lightCount} светофоров");
            if (trainCount > 0) parts.Insert(0, $"обнаружено {trainCount} поездов");
            if (anomalyCount > 0) parts.Insert(0, $"обнаружено {anomalyCount} нестандартных объектов");

            return string.Join(". ", parts) + ".";
        }
    }
}