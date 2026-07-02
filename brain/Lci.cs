using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

// ============================================================
//  LCI (Liga Catur Indonesia) API client - pembungkus WAbot.asmx.
//  Port dari tools/manual-pairing/pair.js + method tambahan.
//  ASMX balas XML  <string xmlns="...">ISI</string>  -> dikupas; ISI bisa JSON / teks.
//  Token dari config.WabotToken (di-set dari secrets.json "wabotToken").
//
//  Operasi WAbot.asmx:
//    GetLichessUserByPhone(Phone)
//    PairLichessUsernames(WhitePlayer,BlackPlayer,ClockLimit,ClockIncrement,IsRated,Variant)
//    StartBulkPairingClocks(BulkPairingID)
//    CancelBulkPairing(BulkPairingID)
//    GetBulkPairingResults(BulkPairingID)
// ============================================================

internal class LciConfig
{
	public bool Enabled { get; set; } = false;

	public string BaseUrl { get; set; } = "https://services.chessstream.com/webservices/WAbot.asmx";

	public string DefaultVariant { get; set; } = "standard";

	public bool DefaultRated { get; set; } = false;
}

internal static class LciClient
{
	public sealed class LookupResult
	{
		public bool Found;
		public bool Verified;
		public string FullName = "";
		public string Handle = "";
	}

	public sealed class PairResult
	{
		public bool Success;
		public string BulkId = "";
		public string GameId = "";
		public string Url = "";
		public string White = "";
		public string Black = "";
		public string Message = "";
	}

	public sealed class ActionResult
	{
		public bool Success;
		public string Message = "";
		public string Raw = "";
	}

	public sealed class GameResult
	{
		public string White = "";
		public string Black = "";
		public string Score = ""; // "1-0" / "0-1" / "1/2-1/2"
		public bool Finished;
		public bool Started; // sudah ada langkah (moves tidak kosong)
	}

	public sealed class ResultInfo
	{
		public bool Ok;            // dapat respon valid dari server
		public bool AllFinished;   // semua game selesai
		public List<GameResult> Games = new List<GameResult>();
		public string Summary = ""; // mis. "Mikaysr 1-0 Ade21h"
	}

