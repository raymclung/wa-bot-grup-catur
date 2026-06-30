/*
 * ============================================================
 *  SCRIPT PAIRING MANUAL  -  Liga Catur / WAbot.asmx
 * ============================================================
 *  Tugas: rangkai 3 langkah API jadi satu alur otomatis.
 *
 *    LANGKAH 1  Lookup   : nomor HP        -> username Lichess
 *    LANGKAH 2  Pair     : 2 username + TC -> BulkID + link game
 *    LANGKAH 3  Results  : BulkID          -> hasil (1-0 / 0-1 / 1/2)
 *
 *  Dibuat untuk belajar API. Komentar sengaja banyak.
 *  Author: Ray (dibimbing) - 2026
 * ============================================================
 *
 *  CARA JALANIN (Git Bash):
 *    WABOT_TOKEN="token_dari_pa_chacha" node pair.js <hpPutih> <hpHitam>
 *
 *  CARA JALANIN (PowerShell):
 *    $env:WABOT_TOKEN="token_dari_pa_chacha"
 *    node pair.js <hpPutih> <hpHitam>
 *
 *  Contoh (lookup + tampilkan rencana, TANPA bikin game):
 *    node pair.js 628111111111 628222222222
 *
 *  Contoh (beneran bikin game di Lichess  ->  pakai flag --pair):
 *    node pair.js 628111111111 628222222222 --pair --limit 300 --inc 3
 * ============================================================
 */

'use strict';

// --- Pengaturan dasar -------------------------------------------------------

// (2) ALAMAT dasar API. Tiap method nanti ditempel di belakang alamat ini.
const BASE = 'https://services.chessstream.com/webservices/WAbot.asmx';

// TOKEN = password API. Diambil dari environment variable, BUKAN ditulis di sini.
// Kenapa? Supaya kalau script ini dibagikan / masuk Git, password TIDAK ikut bocor.
const TOKEN = process.env.WABOT_TOKEN;

// --- Penyimpanan LOKAL (BUKAN API): catat tiap pairing ke file -------------
// Ini "simpan ke file" pakai modul bawaan Node 'fs'. TIDAK menyentuh server,
// tidak butuh token, tidak butuh internet. Cuma nulis/baca file di komputer ini.
const fs = require('fs');
const path = require('path');
const STORE = path.join(__dirname, 'pairings.json'); // file catatan di sebelah script

// Baca semua catatan. Kalau file belum ada -> mulai dari daftar kosong.
function loadStore() {
  try {
    return JSON.parse(fs.readFileSync(STORE, 'utf8'));
  } catch {
    return [];
  }
}

// Tulis semua catatan kembali ke file (rapi, 2 spasi indentasi).
function saveStore(list) {
  fs.writeFileSync(STORE, JSON.stringify(list, null, 2));
}

// Tambah 1 pairing baru ke catatan (dipanggil otomatis tiap pairing sukses).
function addPairing(pr, opt) {
  const list = loadStore();
  list.push({
    bulkId: pr.bulk_pairing_id || '',
    gameId: pr.lichess_game_id || '',
    white: pr.white_player || '',
    black: pr.black_player || '',
    url: pr.white_url || '',
    clockLimit: String(opt.limit),
    clockIncrement: String(opt.inc),
    rated: !!opt.rated,
    variant: opt.variant,
    createdAt: new Date().toISOString(),
    result: null, // diisi nanti lewat --check / --check-all
  });
  saveStore(list);
}

// Isi / perbarui hasil game untuk satu BulkID. Return true kalau ketemu.
function setResult(bulkId, result) {
  const list = loadStore();
  const row = list.find((r) => r.bulkId === bulkId);
  if (row) {
    row.result = result;
    saveStore(list);
  }
  return !!row;
}


