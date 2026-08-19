// Генератор golden-векторов.
//
// Формулы ниже — ДОСЛОВНАЯ транскрипция минифицированного кода calcal.ru
// (чанки 387cb267058b45ea.js и 224b6a5bed7a0453.js, снято 19.08.2026).
// Намеренно сохранены все их особенности, включая мёртвое `4*sqrt(area)/4`
// и toFixed, возвращающий строку. Ничего не «чиним» — это эталон.

import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = join(dirname(fileURLToPath(import.meta.url)), '..', '..');
const lightData = JSON.parse(readFileSync(join(root, 'data', 'lighting.json'), 'utf8'));
const outData = JSON.parse(readFileSync(join(root, 'data', 'outlets.json'), 'utf8'));

// ---------- освещённость: оригинальный useMemo ----------
function calcLighting(area, height, roomId, wallId, lampId, fixtureWatt, hoursPerDay, tariff) {
  const t = lightData.roomTypes.find(x => x.id === roomId);
  const r = lightData.wallColors.find(x => x.id === wallId);
  const s = lightData.lampTypes.find(x => x.id === lampId);
  if (!t || !r || !s || area <= 0 || height <= 0 || fixtureWatt <= 0) return null;
  let l = 1;
  height > 2.7 && height <= 3 ? l = 1.2 : height > 3 && height <= 3.5 ? l = 1.5 : height > 3.5 && (l = 2);
  const d = 1 / r.coeff;
  const u = t.lux * area * l * (d / (1 / 0.7));
  const f = fixtureWatt * s.efficiency;
  const p = Math.ceil(u / f);
  const h = p * fixtureWatt;
  const y = h / 1e3 * hoursPerDay * 30;
  const j = y * tariff;
  return {
    requiredLux: t.lux,
    requiredLumens: Math.round(u),
    fixtureLumen: Math.round(f),
    fixtureCount: p,
    fixtureWatt: fixtureWatt,
    totalWatt: h,
    monthlyKwh: y.toFixed(1),
    monthlyCost: j.toFixed(0),
    heightCoeff: l,
    roomName: t.name,
  };
}

// ---------- электрофурнитура: оригинальный useEffect ----------
function calcElectrical(rooms, tierId) {
  const e = outData.priceTiers[tierId];
  const r = outData.roomPresets;
  let t = 0, s = 0, l = 0, n = 0, d = 0, c = 0, m = 0, h = 0, p = 0;
  const x = [];
  Object.entries(rooms).forEach(([key, i]) => {
    if (!i.enabled) return;
    const o = i.count || 1;
    const u = i.outlets * o, f = i.switches * o, b = i.passthrough * o;
    const g = i.usb * o, v = i.tv * o, y = i.internet * o;
    const j = u + f + b + g + v + y;
    const w = Math.ceil(j * (4 * Math.sqrt(i.area) / 4 + 1.5));
    const N = u * e.outlet + (f + b) * e.switch + g * e.usbOutlet + v * e.tvOutlet
      + y * e.internetOutlet + j * e.frame1 + j * e.box;
    t += u; s += f + b; l += g; n += v; d += y; c += j; m += j; p += w; h += N;
    x.push({
      key: key, label: r[key].label, count: o, outlets: u, switches: f + b,
      usb: g, tv: v, internet: y, cable: w, cost: N,
    });
  });
  return {
    totalOutlets: t, totalSwitches: s, totalUsb: l, totalTv: n, totalInternet: d,
    totalBoxes: c, totalFrames: m, totalCableMeters: p, totalCost: h,
    roomBreakdown: x, tierLabel: e.label,
  };
}

// дефолтное состояние комнат — как в оригинальном useState
function defaultRooms() {
  const out = {};
  for (const [k, s] of Object.entries(outData.roomPresets)) {
    out[k] = {
      enabled: s.enabledByDefault,
      area: s.defaultArea,
      count: 1,
      outlets: Math.round((s.outletsMin + s.outletsMax) / 2),
      switches: s.switches,
      passthrough: s.passthrough,
      usb: s.usb,
      tv: s.tv,
      internet: s.internet,
    };
  }
  return out;
}

// ---------- матрица случаев: освещённость ----------
const L = [];
function push(name, a, hgt, rm, wl, lp, w, hrs, tf) {
  L.push({
    name: name,
    input: { area: a, height: hgt, roomType: rm, wallColor: wl, lampType: lp, fixtureWatt: w, hoursPerDay: hrs, tariff: tf },
    expected: calcLighting(a, hgt, rm, wl, lp, w, hrs, tf),
  });
}

push('дефолт', 18, 2.7, 'living', 'light', 'led', 12, 6, 6.5);
for (const h of [2.0, 2.7, 2.71, 2.8, 3.0, 3.01, 3.2, 3.5, 3.51, 4.0, 6.0]) {
  push('высота ' + h, 18, h, 'living', 'light', 'led', 12, 6, 6.5);
}
for (const r of lightData.roomTypes) push('комната ' + r.id, 18, 2.7, r.id, 'light', 'led', 12, 6, 6.5);
for (const w of lightData.wallColors) push('стены ' + w.id, 18, 2.7, 'living', w.id, 'led', 12, 6, 6.5);
for (const l of lightData.lampTypes) push('лампа ' + l.id, 18, 2.7, 'living', 'light', l.id, 12, 6, 6.5);
for (const a of [1, 1.5, 9.5, 50, 120.5, 500]) push('площадь ' + a, a, 2.7, 'office', 'medium', 'led', 12, 6, 6.5);
for (const w of [1, 7, 36, 100, 500]) push('мощность ' + w, 18, 2.7, 'living', 'light', 'led', w, 6, 6.5);
for (const p of lightData.fixturePresets) push('пресет ' + p.name, 18, 2.7, 'kitchen', 'light', 'led', p.watt, 6, 6.5);
for (const hr of [1, 5, 12, 24]) push('часов ' + hr, 18, 2.7, 'living', 'light', 'led', 12, hr, 6.5);
for (const tf of [0, 0.1, 5, 6.5, 99.9]) push('тариф ' + tf, 18, 2.7, 'living', 'light', 'led', 12, 6, tf);

