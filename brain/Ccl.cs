using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

/// <summary>Integrasi event chess.college (id.chess.college). Data via API ASMX ScriptService.</summary>
class CclConfig
{
    public bool Enabled { get; set; } = false;
    public string BaseUrl { get; set; } = "https://id.chess.college";
    public string ApiBase { get; set; } = "https://id.chess.college/webservices/events/service.asmx";
    public string Command { get; set; } = "events";
    public bool AnnounceEnabled { get; set; } = true;
    public string AnnounceGroupJid { get; set; } = "";   // grup chess.com
    public int[] RemindersMinutes { get; set; } = new[] { 300, 15 };
    public int PollMinutes { get; set; } = 10;
    public int TimezoneOffsetHours { get; set; } = 7;
}

class CclEvent
{
    public int Tid;
    public string Name = "";
    public DateTimeOffset Start;
    public bool HasStart;
    public string Type = "";
    public bool IsTeam => Type.IndexOf("team", StringComparison.OrdinalIgnoreCase) >= 0;
}

/// <summary>Sesi wizard "!events": daftar event untuk dipilih.</summary>
class CclSession
{
    public List<CclEvent> Options { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

static class Ccl
{
    // Panggil method ASMX ScriptService ({"d":"<json>"}), kembalikan elemen "data" (atau null).
    static async Task<JsonElement?> CallData(CclConfig cfg, string method, object body, HttpClient http, ILogger logger)
    {
        try
        {
            var payload = JsonSerializer.Serialize(body);
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var resp = await http.PostAsync($"{cfg.ApiBase}/{method}", content);
            if (!resp.IsSuccessStatusCode) return null;
            string outer = await resp.Content.ReadAsStringAsync();
            using var od = JsonDocument.Parse(outer);
            string inner = od.RootElement.TryGetProperty("d", out var de) ? (de.GetString() ?? "") : outer;
            if (string.IsNullOrEmpty(inner)) return null;
            using var id = JsonDocument.Parse(inner);
            var root = id.RootElement;
            if (root.TryGetProperty("success", out var su) && su.ValueKind == JsonValueKind.False) return null;
            if (root.TryGetProperty("data", out var data)) return data.Clone();
            return root.Clone();
        }
        catch (Exception ex) { logger.LogError("CCL {Method} gagal: {Msg}", method, ex.Message); return null; }
    }

    static DateTimeOffset ParseUtc(string s)
    {
        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
            return new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc));
        return DateTimeOffset.MinValue;
    }

    static CclEvent ParseEvent(JsonElement e)
    {
        var ev = new CclEvent();
        if (e.TryGetProperty("TournamentID", out var t) && t.ValueKind == JsonValueKind.Number) ev.Tid = t.GetInt32();
        if (e.TryGetProperty("EventName", out var n)) ev.Name = (n.GetString() ?? "").Trim();
        if (e.TryGetProperty("EventType", out var ty)) ev.Type = ty.GetString() ?? "";
        if (e.TryGetProperty("EventDateStart", out var d) && d.ValueKind == JsonValueKind.String)
        {
            var dt = ParseUtc(d.GetString() ?? "");
            if (dt != DateTimeOffset.MinValue) { ev.Start = dt; ev.HasStart = true; }
        }
        return ev;
    }

    public static async Task<(List<CclEvent> upcoming, List<CclEvent> past)> GetEvents(CclConfig cfg, HttpClient http, ILogger logger)
    {
        var up = new List<CclEvent>(); var past = new List<CclEvent>();
        var data = await CallData(cfg, "GetEvents", new { }, http, logger);
        if (data is null) return (up, past);
        var d = data.Value;
        if (d.TryGetProperty("Upcoming", out var u) && u.ValueKind == JsonValueKind.Array)
            foreach (var e in u.EnumerateArray()) up.Add(ParseEvent(e));
        if (d.TryGetProperty("Past", out var p) && p.ValueKind == JsonValueKind.Array)
            foreach (var e in p.EnumerateArray()) past.Add(ParseEvent(e));
        return (up, past);
    }

    static string Score(JsonElement e, string prop)
    {
        if (!e.TryGetProperty(prop, out var v) || v.ValueKind != JsonValueKind.Number) return "0";
        return v.GetDouble().ToString(CultureInfo.InvariantCulture).Replace(".5", "½");
    }

