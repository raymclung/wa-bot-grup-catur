using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

// ============================================================
//  Persona chat: bot tampil sebagai TOKOH manusia (mis. "Ria") di grup non-catur.
//  Semua fitur catur dilewati -> handler /incoming nge-branch ke sini lalu return.
//   - Persona = system prompt tersendiri (file/inline), TIDAK memakai prompt "polisi catur".
//   - Memori percakapan bergulir per-grup (persisten) biar nyambung antar pesan/restart.
//   - Balas kalau: di-tag, namanya disebut, atau membalas pesan dia; selain itu SESEKALI
//     "nyeletuk" (peluang kecil + jeda) supaya hidup tapi tidak spam / tidak bikin bosan.
//   - Tanpa moderasi, tanpa admin: murni ngobrol.
// ============================================================

internal class PersonaConfig
{
	public bool Enabled { get; set; } = false;

	// Nama tampil tokoh (mis. "Ria").
	public string Name { get; set; } = "";

	// Nama-nama pemicu (kalau disebut di chat -> pasti dibalas). Mis. ["ria","maria","judit"].
	public string[] Names { get; set; } = Array.Empty<string>();

	// Persona bisa ditaruh inline (Prompt) ATAU dari file (PromptFile, relatif ke folder brain).
	public string Prompt { get; set; } = "";
	public string PromptFile { get; set; } = "";

	// Peluang "nyeletuk" saat tidak di-tag (0..1) + jeda minimal antar celetukan (detik).
	public double JumpInChance { get; set; } = 0.15;
	public int JumpInCooldownSeconds { get; set; } = 150;

	// Jumlah giliran percakapan yang diingat (per grup).
	public int MemoryTurns { get; set; } = 14;

	// Rasa "hangat" + panjang balasan.
	public double Temperature { get; set; } = 0.85;
	public int MaxChars { get; set; } = 600;
	public int NumPredict { get; set; } = 220;
}

internal static class PersonaChat
{
	private static readonly object _lk = new object();
	private static readonly Dictionary<string, List<PMsg>> _mem = new Dictionary<string, List<PMsg>>();
	private static readonly Dictionary<string, DateTime> _lastJumpIn = new Dictionary<string, DateTime>();
	private static readonly Dictionary<string, string> _lastBotMsgId = new Dictionary<string, string>();
	private static readonly Dictionary<string, string> _promptCache = new Dictionary<string, string>();
	private static string _path = "";

	internal sealed class PMsg
	{
		public string Who { get; set; } = "";
		public string Text { get; set; } = "";
	}

	// Muat memori percakapan yang tersimpan (dipanggil sekali saat start).
	public static void Init(string path)
	{
		_path = path;
		try
		{
			if (!File.Exists(path)) return;
			Dictionary<string, List<PMsg>>? d = JsonSerializer.Deserialize<Dictionary<string, List<PMsg>>>(File.ReadAllText(path));
			if (d != null)
			{
				lock (_lk)
				{
					_mem.Clear();
					foreach (KeyValuePair<string, List<PMsg>> kv in d) _mem[kv.Key] = kv.Value ?? new List<PMsg>();
				}
			}
		}
		catch { }
	}

	private static void Save()
	{
		if (_path.Length == 0) return;
		try { File.WriteAllText(_path, JsonSerializer.Serialize(_mem)); } catch { }
	}