push('максимум', 500, 6.0, 'office', 'dark', 'incandescent', 500, 24, 99.9);
push('минимум', 1, 2.0, 'bedroom', 'light', 'led', 500, 1, 0);
push('невалид: площадь 0', 0, 2.7, 'living', 'light', 'led', 12, 6, 6.5);
push('невалид: высота 0', 18, 0, 'living', 'light', 'led', 12, 6, 6.5);
push('невалид: мощность 0', 18, 2.7, 'living', 'light', 'led', 0, 6, 6.5);
push('невалид: тип комнаты', 18, 2.7, 'nope', 'light', 'led', 12, 6, 6.5);

// прицельно в округление toFixed
const roundCases = [[5, 1], [15, 1], [25, 1], [5, 3], [12, 5], [7, 5], [35, 1], [45, 1], [55, 1], [5, 7]];
for (const rc of roundCases) {
  push('округление W=' + rc[0] + ' h=' + rc[1], 18, 2.7, 'living', 'light', 'led', rc[0], rc[1], 6.5);
}

// ---------- матрица случаев: электрофурнитура ----------
const E = [];
function pushE(name, rooms, tier) {
  E.push({ name: name, input: { rooms: rooms, tier: tier }, expected: calcElectrical(rooms, tier) });
}

for (const tier of Object.keys(outData.priceTiers)) pushE('дефолт / ' + tier, defaultRooms(), tier);

const all = defaultRooms();
for (const k of Object.keys(all)) all[k].enabled = true;
pushE('все комнаты включены', all, 'medium');

const none = defaultRooms();
for (const k of Object.keys(none)) none[k].enabled = false;
pushE('все комнаты выключены', none, 'budget');

const one = defaultRooms();
for (const k of Object.keys(one)) one[k].enabled = (k === 'kitchen');
pushE('только кухня', one, 'premium');

const multi = defaultRooms();
multi.bedroom.count = 3;
multi.nursery.enabled = true;
multi.nursery.count = 2;
pushE('несколько одинаковых комнат', multi, 'medium');

const zeroCount = defaultRooms();
zeroCount.kitchen.count = 0; // count||1 -> 1
pushE('count = 0 (падает в 1)', zeroCount, 'budget');

const maxed = defaultRooms();
for (const k of Object.keys(maxed)) {
  Object.assign(maxed[k], { enabled: true, area: 200, count: 10, outlets: 20, switches: 10, passthrough: 10, usb: 10, tv: 5, internet: 5 });
}
pushE('всё по максимуму', maxed, 'premium');

const zeroed = defaultRooms();
for (const k of Object.keys(zeroed)) {
  Object.assign(zeroed[k], { enabled: true, area: 1, outlets: 0, switches: 0, passthrough: 0, usb: 0, tv: 0, internet: 0 });
}
pushE('все точки обнулены', zeroed, 'budget');

// площади с нецелым корнем — проверяем Math.ceil в метраже кабеля
for (const a of [1, 2, 3, 5, 7, 10, 12.5, 17, 99, 200]) {
  const r = defaultRooms();
  for (const k of Object.keys(r)) r[k].enabled = (k === 'living');
  r.living.area = a;
  pushE('кабель: площадь ' + a, r, 'budget');
}

// ---------- эталон для toFixed / Math.round ----------
// Значения берём у самого движка, а не по памяти: именно здесь наивное
// округление в C# и расходится с JS.
const toFixedValues = [
  0, 0.5, 1.005, 1.05, 1.15, 1.25, 1.35, 1.45, 1.55, 1.65, 1.75, 1.85, 1.95,
  2.5, 2.675, 6.479999999999999, 8.775, 9.75, 10.725, 0.0001, 1234.5678,
  1e-7, 123456789.987654, 0.1 + 0.2,
];
const F = [];
for (const v of toFixedValues) {
  for (const d of [0, 1, 2, 3]) {
    F.push({ value: v, digits: d, expected: v.toFixed(d) });
    // при v === 0 знак ничего не меняет, дубль в наборе не нужен
    if (v !== 0) F.push({ value: -v, digits: d, expected: (-v).toFixed(d) });
  }
}

const roundValues = [
  0, 0.5, 1.5, 2.5, -0.5, -1.5, -2.5, 1.4999999999999999, 0.49999999999999994,
  2699.9999999999995, 2700, 1200, 4199.999999999999, -3.5,
];
// 1.4999999999999999 и 1.5 — один и тот же double, дубли отбрасываем
const R = [...new Set(roundValues)].map(v => ({ value: v, expected: Math.round(v) }));

const dst = join(root, 'tests', 'Domeo.Calculators.Tests', 'golden');
mkdirSync(dst, { recursive: true });
writeFileSync(join(dst, 'lighting.json'), JSON.stringify(L, null, 2), 'utf8');
writeFileSync(join(dst, 'electrical.json'), JSON.stringify(E, null, 2), 'utf8');
writeFileSync(join(dst, 'jsnumber.json'), JSON.stringify({ toFixed: F, round: R }, null, 2), 'utf8');
console.log('lighting: ' + L.length + ' cases');
console.log('electrical: ' + E.length + ' cases');
console.log('jsnumber: ' + F.length + ' toFixed + ' + R.length + ' round');
