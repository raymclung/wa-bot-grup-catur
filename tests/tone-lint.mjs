// ============================================================
//  Tone lint — jaga nada respons bot tetap RAMAH & TIDAK MENYALAHKAN.
//  Memindai field respons di brain/config/config.json terhadap kata "keras/menuduh".
//  rulesText DIKECUALIKAN (itu aturan, bukan teguran personal).
//  Jalankan: node tests/tone-lint.mjs   (exit 1 bila ada pelanggaran)
//  Pedoman lengkap: docs/QC-respons.md
// ============================================================
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join, resolve } from 'node:path';

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const cfgRaw = readFileSync(join(ROOT, 'brain', 'config', 'config.json'), 'utf8').replace(/^﻿/, '');
const cfg = JSON.parse(cfgRaw);

// Field respons-ke-user yang harus berdada ramah (rulesText sengaja TIDAK termasuk).
const fields = [
  ['warningMessage', cfg.warningMessage],
  ['floodWarningMessage', cfg.floodWarningMessage],
  ['welcomeMessage', cfg.welcomeMessage],
  ['probation.message', cfg.probation?.message],
  ['mediaModeration.message', cfg.mediaModeration?.message],
  ['puzzle.tryHarderMessage', cfg.puzzle?.tryHarderMessage],
];

// Kata/pola yang menandakan nada menyalahkan / keras (case-insensitive).
// "salah" hanya dianggap pelanggaran sebagai kata utuh (hindari "menyalahkan" untuk jawaban puzzle).
const harsh = [
  /\bdilarang\b/i, /\bmelanggar\b/i, /\btidak boleh\b/i, /\bkamu harus\b/i,
  /\bawas\b/i, /\bhukuman\b/i, /\bdiperingatkan\b/i, /\bsanksi\b/i,
  /\bsalah\b/i, /DIHAPUS/, /\bbodoh\b/i, /\bjangan ulangi\b/i,
];

let violations = 0;
console.log('=== TONE LINT (nada respons bot) ===');
for (const [name, val] of fields) {
  if (!val) { console.log(`  SKIP  ${name} (kosong/tak ada)`); continue; }
  const hits = harsh.filter((re) => re.test(val)).map((re) => re.source);
  if (hits.length) { violations++; console.log(`  FAIL  ${name}: mengandung nada keras -> ${hits.join(', ')}`); }
  else console.log(`  OK    ${name}`);
}

console.log(`\n=== ${violations === 0 ? 'LULUS — nada ramah terjaga' : violations + ' field perlu diperhalus (lihat docs/QC-respons.md)'} ===`);
process.exit(violations === 0 ? 0 : 1);