// --- Helper: pemanggil API umum --------------------------------------------
// Satu fungsi ini dipakai oleh KETIGA langkah. Ini inti "4 bagian API":
//   method  -> selalu POST
//   alamat  -> BASE + '/' + namaMethod
//   data    -> Token + parameter lain (dikirim sebagai form-urlencoded)
//   response-> XML <string>...</string>, isinya kita kupas jadi data biasa
async function callApi(methodName, params) {
  // (3) DATA: gabung Token + parameter, jadi bentuk "Key=Value&Key=Value"
  const body = new URLSearchParams({ Token: TOKEN, ...params });

  // (1) METHOD + (2) ALAMAT: ketuk pintu lewat POST
  const res = await fetch(`${BASE}/${methodName}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body,
  });

  // (4) RESPONSE: ASMX balas XML  ->  <string xmlns="...">ISI</string>
  const xml = await res.text();

  // Kupas bungkus XML-nya, ambil ISI di dalamnya.
  const inner = xml
    .replace(/<\?xml[^>]*\?>/i, '')      // buang baris <?xml ... ?>
    .replace(/<\/?string[^>]*>/gi, '')   // buang <string ...> dan </string>
    .trim();

  // Isi-nya bisa JSON (untuk lookup) atau teks biasa. Coba parse jadi JSON dulu.
  try {
    return JSON.parse(inner);
  } catch {
    return inner; // kalau bukan JSON, kembalikan teks apa adanya
  }
}


// --- LANGKAH 1: Lookup nomor HP -> user Lichess -----------------------------
async function lookup(phone) {
  return callApi('GetLichessUserByPhone', { Phone: phone });
}

// --- LANGKAH 2: Pasangkan 2 username -> BulkID + link -----------------------
async function pair(white, black, opt) {
  return callApi('PairLichessUsernames', {
    WhitePlayer: white,                 // pegang PUTIH
    BlackPlayer: black,                 // pegang HITAM
    ClockLimit: String(opt.limit),      // waktu dasar (detik), mis. 300 = 5 menit
    ClockIncrement: String(opt.inc),    // tambahan per langkah (detik), mis. 3
    IsRated: String(opt.rated),         // "true" / "false"
    Variant: opt.variant,               // "standard", "chess960", dll
  });
}

// --- LANGKAH 3: Cek hasil berdasarkan BulkID --------------------------------
async function results(bulkId) {
  return callApi('GetBulkPairingResults', { BulkPairingID: String(bulkId) });
}


// --- Bantu baca argumen dari terminal ---------------------------------------
// Contoh argumen:  628111... 628222... --pair --limit 300 --inc 3 --casual
function parseArgs(argv) {
  const a = argv.slice(2);                 // 2 item pertama = path node & script
  const positional = a.filter((x) => !x.startsWith('--'));
  const has = (flag) => a.includes(flag);
  const val = (flag, def) => {
    const i = a.indexOf(flag);
    return i >= 0 && a[i + 1] ? a[i + 1] : def;
  };
  return {
    phoneWhite: positional[0],
    phoneBlack: positional[1],
    doPair: has('--pair'),               // TANPA flag ini = cuma lookup (aman)
    limit: val('--limit', '300'),        // default 5 menit
    inc: val('--inc', '3'),              // default +3 detik
    rated: !has('--casual'),             // default rated; --casual = latihan
    variant: val('--variant', 'standard'),
  };
}


// --- Alur utama -------------------------------------------------------------
async function main() {
  const opt = parseArgs(process.argv);

  // Guard clause: pastikan syarat dasar terpenuhi dulu (pola yang kita pelajari).
  if (!TOKEN) {
    console.error('ERROR: WABOT_TOKEN belum di-set. Set dulu token-nya.');
    process.exit(1);
  }
  if (!opt.phoneWhite || !opt.phoneBlack) {
    console.error('Pemakaian: node pair.js <hpPutih> <hpHitam> [--pair --limit 300 --inc 3]');
    process.exit(1);
  }

  // ===== LANGKAH 1: lookup kedua nomor =====
  console.log('\n[1] LOOKUP nomor -> username Lichess');
  const w = await lookup(opt.phoneWhite);
  const b = await lookup(opt.phoneBlack);

  const show = (label, r) =>
    console.log(
      `    ${label}: found=${r.found} verified=${r.is_verified} ` +
      `nama="${r.full_name}" lichess="${r.lichess_handle}"`
    );
  show('Putih', w);
  show('Hitam', b);

  // Verifikasi: kedua pemain harus ketemu DAN terverifikasi sebelum dipasangkan.
  const ok = w.found && w.is_verified && b.found && b.is_verified;
  if (!ok) {
    console.log('\n    -> Tidak bisa pairing: ada pemain yang belum ketemu / belum verifikasi.');
    return;
  }

  // ===== LANGKAH 2: pairing =====
  console.log('\n[2] PAIR ' + w.lichess_handle + ' (putih) vs ' + b.lichess_handle + ' (hitam)');
  console.log(`    Time control: G${opt.limit}s + ${opt.inc}s | rated=${opt.rated} | ${opt.variant}`);

  if (!opt.doPair) {
    // Mode aman: cuma tampilkan RENCANA, tidak bikin game.
    console.log('    -> MODE LATIHAN (tanpa --pair): game TIDAK dibuat. Tambah --pair untuk eksekusi.');
    return;
  }

  const pr = await pair(w.lichess_handle, b.lichess_handle, opt);
  if (pr && pr.success && pr.bulk_pairing_id) {
    addPairing(pr, opt); // <-- SIMPAN OTOMATIS
    console.log('    Game dibuat:', pr.white_url);
    console.log('    BulkID =', pr.bulk_pairing_id, '(tersimpan ke pairings.json)');
    console.log('\n[3] (Nanti) cek hasil: node pair.js --check ' + pr.bulk_pairing_id);
  } else {
    console.log('    Gagal:', (pr && pr.Message) || JSON.stringify(pr));
  }
}

// Mode khusus: pairing langsung pakai USERNAME (lewati lookup phone).
// Berguna kalau kamu SUDAH tahu username Lichess kedua pemain.
//   node pair.js --pairusers <putih> <hitam>            (LATIHAN: cuma tampilkan rencana)
//   node pair.js --pairusers <putih> <hitam> --pair     (EKSEKUSI: bikin game asli)
async function maybePairUsers() {
  const a = process.argv.slice(2);
  const i = a.indexOf('--pairusers');
  if (i < 0 || !a[i + 1] || !a[i + 2]) return false;

  const opt = parseArgs(process.argv);
  const white = a[i + 1];
  const black = a[i + 2];

  console.log('\n[2] PAIR (langsung username) ' + white + ' (putih) vs ' + black + ' (hitam)');
  console.log(`    Time control: G${opt.limit}s + ${opt.inc}s | rated=${opt.rated} | ${opt.variant}`);

  if (!opt.doPair) {
    console.log('    -> MODE LATIHAN (tanpa --pair): game TIDAK dibuat. Tambah --pair untuk eksekusi.');
    return true;
  }

  const pr = await pair(white, black, opt);
  if (pr && pr.success && pr.bulk_pairing_id) {
    addPairing(pr, opt); // <-- SIMPAN OTOMATIS ke pairings.json (file lokal)
    console.log('    Game dibuat:', pr.white_url);
    console.log('    BulkID =', pr.bulk_pairing_id, '(tersimpan ke pairings.json)');
  } else {
    console.log('    Gagal:', (pr && pr.Message) || JSON.stringify(pr));
  }
  return true;
}

// Mode khusus: cek hasil saja  ->  node pair.js --check <BulkID>
async function maybeCheckOnly() {
  const a = process.argv.slice(2);
  const i = a.indexOf('--check');
  if (i >= 0 && a[i + 1]) {
    const id = a[i + 1];
    console.log('[3] CEK HASIL BulkID =', id);
    const r = await results(id);
    setResult(id, r); // simpan hasil ke pairings.json (kalau BulkID-nya tercatat)
    console.log('    ', typeof r === 'string' ? r : JSON.stringify(r));
    return true;
  }
  return false;
}

// Ringkas hasil game jadi skor pendek (mis. "1-0") -- buang JSON/PGN yang panjang.
function summarizeResult(result) {
  if (!result) return '(belum dimainkan)';
  if (typeof result === 'string') return result;
  if (result.games && result.games.length) {
    return result.games
      .map((g) => g.result + (g.is_finished ? '' : ' (berjalan)'))
      .join(', ');
  }
  return '(belum ada)';
}

// Mode: lihat SEMUA pairing tersimpan  ->  node pair.js --list
async function maybeList() {
  if (!process.argv.slice(2).includes('--list')) return false;
  const list = loadStore();
  if (!list.length) {
    console.log('Belum ada pairing tersimpan (pairings.json kosong).');
    return true;
  }
  console.log(`\n${list.length} pairing tersimpan:\n`);
  list.forEach((r, i) => {
    console.log(`  ${i + 1}. ${r.white} (P) vs ${r.black} (H)  ->  ${summarizeResult(r.result)}`);
    console.log(`     ${r.url} | BulkID=${r.bulkId}`);
  });
  return true;
}

// Mode: cek hasil SEMUA game yang belum ada hasilnya  ->  node pair.js --check-all
async function maybeCheckAll() {
  if (!process.argv.slice(2).includes('--check-all')) return false;
  if (!TOKEN) { console.error('WABOT_TOKEN belum di-set.'); return true; }
  const list = loadStore();
  const pending = list.filter((r) => !r.result && r.bulkId);
  console.log(`\nCek hasil ${pending.length} game yang belum selesai...`);
  for (const r of pending) {
    const res = await results(r.bulkId);
    setResult(r.bulkId, res); // simpan balik ke file
    console.log(`  ${r.white} vs ${r.black} (${r.bulkId}):`,
      typeof res === 'string' ? res : JSON.stringify(res));
  }
  console.log('Selesai. Lihat semua: node pair.js --list');
  return true;
}

// Jalankan: cek mode-mode khusus dulu; kalau tidak ada, jalankan alur penuh.
(async () => {
  try {
    if (await maybeList()) return;
    if (await maybeCheckAll()) return;
    if (await maybeCheckOnly()) return;
    if (await maybePairUsers()) return;
    await main();
  } catch (err) {
    console.error('Terjadi error:', err.message);
    process.exit(1);
  }
})();
