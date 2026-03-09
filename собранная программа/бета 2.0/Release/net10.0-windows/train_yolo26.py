#!/usr/bin/env python3
# -*- coding: utf-8 -*-

"""
Скрипт для обучения YOLO26 на железнодорожных объектах
Автор: OSTU Team
Дата: 2026
"""

import os
import sys
import argparse
import yaml
import torch
from pathlib import Path
from ultralytics import YOLO

def setup_environment():
    """Проверяет наличие GPU и настраивает окружение"""
    print("=" * 60)
    print("🚂 НАСТРОЙКА ОКРУЖЕНИЯ ДЛЯ YOLO26")
    print("=" * 60)
    
    if torch.cuda.is_available():
        print(f"✅ GPU доступен: {torch.cuda.get_device_name(0)}")
        device = 'cuda'
    else:
        print("⚠️ GPU не найден, обучение на CPU")
        device = 'cpu'
    
    print(f"📦 PyTorch версия: {torch.__version__}")
    print(f"📦 Ultralytics версия: {YOLO.__module__}")
    
    return device

def check_dataset(data_yaml):
    """Проверяет структуру датасета"""
    print("\n" + "=" * 60)
    print("📁 ПРОВЕРКА ДАТАСЕТА")
    print("=" * 60)
    
    if not os.path.exists(data_yaml):
        print(f"❌ Файл {data_yaml} не найден!")
        return False
    
    with open(data_yaml, 'r', encoding='utf-8') as f:
        data = yaml.safe_load(f)
    
    print(f"📄 Классы: {data['names']}")
    print(f"📊 Количество классов: {data['nc']}")
    
    # Проверяем пути
    for split in ['train', 'val']:
        if split in data:
            path = Path(data[split])
            if not path.exists():
                print(f"⚠️ Путь к {split} не найден: {path}")
    
    return True

def train_yolo26(data_yaml, epochs=100, imgsz=640, batch=16, model_size='n', device='cpu'):
    """
    Обучает модель YOLO26
    
    Параметры:
        data_yaml: путь к dataset.yaml
        epochs: количество эпох
        imgsz: размер изображения
        batch: размер батча
        model_size: размер модели ('n' - nano, 's' - small, 'm' - medium, 'l' - large, 'x' - xlarge)
        device: устройство ('cpu' или 'cuda')
    """
    print("\n" + "=" * 60)
    print("🏋️‍♂️ НАЧАЛО ОБУЧЕНИЯ YOLO26")
    print("=" * 60)
    
    # Выбираем модель
    model_name = f'yolo26{model_size}.pt'
    print(f"📥 Загрузка модели: {model_name}")
    
    try:
        model = YOLO(model_name)
    except Exception as e:
        print(f"❌ Ошибка загрузки модели: {e}")
        print("   Скачиваю модель...")
        model = YOLO(f'yolo26{model_size}.yaml').load(f'yolo26{model_size}.pt')
    
    print(f"✅ Модель загружена")
    
    print(f"\n⚙️ ПАРАМЕТРЫ ОБУЧЕНИЯ:")
    print(f"   Датасет: {data_yaml}")
    print(f"   Эпох: {epochs}")
    print(f"   Размер: {imgsz}")
    print(f"   Батч: {batch}")
    print(f"   Устройство: {device}")
    
    # Запуск обучения с оптимизациями YOLO26
    results = model.train(
        data=data_yaml,
        epochs=epochs,
        imgsz=imgsz,
        batch=batch,
        device=device,
        workers=4,
        patience=50,
        save=True,
        project='runs/train',
        name='railway_vision_yolo26',
        exist_ok=True,
        pretrained=True,
        optimizer='MuSGD',  # YOLO26 использует MuSGD оптимизатор
        verbose=True,
        seed=42,
        deterministic=True,
        single_cls=False,
        cos_lr=True,  # Cosine learning rate для YOLO26
        close_mosaic=10,
        amp=True,
        fraction=1.0,
        profile=False,
        freeze=None,
        multi_scale=False,
        overlap_mask=True,
        mask_ratio=4,
        dropout=0.0,
        val=True,
        plots=True,
        lr0=0.01,
        lrf=0.01,
        momentum=0.937,
        weight_decay=0.0005,
        warmup_epochs=3,
        warmup_momentum=0.8,
        warmup_bias_lr=0.1,
        box=7.5,
        cls=0.5,
        dfl=1.5,
        label_smoothing=0.0,
        nbs=64
    )
    
    print("\n✅ ОБУЧЕНИЕ ЗАВЕРШЕНО!")
    
    # Сохраняем лучшую модель
    best_pt = Path('runs/train/railway_vision_yolo26/weights/best.pt')
    if best_pt.exists():
        print(f"📦 Лучшая модель: {best_pt}")
        
        # Копируем в папку Models
        import shutil
        target_pt = Path('Models/custom_yolo26.pt')
        target_pt.parent.mkdir(exist_ok=True)
        shutil.copy(best_pt, target_pt)
        print(f"📋 Модель скопирована в: {target_pt}")
        
        # Конвертируем в ONNX для YOLO26 (end-to-end режим)
        print("\n🔄 Конвертация в ONNX (end-to-end)...")
        model.export(format='onnx', imgsz=imgsz, simplify=True, nms=False)  # end2end
        onnx_path = Path('runs/train/railway_vision_yolo26/weights/best.onnx')
        if onnx_path.exists():
            target_onnx = Path('Models/yolo26n.onnx')
            shutil.copy(onnx_path, target_onnx)
            print(f"✅ ONNX модель сохранена: {target_onnx}")
    
    return results

def main():
    parser = argparse.ArgumentParser(description='Обучение YOLO26 для железнодорожных объектов')
    parser.add_argument('--data', type=str, default='CustomData/dataset.yaml', help='Путь к dataset.yaml')
    parser.add_argument('--epochs', type=int, default=100, help='Количество эпох')
    parser.add_argument('--imgsz', type=int, default=640, help='Размер изображения')
    parser.add_argument('--batch', type=int, default=16, help='Размер батча')
    parser.add_argument('--model', type=str, default='n', choices=['n', 's', 'm', 'l', 'x'], 
                       help='Размер модели (n, s, m, l, x)')
    
    args = parser.parse_args()
    
    print("""
    ╔══════════════════════════════════════════════════════════════╗
    ║             OSTU VISION - YOLO26 TRAINING                ║
    ║                  Самая новая версия 2026                     ║
    ╚══════════════════════════════════════════════════════════════╝
    """)
    
    device = setup_environment()
    
    if not check_dataset(args.data):
        return
    
    train_yolo26(
        data_yaml=args.data,
        epochs=args.epochs,
        imgsz=args.imgsz,
        batch=args.batch,
        model_size=args.model,
        device=device
    )

if __name__ == "__main__":
    main()