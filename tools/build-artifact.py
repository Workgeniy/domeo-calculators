# Пересобирает артефакт из живой страницы: та же вёрстка и стили,
# но слой API заменён локальным расчётом (артефакт работает без сервера).
import io, re, json

SRC = r'C:\Users\Alex\Проекты Domeo\domeo-calculators\src\Domeo.Calculators.Web\wwwroot\index.html'
DATA = r'C:\Users\Alex\Проекты Domeo\domeo-calculators\data'
DST = 'calc-preview.html'

live = io.open(SRC, encoding='utf-8').read()
style = re.search(r'<style>.*?</style>', live, re.S).group(0)
markup = re.search(r'<body>(.*?)<script>', live, re.S).group(1).strip()
script = re.search(r'<script>(.*?)</script>', live, re.S).group(1)

L = json.load(io.open(DATA + r'\lighting.json', encoding='utf-8'))
E = json.load(io.open(DATA + r'\outlets.json', encoding='utf-8'))

# Справочник в том же виде, в каком его отдаёт GET /api/reference —
# тогда весь код отрисовки ниже остаётся без изменений.
ref = {
    "lighting": {
        "roomTypes": L["roomTypes"],
        "wallColors": L["wallColors"],
        "lampTypes": L["lampTypes"],
        "fixturePresets": L["fixturePresets"],
    },
    "electrical": {
        "roomOrder": list(E["roomPresets"].keys()),
        "roomPresets": [{"key": k, "preset": v} for k, v in E["roomPresets"].items()],
        "priceTiers": [{"key": k, "tier": v} for k, v in E["priceTiers"].items()],
        "defaultRooms": {
            k: {
                "enabled": v["enabledByDefault"], "area": v["defaultArea"], "count": 1,
                "outlets": int((v["outletsMin"] + v["outletsMax"]) / 2 + 0.5),
                "switches": v["switches"], "passthrough": v["passthrough"],
                "usb": v["usb"], "tv": v["tv"], "internet": v["internet"],
            } for k, v in E["roomPresets"].items()
        },
    },
}

# Локальные аналоги эндпоинтов. Формулы — та же дословная транскрипция
# оригинала calcal.ru, что и в C#-порте; ключи ответа совпадают с API.
local = """
const REF = __REF__;

function localLighting(i) {
  const room = REF.lighting.roomTypes.find(r => r.id === i.roomType);
  const wall = REF.lighting.wallColors.find(w => w.id === i.wallColor);
  const lamp = REF.lighting.lampTypes.find(l => l.id === i.lampType);
  if (!room || !wall || !lamp || i.area <= 0 || i.height <= 0 || i.fixtureWatt <= 0) {
    return { ok: false, reason: "Площадь, высота и мощность должны быть больше нуля." };
  }
  let hc = 1;
  if (i.height > 2.7 && i.height <= 3) hc = 1.2;
  else if (i.height > 3 && i.height <= 3.5) hc = 1.5;
  else if (i.height > 3.5) hc = 2;

  const lumens = room.lux * i.area * hc * (0.7 / wall.coeff);
  const fixtureLumen = i.fixtureWatt * lamp.efficiency;
  const count = Math.ceil(lumens / fixtureLumen);
  const totalWatt = count * i.fixtureWatt;
  const kwh = totalWatt / 1e3 * i.hoursPerDay * 30;

  return { ok: true, result: {
    requiredLux: room.lux, requiredLumens: Math.round(lumens),
    fixtureLumen: Math.round(fixtureLumen), fixtureCount: count,
    fixtureWatt: i.fixtureWatt, totalWatt: totalWatt,
    monthlyKwhFormatted: kwh.toFixed(1), monthlyCostFormatted: (kwh * i.tariff).toFixed(0),
    heightCoeff: hc, roomName: room.name,
    areaPerFixtureFormatted: (i.area / count).toFixed(1) } };
}

function localElectrical(req) {
  const price = (REF.electrical.priceTiers.find(t => t.key === req.tier) || {}).tier;
  if (!price) return { ok: false, reason: "Неизвестный ценовой уровень." };
  const labels = {};
  for (const p of REF.electrical.roomPresets) labels[p.key] = p.preset.label;

  let o = 0, s = 0, u = 0, tv = 0, net = 0, pts = 0, cab = 0, cost = 0;
  const rows = [];

  for (const key of REF.electrical.roomOrder) {
    const r = req.rooms[key];
    if (!r || !r.enabled) continue;
    const n = r.count || 1;
    const outlets = r.outlets * n, sw = r.switches * n, pass = r.passthrough * n;
    const usb = r.usb * n, t = r.tv * n, inet = r.internet * n;
    const points = outlets + sw + pass + usb + t + inet;
    const cable = Math.ceil(points * (Math.sqrt(r.area) + 1.5));
    const c = outlets * price.outlet + (sw + pass) * price.switch + usb * price.usbOutlet
            + t * price.tvOutlet + inet * price.internetOutlet
            + points * price.frame1 + points * price.box;

    o += outlets; s += sw + pass; u += usb; tv += t; net += inet;
    pts += points; cab += cable; cost += c;
    rows.push({ key: key, label: labels[key], outlets: outlets, switches: sw + pass,
                usb: usb, tv: t, internet: inet, cable: cable, cost: c });
  }

  return { ok: true, result: {
    totalOutlets: o, totalSwitches: s, totalUsb: u, totalTv: tv, totalInternet: net,
    totalBoxes: pts, totalFrames: pts, totalCableMeters: cab, totalCost: cost,
    roomBreakdown: rows, tierLabel: price.label } };
}

async function post(url, body) {
  return url.indexOf("lighting") !== -1 ? localLighting(body) : localElectrical(body);
}
"""
local = local.replace('__REF__', json.dumps(ref, ensure_ascii=False))

# выкидываем сетевой post и подменяем загрузку справочников на локальную
script = re.sub(r'async function post\(url, body\) \{.*?\n\}\n', '', script, flags=re.S)
script = re.sub(r'  try \{\n    const res = await fetch\("api/reference"\);.*?\n  \}\n',
                '  ref = REF;\n', script, flags=re.S)
script = local + script

markup = markup.replace('<span class="dot"></span> Считает бэкенд',
                        '<span class="dot"></span> Расчёт в браузере')
markup = markup.replace(
    'электрофурнитура на квартиру. Расчёт по нормам, результат сразу.',
    'электрофурнитура на квартиру. Автономная копия — рабочая версия считает на сервере.')

out = '<title>Свет и электрика</title>\n' + style + '\n' + markup + '\n<script>\n' + script + '\n</script>\n'
io.open(DST, 'w', encoding='utf-8', newline='\n').write(out)
print('собрано:', len(out), 'символов')