	private static string LoadPrompt(PersonaConfig p)
	{
		if (!string.IsNullOrWhiteSpace(p.Prompt)) return p.Prompt;
		if (string.IsNullOrWhiteSpace(p.PromptFile)) return "";
		lock (_lk)
		{
			if (_promptCache.TryGetValue(p.PromptFile, out string? cached) && cached != null) return cached;
		}
		string txt = "";
		try
		{
			foreach (string bas in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
			{
				string path = Path.IsPathRooted(p.PromptFile) ? p.PromptFile : Path.Combine(bas, p.PromptFile);
				if (File.Exists(path)) { txt = File.ReadAllText(path); break; }
			}
		}
		catch { }
		lock (_lk) { _promptCache[p.PromptFile] = txt; }
		return txt;
	}

	private static string CleanName(string? pushName)
	{
		string n = (pushName ?? "").Trim().Replace(":", " ").Replace("\n", " ").Replace("\r", " ").Trim();
		if (n.Length == 0) return "Seseorang";
		if (n.Length > 24) n = n.Substring(0, 24).Trim();
		return n.Length == 0 ? "Seseorang" : n;
	}

	private static void Remember(string jid, string who, string text, int cap)
	{
		if (string.IsNullOrWhiteSpace(text)) return;
		string t = text.Trim();
		if (t.Length > 240) t = t.Substring(0, 240) + "…";
		lock (_lk)
		{
			if (!_mem.TryGetValue(jid, out List<PMsg>? list) || list == null) { list = new List<PMsg>(); _mem[jid] = list; }
			list.Add(new PMsg { Who = who, Text = t });
			int max = Math.Max(4, cap);
			while (list.Count > max) list.RemoveAt(0);
		}
		Save();
	}

	private static string RenderHistory(string jid)
	{
		lock (_lk)
		{
			if (!_mem.TryGetValue(jid, out List<PMsg>? list) || list == null || list.Count == 0) return "";
			StringBuilder sb = new StringBuilder();
			foreach (PMsg m in list) sb.Append(m.Who).Append(": ").Append(m.Text).Append('\n');
			return sb.ToString().TrimEnd();
		}
	}

	private static bool NameMentioned(string text, string[] names)
	{
		if (names == null || names.Length == 0) return false;
		string low = text.ToLowerInvariant();
		foreach (string nm in names)
		{
			string k = (nm ?? "").Trim().ToLowerInvariant();
			if (k.Length == 0) continue;
			if (Regex.IsMatch(low, "(^|[^a-z])" + Regex.Escape(k) + "([^a-z]|$)")) return true;
		}
		return false;
	}

	// Balas satu pesan grup dalam mode persona. Mengembalikan label aksi (untuk /incoming).
	public static async Task<string> HandleAsync(AppConfig config, HttpClient http, IncomingMessage msg, PersonaConfig p, ILogger logger)
	{
		string jid = msg.Jid;
		string who = CleanName(msg.PushName);
		string text = (msg.Text ?? "").Trim();
		if (text.Length == 0) return "persona-empty";

		// Selalu catat pesan masuk supaya konteks tetap terbangun walau tidak dibalas.
		Remember(jid, who, text, p.MemoryTurns);

		bool repliedToRia;
		lock (_lk)
		{
			repliedToRia = msg.QuotedId.Length > 0 && _lastBotMsgId.TryGetValue(jid, out string? lid) && lid != null && lid.Length > 0 && lid == msg.QuotedId;
		}
		bool solicited = msg.MentionedBot || repliedToRia || NameMentioned(text, p.Names);
		bool willReply = solicited;
		if (!willReply)
		{
			// Nyeletuk sesekali: hanya kalimat "berisi", peluang kecil + jeda, bukan media/forward/perintah.
			bool substantive = text.Length >= 15 && text.Contains(' ') && string.IsNullOrEmpty(msg.MediaType) && !msg.IsForwarded && !text.StartsWith("!");
			if (substantive)
			{
				bool cooled;
				lock (_lk)
				{
					cooled = !_lastJumpIn.TryGetValue(jid, out DateTime last) || (DateTime.UtcNow - last).TotalSeconds >= p.JumpInCooldownSeconds;
				}
				if (cooled && Random.Shared.NextDouble() < p.JumpInChance)
				{
					willReply = true;
					lock (_lk) { _lastJumpIn[jid] = DateTime.UtcNow; }
				}
			}
		}
		if (!willReply) return "persona-listen";

		string persona = LoadPrompt(p);
		if (persona.Length == 0)
		{
			logger.LogWarning("Persona prompt kosong untuk {Jid} (cek PromptFile/Prompt).", jid);
			return "persona-noprompt";
		}

		string history = RenderHistory(jid);
		string selfName = string.IsNullOrWhiteSpace(p.Name) ? "kamu" : p.Name;
		StringBuilder q = new StringBuilder();
		if (history.Length > 0) q.Append("[Transkrip obrolan grup terbaru]\n").Append(history).Append("\n\n");
		q.Append("Pesan terbaru dari ").Append(who).Append(": ").Append(text).Append("\n\n");
		q.Append("Balas dengan WAJAR sebagai ").Append(selfName)
		 .Append(" — singkat (1-3 kalimat), santai, sesuai kepribadian & sejarahmu. ")
		 .Append("Jangan menyebut dirimu AI/bot/asisten. Jangan mengulang transkrip. ")
		 .Append("Kalau tidak ada yang penting untuk ditanggapi, cukup balas singkat & ramah.");

		AiConfig pcfg = new AiConfig
		{
			Enabled = true,
			Provider = config.Ai?.Provider ?? "ollama",
			Url = config.Ai?.Url ?? "http://localhost:11434",
			Model = config.Ai?.Model ?? "qwen2.5:7b",
			SystemPrompt = persona,
			MaxOutputChars = p.MaxChars,
			NumPredict = p.NumPredict,
			TimeoutSeconds = config.Ai?.TimeoutSeconds ?? 60,
			KeepAlive = config.Ai?.KeepAlive ?? "30m",
			Temperature = p.Temperature,
			TopP = config.Ai?.TopP ?? 0.9,
			RepeatPenalty = config.Ai?.RepeatPenalty ?? 1.15
		};

		string? ans = await Ai.Ask(pcfg, http, q.ToString(), logger);
		if (string.IsNullOrWhiteSpace(ans)) return "persona-silent";
		ans = ans.Trim();
		if (ans.Length > p.MaxChars) ans = ans.Substring(0, p.MaxChars).TrimEnd() + "…";

		string url = ChannelRoute.BaseForJid(config, jid) + "/send";
		string sentId = await SendReturningId(http, url, jid, ans, logger);
		if (sentId.Length > 0) lock (_lk) { _lastBotMsgId[jid] = sentId; }

		Remember(jid, selfName, ans, p.MemoryTurns);
		return "persona-reply";
	}

	private static async Task<string> SendReturningId(HttpClient http, string url, string jid, string text, ILogger logger)
	{
		try
		{
			var payload = new { jid, text };
			using StringContent content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
			using HttpResponseMessage r = await http.PostAsync(url, content);
			if (!r.IsSuccessStatusCode) return "";
			using JsonDocument doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
			return doc.RootElement.TryGetProperty("id", out JsonElement idEl) ? (idEl.GetString() ?? "") : "";
		}
		catch (Exception ex)
		{
			logger.LogWarning("Persona kirim gagal: {M}", ex.Message);
			return "";
		}
	}
}
