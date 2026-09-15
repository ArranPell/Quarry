// Phase 48. Reads the two hosted wiki-data files the module already downloads and emits one small,
// purpose-built file -- derived_subpages.json -- carrying only what Quarry actually
// reads out of subPages.json (70 MB): WikiLocationService's coordinates/place-names/Title, and the
// Inspector's Description/ImageUrl for pages an achievement objective can actually link to. See
// docs/PLAN.md's Phase 48 and docs/analysis/DATA-INDEPENDENCE-REVIEW-2026-09-13.md §1/§4.1 for why
// these fields and not others.
//
// Run: dotnet run --project src/DerivedSubpageGenerator -- [output-path]
// Defaults to src/Quarry/ref/derived_subpages.json, which ships in the .bhm (Phase 48's
// embed decision, DECISIONS.md 2026-09-13/14) -- so refreshing this file means rebuilding the module,
// not a runtime download.

using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

const string AchievementDataUrl = "https://bhm.blishhud.com/Denrage.AchievementTrackerModule/data/achievement_data.json";
const string SubPagesUrl = "https://bhm.blishhud.com/Denrage.AchievementTrackerModule/data/subPages.json";

// WikiLocationService.cs:44 -- keep in sync; this is the module's real place-name key set, not the
// 7-key set the first-pass measurement doc used.
var placeNameKeys = new HashSet<string>(StringComparer.Ordinal) { "Zone", "Location", "Locations", "Area", "Region" };

// FormattedLabelHtmlService's tag switch: these five are the only ones that survive to the screen as
// something other than plain text (SubPageInformationConverter puts Location at TypeDiscriminator 1;
// the module's own TODO comments confirm <b>/<small>/<big>/<h3> etc. render as plain text today).
var visualTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "a", "i", "s", "img", "br" };

var outputPath = args.Length > 0
    ? args[0]
    : Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Quarry", "ref", "derived_subpages.json");
outputPath = Path.GetFullPath(outputPath);

using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };

Console.WriteLine($"Downloading {AchievementDataUrl} ...");
var achievementDataBytes = await http.GetByteArrayAsync(AchievementDataUrl);
Console.WriteLine($"  {achievementDataBytes.Length / 1024.0 / 1024.0:F1} MB");

Console.WriteLine($"Downloading {SubPagesUrl} ...");
var subPagesBytes = await http.GetByteArrayAsync(SubPagesUrl);
Console.WriteLine($"  {subPagesBytes.Length / 1024.0 / 1024.0:F1} MB");

static string NormalizeLink(string link)
{
    if (string.IsNullOrEmpty(link)) return null;
    return link.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? link : "https://wiki.guildwars2.com" + link;
}

// Every EntryList row's Link, across every achievement's table -- the set of subpages an Inspector
// objective view can ever actually navigate to. Only CollectionDescription/ObjectivesDescription carry
// EntryList, but scanning any object with that property name is equivalent and doesn't need this
// standalone tool to know the achievement_data.json type hierarchy.
var reachableLinks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
using (var achDoc = JsonDocument.Parse(achievementDataBytes))
{
    void Walk(JsonElement el)
    {
        if (el.ValueKind == JsonValueKind.Object)
        {
            if (el.TryGetProperty("EntryList", out var entryList) && entryList.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in entryList.EnumerateArray())
                {
                    if (entry.TryGetProperty("Link", out var linkProp) && linkProp.ValueKind == JsonValueKind.String)
                    {
                        var normalized = NormalizeLink(linkProp.GetString());
                        if (normalized != null) reachableLinks.Add(normalized);
                    }
                }
            }

            foreach (var prop in el.EnumerateObject())
            {
                Walk(prop.Value);
            }
        }
        else if (el.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in el.EnumerateArray()) Walk(item);
        }
    }

    Walk(achDoc.RootElement);
}

Console.WriteLine($"Reachable (objective-linked) subpage links: {reachableLinks.Count}");

