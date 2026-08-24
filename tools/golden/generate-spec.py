#!/usr/bin/env python3
"""Снимает эталон нормировочной модели с исходной таблицы.

Значения берутся из кэша вычислений в самом xlsx — то есть посчитаны Excel,
а не этим скриптом. Скрипт только раскладывает столбцы-помещения по случаям.

    pip install openpyxl
    python tools/golden/generate-spec.py

Пересобирать нужно, только если поменялся data/source/lighting-electrical.xlsx.
"""

import json
import os
import sys

try:
    import openpyxl
except ImportError:                                     # pragma: no cover
    sys.exit("Нужен openpyxl: pip install openpyxl")

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SOURCE = os.path.join(ROOT, "data", "source", "lighting-electrical.xlsx")
TARGET = os.path.join(ROOT, "tests", "Domeo.Calculators.Tests", "golden", "spec.json")

ROOM_KEYS = {
    "Прихожая": "hallway",
    "Санузел": "wc",
    "Ванная комната": "bathroom",
    "Гостиная": "living",
    "Кухня-гостиная": "kitchenLiving",
    "Кухня": "kitchen",
    "Спальня": "bedroom",
    "Детская": "nursery",
    "Гардеробная": "wardrobe",
    "Лоджия": "balcony",
}

COMFORT = {"Эконом": "Economy", "Стандарт": "Standard", "Премиум": "Premium"}

# строка листа «Калькулятор» -> поле входа
INPUT_ROWS = [
    (4, "tv", "int"), (5, "computers", "int"),
    (6, "floorLamps", "int"), (7, "deskLamps", "int"),
    (8, "vacuum", "bool"), (9, "washingMachine", "bool"), (10, "dryer", "bool"),
    (11, "oven", "bool"), (12, "hob", "bool"), (13, "hood", "bool"),
    (14, "dishwasher", "bool"), (15, "fridge", "bool"), (16, "microwave", "bool"),
    (17, "smallAppliances", "bool"),
    (19, "track", "bool"), (20, "downlights", "bool"), (21, "pendants", "bool"),
    (22, "chandelier", "bool"), (23, "sconces", "bool"), (24, "ledStrip", "bool"),
]

# строка результата -> поле спецификации
RESULT_ROWS = [
    (26, "normalSockets"), (27, "wetSockets"), (28, "tvSockets"), (29, "internetSockets"),
    (30, "singleSwitches"), (31, "doubleSwitches"), (32, "passThroughSwitches"),
    (33, "crossSwitches"), (34, "frames"),
    (35, "trackMeters"), (36, "trackFixtures"), (37, "downlights"), (38, "ledMeters"),
    (39, "chandeliers"), (40, "pendants"), (41, "sconces"),
]

COLUMNS = ["B", "C", "D", "E", "F", "G", "H"]
TOTAL_COLUMN = "I"


def main():
    book = openpyxl.load_workbook(SOURCE, data_only=True)
    sheet = book["Калькулятор"]

    cases = []
    for column in COLUMNS:
        label = sheet[f"{column}2"].value
        if label is None:
            continue

        if label not in ROOM_KEYS:
            sys.exit(f"Тип помещения не сопоставлен с ключом: {label}")

        area = sheet[f"{column}3"].value or 0
        comfort_cell = sheet[f"{column}18"].value

        room = {
            "roomType": ROOM_KEYS[label],
            "area": area,
            "comfort": COMFORT.get(comfort_cell) if comfort_cell else None,
            "windows": 1,
        }
        for row, field, kind in INPUT_ROWS:
            value = sheet[f"{column}{row}"].value
            room[field] = (value == "Да") if kind == "bool" else int(value or 0)

        expected = {}
        for row, field in RESULT_ROWS:
            value = sheet[f"{column}{row}"].value
            if value is None:
                sys.exit(f"В {column}{row} нет посчитанного значения — открой файл в Excel и сохрани")
            expected[field] = int(value)

        cases.append({"name": f"{label}, {area} м²", "input": room, "expected": expected})

    total = {}
    for row, field in RESULT_ROWS:
        total[field] = int(sheet[f"{TOTAL_COLUMN}{row}"].value or 0)

    golden = {
        "source": "data/source/lighting-electrical.xlsx",
        "note": "Ожидания — кэш вычислений Excel из самого файла, не арифметика этого скрипта.",
        "cases": cases,
        "total": total,
    }

    os.makedirs(os.path.dirname(TARGET), exist_ok=True)
    with open(TARGET, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(golden, handle, ensure_ascii=False, indent=2)
        handle.write("\n")

    print(f"{len(cases)} случаев -> {os.path.relpath(TARGET, ROOT)}")


if __name__ == "__main__":
    main()