    static string Link(CclConfig cfg, CclEvent ev, bool standings)
    {
        if (!standings) return $"{cfg.BaseUrl}/#event-live/{ev.Tid}";
        return ev.IsTeam ? $"{cfg.BaseUrl}/#pairings/{ev.Tid}" : $"{cfg.BaseUrl}/#individual-standings/{ev.Tid}";
    }

    static string WhenWIB(CclConfig cfg, CclEvent ev)
    {
        if (!ev.HasStart) return "";
        return ev.Start.ToOffset(TimeSpan.FromHours(cfg.TimezoneOffsetHours))
                       .ToString("dd/MM HH:mm", CultureInfo.InvariantCulture) + " WIB";
    }

    /// <summary>Daftar bernomor untuk wizard "!events".</summary>
    public static string BuildList(CclConfig cfg, List<CclEvent> opts)
    {
        var sb = new StringBuilder();
        sb.AppendLine("📋 *Event chess.college* — balas nomornya untuk lihat pairing/klasemen:");
        for (int i = 0; i < opts.Count; i++)
        {
            var e = opts[i];
            string tag = e.HasStart && e.Start > DateTimeOffset.UtcNow ? "📅" : "🏁";
            string when = WhenWIB(cfg, e);
            sb.AppendLine($"{i + 1}. {tag} {e.Name} ({e.Type}){(when.Length > 0 ? " — " + when : "")}");
        }
        sb.Append("(ketik !batal untuk membatalkan)");
        return sb.ToString();
    }

    /// <summary>Tampilan pairing/klasemen satu event (tim atau individu).</summary>
    public static async Task<string> BuildView(CclConfig cfg, CclEvent ev, HttpClient http, ILogger logger)
    {
        string[] medal = { "🥇", "🥈", "🥉" };
        string notReady = $"📅 *{ev.Name}* ({ev.Type})\n" +
                          (ev.HasStart ? $"Mulai: {WhenWIB(cfg, ev)}\n" : "") +
                          $"Belum ada klasemen (mungkin belum mulai).\nDaftar/lihat: {Link(cfg, ev, false)}";

        if (ev.IsTeam)
        {
            var data = await CallData(cfg, "GetEventDetail", new { tournamentID = ev.Tid }, http, logger);
            var rows = (data is { } dd && dd.TryGetProperty("TeamStandings", out var ts) && ts.ValueKind == JsonValueKind.Array)
                ? ts.EnumerateArray().ToList() : new List<JsonElement>();
            if (rows.Count == 0) return notReady;
            var sb = new StringBuilder();
            sb.AppendLine($"🏆 *KLASEMEN TIM — {ev.Name}*");
            sb.AppendLine();
            for (int i = 0; i < rows.Count && i < 10; i++)
            {
                var r = rows[i];
                string pos = i < 3 ? medal[i] : $"{i + 1}.";
                string team = "";
                if (r.TryGetProperty("TeamName", out var tn) && !string.IsNullOrWhiteSpace(tn.GetString())) team = tn.GetString()!;
                else if (r.TryGetProperty("SchoolName", out var sn) && !string.IsNullOrWhiteSpace(sn.GetString())) team = sn.GetString()!;
                else if (r.TryGetProperty("TeamCode", out var tc)) team = tc.GetString() ?? "";
                sb.AppendLine($"{pos} {team} — {Score(r, "MatchPoints")} match / {Score(r, "GamePoints")} game");
            }
            if (rows.Count > 10) sb.AppendLine($"… (+{rows.Count - 10} tim lagi)");
            sb.AppendLine();
            sb.Append($"📊 Pairing & klasemen lengkap: {Link(cfg, ev, true)}");
            return sb.ToString();
        }
        else
        {
            var data = await CallData(cfg, "GetIndividualEventDetail", new { tournamentID = ev.Tid }, http, logger);
            var rows = (data is { } dd && dd.TryGetProperty("IndividualStandings", out var st) && st.ValueKind == JsonValueKind.Array)
                ? st.EnumerateArray().ToList() : new List<JsonElement>();
            if (rows.Count == 0) return notReady;
            int show = rows.Count <= 8 ? Math.Min(3, rows.Count) : 10;
            var sb = new StringBuilder();
            sb.AppendLine($"🏆 *KLASEMEN — {ev.Name}*");
            sb.AppendLine();
            for (int i = 0; i < rows.Count && i < show; i++)
            {
                var r = rows[i];
                string pos = i < 3 ? medal[i] : $"{i + 1}.";
                string name = r.TryGetProperty("PlayerName", out var pn) ? (pn.GetString() ?? "") : "";
                sb.AppendLine($"{pos} {name} — {Score(r, "Score")}");
            }
            if (rows.Count > show) sb.AppendLine($"… (+{rows.Count - show} pemain lagi)");
            sb.AppendLine();
            sb.Append($"📊 Pairing & klasemen lengkap: {Link(cfg, ev, true)}");
            return sb.ToString();
        }
    }