string FlattenToVisualTags(string html)
{
    if (string.IsNullOrEmpty(html)) return html;
    return Regex.Replace(html, "</?([a-zA-Z0-9]+)[^>]*>", m => visualTags.Contains(m.Groups[1].Value) ? m.Value : string.Empty);
}

var derived = new Dictionary<string, DerivedSubpageRecord>(StringComparer.OrdinalIgnoreCase);
int total = 0, kept = 0;

using (var doc = JsonDocument.Parse(subPagesBytes))
{
    foreach (var page in doc.RootElement.EnumerateArray())
    {
        total++;
        var typeDiscriminator = page.TryGetProperty("TypeDiscriminator", out var tdEl) ? tdEl.GetInt32() : -1;
        var isLocationType = typeDiscriminator == 1; // Item=0, Location=1, Npc=2, Quest=3, Text=4

        if (!page.TryGetProperty("TypeValue", out var tv)) continue;

        var link = tv.TryGetProperty("Link", out var l) && l.ValueKind == JsonValueKind.String ? l.GetString() : null;
        if (string.IsNullOrEmpty(link)) continue;

        var title = tv.TryGetProperty("Title", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;
        var description = tv.TryGetProperty("Description", out var d) && d.ValueKind == JsonValueKind.String ? d.GetString() : null;
        var imageUrl = tv.TryGetProperty("ImageUrl", out var iu) && iu.ValueKind == JsonValueKind.String ? iu.GetString() : null;

        string coordinates = null;
        if (tv.TryGetProperty("InteractiveMap", out var imEl) && imEl.ValueKind == JsonValueKind.Object &&
            imEl.TryGetProperty("Coordinates", out var coordEl) && coordEl.ValueKind == JsonValueKind.String)
        {
            coordinates = coordEl.GetString();
        }

        List<PlaceValue> places = null;
        if (tv.TryGetProperty("DescriptionList", out var dl) && dl.ValueKind == JsonValueKind.Array)
        {
            foreach (var kv in dl.EnumerateArray())
            {
                if (kv.TryGetProperty("Key", out var kEl) && kEl.ValueKind == JsonValueKind.String &&
                    kv.TryGetProperty("Value", out var vEl) && vEl.ValueKind == JsonValueKind.String &&
                    placeNameKeys.Contains(kEl.GetString()))
                {
                    (places ??= new List<PlaceValue>()).Add(new PlaceValue { Key = kEl.GetString(), Value = vEl.GetString() });
                }
            }
        }

        var normalizedLink = NormalizeLink(link);
        var reachable = reachableLinks.Contains(normalizedLink);
        var hasLocationData = coordinates != null || places != null || (isLocationType && !string.IsNullOrEmpty(title));

        if (!reachable && !hasLocationData) continue;

        kept++;
        derived[normalizedLink] = new DerivedSubpageRecord
        {
            Coordinates = coordinates,
            Title = isLocationType ? title : null,
            Places = places,
            Description = reachable ? FlattenToVisualTags(description) : null,
            ImageUrl = reachable ? imageUrl : null,
        };
    }
}

Console.WriteLine($"Kept {kept} of {total} subpages.");

var options = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
};
var json = JsonSerializer.Serialize(derived, options);
var jsonBytes = Encoding.UTF8.GetBytes(json);

Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
File.WriteAllBytes(outputPath, jsonBytes);

using var gzipMs = new MemoryStream();
using (var gz = new GZipStream(gzipMs, CompressionLevel.Optimal, true))
{
    gz.Write(jsonBytes, 0, jsonBytes.Length);
}

Console.WriteLine($"Wrote {outputPath}");
Console.WriteLine($"  raw {jsonBytes.Length / 1024.0 / 1024.0:F2} MB, gzip {gzipMs.Length / 1024.0 / 1024.0:F2} MB");

class DerivedSubpageRecord
{
    public string Coordinates { get; set; }
    public string Title { get; set; }
    public List<PlaceValue> Places { get; set; }
    public string Description { get; set; }
    public string ImageUrl { get; set; }
}

class PlaceValue
{
    public string Key { get; set; }
    public string Value { get; set; }
}
