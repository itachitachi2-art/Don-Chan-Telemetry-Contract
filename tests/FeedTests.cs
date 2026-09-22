using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.IO;
using System.Web.Script.Serialization;
using DonChan.TelemetryProbe;

internal static class FeedTests
{
    private static readonly string Session = new string('a', 32);
    private static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 2097152 };
    private static void Check(bool value, string label) { if (!value) throw new Exception(label); }
    private static Dictionary<string, object> Read(TelemetryFeed feed, string session, long after)
    { return Json.Deserialize<Dictionary<string, object>>(feed.Read(session, after)); }
    private static string Record(long n) { return "{\"sequence\":" + n + ",\"text\":\"日本語\"}"; }
    private static long Number(Dictionary<string, object> r, string k) { return Convert.ToInt64(r[k]); }
    public static void Main()
    {
        string escaped = "日本語\"\\\n\t\r\b\f\u0001";
        Check(Json.Deserialize<string>(JsonUtil.Serialize(escaped)) == escaped, "JSON escaping round trip");
        var f = new TelemetryFeed(Session, 2);
        f.Publish(1, Record(1), false); f.Publish(2, Record(2), true);
        var initial = Read(f, "", 0);
        Check((bool)initial["reset"] && Number(initial, "nextSequence") == 2, "initial live baseline");
        Check(((ICollection)initial["events"]).Count == 0, "no initial replay");
        f.Publish(3, Record(3), false); f.Publish(4, Record(4), false);
        var gap = Read(f, Session, 0);
        Check((bool)gap["gap"] && ((ICollection)gap["events"]).Count == 2, "ring eviction gap");
        Check(!(bool)Read(f, Session, 4)["gap"], "recovered cursor");
        Check((bool)Read(f, new string('b', 32), 4)["reset"], "session change");
        Check((bool)Read(f, Session, 99)["reset"], "future cursor reset");
        var paged = new TelemetryFeed(Session, 4096);
        for (int i = 1; i <= 300; i++) paged.Publish(i, Record(i), false);
        paged.Publish(301, Record(301), true);
        var page1 = Read(paged, Session, 0);
        Check((bool)page1["hasMore"] && Number(page1, "nextSequence") == 256, "page one cursor");
        var page2 = Read(paged, Session, 256);
        Check(!(bool)page2["hasMore"] && Number(page2, "nextSequence") == 301, "page two cursor");
        Check(((ICollection)page2["events"]).Count == 44, "snapshot does not skip events");
        paged.Publish(302, new string('x', TelemetryFeed.MaxRecordBytes + 1), false);
        Check((bool)Read(paged, Session, 301)["gap"], "oversize record explicitly missing");
        const int port = 18739;
        using (var server = new TelemetryServer(f, port))
        {
            var req = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:" + port + "/v1/feed?sessionId=" + Session + "&after=4");
            req.Proxy = null; req.Timeout = 3000;
            using (var response = req.GetResponse())
            using (var reader = new StreamReader(response.GetResponseStream()))
                Check(Number(Json.Deserialize<Dictionary<string, object>>(reader.ReadToEnd()), "nextSequence") == 4, "HTTP round trip");
            var bad = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:" + port + "/v1/feed");
            bad.Proxy = null; bad.Timeout = 3000; bad.Headers["Origin"] = "http://example.com";
            try { using (bad.GetResponse()) { throw new Exception("Origin was allowed"); } }
            catch (WebException e) { using (var response = (HttpWebResponse)e.Response) Check(response.StatusCode == HttpStatusCode.Forbidden, "Origin denied"); }
        }
        Console.WriteLine("PASS: escaping, baseline, eviction, sessions, pagination, oversize, HTTP, origin rejection");
    }
}