    // ===== Announcer: reminder event mendatang ke grup chess.com =====
    public static async Task RunLoop(Func<AppConfig> getConfig, HttpClient http, ILogger logger, string sentPath)
    {
        var sent = LoadSent(sentPath);
        while (true)
        {
            int poll = 10;
            try
            {
                var cfg = getConfig();
                var c = cfg.Ccl;
                if (c is { Enabled: true, AnnounceEnabled: true } && !string.IsNullOrWhiteSpace(c.AnnounceGroupJid))
                {
                    poll = c.PollMinutes < 1 ? 1 : c.PollMinutes;
                    bool quietSuppress = (cfg.QuietHours?.SuppressReminders ?? true)
                        && QuietHours.IsActive(cfg.QuietHours, DateTimeOffset.UtcNow);
                    if (!quietSuppress)
                        await Tick(c, cfg.GatewayUrl, http, logger, sent, sentPath);
                }
            }
            catch (Exception ex) { logger.LogError("CCL announcer error: {Msg}", ex.Message); }
            await Task.Delay(TimeSpan.FromMinutes(poll));
        }
    }

    static async Task Tick(CclConfig cfg, string gatewayUrl, HttpClient http, ILogger logger, HashSet<string> sent, string sentPath)
    {
        var (up, _) = await GetEvents(cfg, http, logger);
        var now = DateTimeOffset.UtcNow;
        var reminders = cfg.RemindersMinutes is { Length: > 0 } ? cfg.RemindersMinutes : new[] { 300, 15 };
        const int stale = 60;
        foreach (var e in up)
        {
            if (!e.HasStart) continue;
            double mins = (e.Start - now).TotalMinutes;
            foreach (int T in reminders)
            {
                if (mins <= 0 || mins > T || mins < T - stale) continue;
                string key = e.Tid + "|" + T;
                if (sent.Contains(key)) continue;
                if (await Send(http, gatewayUrl, cfg.AnnounceGroupJid, BuildReminder(cfg, e, T), logger))
                {
                    sent.Add(key); SaveSent(sentPath, sent);
                    logger.LogInformation("CCL announcer: '{Name}' reminder {T} menit", e.Name, T);
                }
            }
        }
    }

    static string BuildReminder(CclConfig cfg, CclEvent e, int T)
    {
        string head = T >= 60
            ? $"🏆 *{e.Name}* (chess.college) dimulai sekitar {T / 60} jam lagi!"
            : $"⏰ *{e.Name}* (chess.college) dimulai {T} menit lagi!";
        return head + "\n\n" +
               $"🕒 {WhenWIB(cfg, e)}\n" +
               $"🎯 {e.Type}\n" +
               $"🔗 {Link(cfg, e, false)}\n\n" +
               "Daftar & lihat detail di link di atas. Sampai jumpa di papan! ♟️";
    }

    static async Task<bool> Send(HttpClient http, string gatewayUrl, string jid, string text, ILogger logger)
    {
        try
        {
            using var content = new StringContent(JsonSerializer.Serialize(new { jid, text }), Encoding.UTF8, "application/json");
            var r = await http.PostAsync($"{gatewayUrl}/send", content);
            return r.IsSuccessStatusCode;
        }
        catch (Exception ex) { logger.LogError("CCL send gagal: {Msg}", ex.Message); return false; }
    }

    static HashSet<string> LoadSent(string path)
    {
        try { return File.Exists(path) ? (JsonSerializer.Deserialize<HashSet<string>>(File.ReadAllText(path)) ?? new()) : new(); }
        catch { return new(); }
    }
    static void SaveSent(string path, HashSet<string> sent)
    {
        try { File.WriteAllText(path, JsonSerializer.Serialize(sent)); } catch { }
    }
}