	private static readonly Regex XmlDecl = new Regex("<\\?xml[^>]*\\?>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
	private static readonly Regex StringTag = new Regex("</?string[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	// POST form-urlencoded {Token, ...params} -> kupas <string>...</string> -> teks mentah (JSON / biasa).
	private static async Task<string?> CallRaw(AppConfig config, HttpClient http, string method, Dictionary<string, string> form, ILogger logger)
	{
		try
		{
			string baseUrl = config.Lci?.BaseUrl ?? "";
			if (baseUrl.Length == 0 || string.IsNullOrEmpty(config.WabotToken))
			{
				logger.LogWarning("LCI {Method} dilewati: baseUrl/token kosong", method);
				return null;
			}
			Dictionary<string, string> body = new Dictionary<string, string>(form) { ["Token"] = config.WabotToken };
			using FormUrlEncodedContent content = new FormUrlEncodedContent(body);
			using HttpResponseMessage r = await http.PostAsync(baseUrl + "/" + method, content);
			string xml = await r.Content.ReadAsStringAsync();
			return StringTag.Replace(XmlDecl.Replace(xml, ""), "").Trim();
		}
		catch (Exception ex)
		{
			logger.LogWarning("LCI {Method} gagal: {Msg}", method, ex.Message);
			return null;
		}
	}

	private static async Task<JsonElement?> Call(AppConfig config, HttpClient http, string method, Dictionary<string, string> form, ILogger logger)
	{
		string? inner = await CallRaw(config, http, method, form, logger);
		if (inner == null || inner.Length == 0 || (inner[0] != '{' && inner[0] != '['))
		{
			if (!string.IsNullOrEmpty(inner))
			{
				logger.LogWarning("LCI {Method} balas non-JSON: {Body}", method, (inner.Length > 120) ? inner.Substring(0, 120) : inner);
			}
			return null;
		}
		try
		{
			using JsonDocument doc = JsonDocument.Parse(inner);
			return doc.RootElement.Clone();
		}
		catch (Exception ex)
		{
			logger.LogWarning("LCI {Method} parse gagal: {Msg}", method, ex.Message);
			return null;
		}
	}

	// nomor HP -> user Lichess (found / verified / handle).
	public static async Task<LookupResult> LookupByPhone(AppConfig config, HttpClient http, string phone, ILogger logger)
	{
		LookupResult res = new LookupResult();
		JsonElement? j = await Call(config, http, "GetLichessUserByPhone", new Dictionary<string, string> { ["Phone"] = phone }, logger);
		if (j.HasValue)
		{
			JsonElement e = j.Value;
			res.Found = GetBool(e, "found");
			res.Verified = GetBool(e, "is_verified");
			res.FullName = GetStr(e, "full_name");
			res.Handle = GetStr(e, "lichess_handle");
		}
		return res;
	}

	// 2 username + TC -> BulkID + link game. rated=false utk UNRATED.
	public static async Task<PairResult> Pair(AppConfig config, HttpClient http, string white, string black, int limitSec, int incSec, bool rated, string variant, ILogger logger)
	{
		Dictionary<string, string> p = new Dictionary<string, string>
		{
			["WhitePlayer"] = white,
			["BlackPlayer"] = black,
			["ClockLimit"] = limitSec.ToString(),
			["ClockIncrement"] = incSec.ToString(),
			["IsRated"] = (rated ? "true" : "false"),
			["Variant"] = (string.IsNullOrWhiteSpace(variant) ? "standard" : variant)
		};
		PairResult res = new PairResult();
		JsonElement? j = await Call(config, http, "PairLichessUsernames", p, logger);
		if (j.HasValue)
		{
			JsonElement e = j.Value;
			res.Success = GetBool(e, "success");
			res.BulkId = GetStr(e, "bulk_pairing_id");
			res.GameId = GetStr(e, "lichess_game_id");
			res.Url = GetStr(e, "white_url");
			res.White = GetStr(e, "white_player");
			res.Black = GetStr(e, "black_player");
			res.Message = GetStr(e, "Message");
		}
		return res;
	}

	// Mulai jam catur untuk satu BulkID.
	public static Task<ActionResult> StartClocks(AppConfig config, HttpClient http, string bulkId, ILogger logger)
	{
		return ActionByBulk(config, http, "StartBulkPairingClocks", bulkId, logger);
	}

	// Batalkan board untuk satu BulkID.
	public static Task<ActionResult> CancelBoard(AppConfig config, HttpClient http, string bulkId, ILogger logger)
	{
		return ActionByBulk(config, http, "CancelBulkPairing", bulkId, logger);
	}

	// Hasil game untuk satu BulkID (teks/JSON mentah, biar pemanggil yang parse).
	public static async Task<string> Results(AppConfig config, HttpClient http, string bulkId, ILogger logger)
	{
		string? raw = await CallRaw(config, http, "GetBulkPairingResults", new Dictionary<string, string> { ["BulkPairingID"] = bulkId }, logger);
		return raw ?? "";
	}

	// Hasil game yang sudah di-parse (skor + status selesai). Untuk fitur hasil/auto-umumkan.
	public static async Task<ResultInfo> ResultsParsed(AppConfig config, HttpClient http, string bulkId, ILogger logger)
	{
		ResultInfo info = new ResultInfo();
		string raw = await Results(config, http, bulkId, logger);
		if (raw.Length == 0 || raw[0] != '{')
		{
			return info;
		}
		try
		{
			using JsonDocument doc = JsonDocument.Parse(raw);
			JsonElement root = doc.RootElement;
			info.Ok = true;
			info.AllFinished = GetBool(root, "all_finished");
			if (root.TryGetProperty("games", out JsonElement games) && games.ValueKind == JsonValueKind.Array)
			{
				List<string> parts = new List<string>();
				foreach (JsonElement g in games.EnumerateArray())
				{
					GameResult gr = new GameResult
					{
						White = GetStr(g, "white"),
						Black = GetStr(g, "black"),
						Score = GetStr(g, "result"),
						Finished = GetBool(g, "is_finished"),
						Started = GetStr(g, "moves").Trim().Length > 0
					};
					info.Games.Add(gr);
					if (gr.Score.Length > 0)
					{
						parts.Add(gr.White + " " + gr.Score + " " + gr.Black);
					}
				}
				info.Summary = string.Join("\n", parts);
			}
		}
		catch (Exception ex)
		{
			logger.LogWarning("LCI results parse gagal: {Msg}", ex.Message);
		}
		return info;
	}

	private static async Task<ActionResult> ActionByBulk(AppConfig config, HttpClient http, string method, string bulkId, ILogger logger)
	{
		ActionResult res = new ActionResult();
		string? raw = await CallRaw(config, http, method, new Dictionary<string, string> { ["BulkPairingID"] = bulkId }, logger);
		if (raw == null)
		{
			return res;
		}
		res.Raw = raw;
		if (raw.Length > 0 && (raw[0] == '{' || raw[0] == '['))
		{
			try
			{
				using JsonDocument doc = JsonDocument.Parse(raw);
				res.Success = GetBool(doc.RootElement, "success");
				res.Message = GetStr(doc.RootElement, "Message");
				return res;
			}
			catch
			{
			}
		}
		// balasan teks biasa: anggap sukses kalau tidak menyebut error/gagal/false.
		string low = raw.ToLowerInvariant();
		res.Success = low.Length > 0 && !low.Contains("error") && !low.Contains("gagal") && !low.Contains("false") && !low.Contains("fail");
		res.Message = raw;
		return res;
	}

	private static bool GetBool(JsonElement e, string name)
	{
		if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out JsonElement v))
		{
			if (v.ValueKind == JsonValueKind.True)
			{
				return true;
			}
			if (v.ValueKind == JsonValueKind.String)
			{
				return string.Equals(v.GetString(), "true", StringComparison.OrdinalIgnoreCase);
			}
		}
		return false;
	}

	private static string GetStr(JsonElement e, string name)
	{
		if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out JsonElement v))
		{
			if (v.ValueKind == JsonValueKind.String)
			{
				return v.GetString() ?? "";
			}
			if (v.ValueKind == JsonValueKind.Number)
			{
				return v.ToString();
			}
		}
		return "";
	}
}
