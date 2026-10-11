// FigureShop - figur paketleri (1.2.0; 1.3.3: poseFrame)
// UserData\FigureShop\<Klasor>\ icinde model.skin olan her klasor bir figurdur: acik figur + kutulu hediye figur olarak
// Fabi Figures lisansinin altina eklenir. Her figur Mod Ayarlari (F10) > FigureShop sayfasinda kendi ac/kapa anahtarini alir.
// Marin (klasor "Marin") eski sabit ID'lerini ve ayarlarini korur; diger figurlerin ID'leri klasor adindan sabit uretilir.
// Kapali figur: oyunda kayitli kalir (rafta/depoda olanlar bozulmaz) ama lisansin urunu degildir, magazada listelenmez.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MelonLoader;
using UnityEngine;
using Il2CppProject.Code.Gameplay.Definitions;

namespace FigureShop
{
    public partial class Core
    {
        internal class Fig
        {
            public string Folder, Dir, Slug;
            public bool IsMarin;
            public int FigPickupId, FigProductId, BoxPickupId, BoxProductId;
            public string FigKey, BoxKey;
            public Func<string> NameEn, NameTr, BoxNameEn, BoxNameTr;
            public float Scale = 1f, RotY = 0f, PoseTime = 0f, FigBase, BoxBase;
            public int PoseFrame = -1;                       // 1.3.3: Figure Maker on izlemesinde secilen kare (-1 = PoseTime)
            public string Pose = "";
            public MelonPreferences_Entry<bool> Enabled;
            public PickupDefinition FigPickup, BoxPickup;
            public ProductDefinition FigProd, BoxProd;
            public Lic Lic;                                  // null = Fabi Figures (varsayilan)
            public bool On => Enabled == null || Enabled.Value;
        }

        // 1.3: kendi lisansi olan paketler (figure.json "licence" nesnesi). Ayni ada sahip paketler ayni lisansi paylasir.
        internal class Lic
        {
            public string Name, NameTr, Slug, Key, LogoPath;
            public int Id, Level = 1;
            public float Price = 1000f;
            public PickupDefinition Def;
            public readonly List<Fig> Figs = new();
        }
        static readonly List<Lic> _lics = new();
        static readonly HashSet<int> _licIds = new();
        const int LicIdBase = 1777250000, LicSlots = 4000;

        static readonly List<Fig> _figs = new();
        static Fig _cur;                                    // kurulmakta olan figur (gorunum/doku yollari icin)
        static readonly HashSet<int> _ourIds = new();       // lisans haric tum figur ID'leri
        static readonly HashSet<int> _ourProductIds = new(); // ProductDefinition ID'leri (raf, fiyat)

        const int PackIdBase = 1777300000, PackSlots = 70000;   // ModMarket'in gizli araligi (1777120000-1777199999) disinda

        static IEnumerable<Fig> BuiltFigs => _figs.Where(f => f.FigProd != null);
        static IEnumerable<PickupDefinition> CustomBrands => _lics.Where(l => l.Def != null).Select(l => l.Def);

        // kendi lisanslari: oyunun lisansindan kopya, kendi adi / fiyati / seviyesi / logosu; urunleri sadece kendi acik figurleri
        static void BuildCustomLicences(Sprite fallbackLogo)
        {
            foreach (var l in _lics)
            {
                try
                {
                    var d = UnityEngine.Object.Instantiate<PickupDefinition>(_srcBrandDef);
                    d.name = ObjPrefix + "Brand_" + l.Slug;
                    d.hideFlags = HideFlags.DontUnloadUnusedAsset;
                    d._id = l.Id; d._nameKey = l.Key; d._notAvailable = false;
                    d._price = l.Price; d._level = l.Level;
                    var logo = LoadSprite(l.LogoPath) ?? l.Figs.Select(x => x.FigProd != null ? x.FigProd._icon : null).FirstOrDefault(x => x != null) ?? fallbackLogo;
                    if (logo != null) { d._icon = logo; d._brand = logo; }
                    var pl = new Il2CppSystem.Collections.Generic.List<PickupDefinition>();
                    foreach (var f in l.Figs.Where(x => x.FigProd != null))
                    {
                        foreach (var p in new[] { f.FigPickup, f.BoxPickup }) if (p != null && logo != null) p._brand = logo;
                        if (f.On) { pl.Add(f.FigPickup); if (f.BoxPickup != null) pl.Add(f.BoxPickup); }
                    }
                    d._productsList = pl;
                    l.Def = d;
                }
                catch (Exception e) { Log.Warning($"Lisans '{l.Name}' kurulamadi: {e.Message}"); }
            }
        }

        static string Slug(string s)
        {
            var sb = new StringBuilder();
            foreach (var ch in s) sb.Append(char.IsLetterOrDigit(ch) && ch < 128 ? char.ToLowerInvariant(ch) : '_');
            var r = sb.ToString().Trim('_');
            return r.Length == 0 ? "figure" : r;
        }

        static uint Fnv(string s)
        {
            uint h = 2166136261;
            foreach (var c in s.ToLowerInvariant()) { h ^= c; h *= 16777619; }
            return h;
        }

        static Dictionary<string, string> ReadCfg(string path)
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (!File.Exists(path)) return d;
                foreach (var raw in File.ReadAllLines(path, Encoding.UTF8))
                {
                    var l = raw.Trim();
                    if (l.Length == 0 || l.StartsWith("#") || l.StartsWith(";")) continue;
                    int i = l.IndexOf('='); if (i <= 0) continue;
                    d[l.Substring(0, i).Trim()] = l.Substring(i + 1).Trim();
                }
            }
            catch (Exception e) { Log.Warning("figure.cfg okunamadi: " + path + " - " + e.Message); }
            return d;
        }

        // figure.json: duz anahtar/deger (metin, sayi); ic ice nesnelerin alanlari da "nesne.alan" ve sadece "alan" olarak okunur
        static Dictionary<string, string> ReadJson(string path)
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8),
                    new System.Text.Json.JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = System.Text.Json.JsonCommentHandling.Skip });
                void Walk(System.Text.Json.JsonElement e, string prefix)
                {
                    foreach (var p in e.EnumerateObject())
                    {
                        var v = p.Value;
                        string k = prefix.Length > 0 ? prefix + "." + p.Name : p.Name;
                        if (v.ValueKind == System.Text.Json.JsonValueKind.Object) { Walk(v, k); continue; }
                        string s = v.ValueKind == System.Text.Json.JsonValueKind.String ? v.GetString() : v.ValueKind == System.Text.Json.JsonValueKind.Number ? v.GetRawText() : v.ToString();
                        d[k] = s ?? ""; if (!d.ContainsKey(p.Name)) d[p.Name] = s ?? "";
                    }
                }
                if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object) Walk(doc.RootElement, "");
            }
            catch (Exception e) { Log.Warning("figure.json okunamadi: " + path + " - " + e.Message); }
            return d;
        }

        static void WriteDefaultJson(string path, string folder)
        {
            try
            {
                File.WriteAllText(path,
                    "{\n" +
                    $"  \"nameEnglish\": \"{folder} Figure\",\n" +
                    $"  \"nameTurkish\": \"{folder} Figürü\",\n" +
                    $"  \"boxedNameEnglish\": \"{folder} Gift Box Figure\",\n" +
                    $"  \"boxedNameTurkish\": \"{folder} Kutulu Hediye Figür\",\n" +
                    "  \"scale\": 1,\n  \"rotationY\": 0,\n  \"pose\": \"\",\n  \"poseTime\": 0,\n" +
                    "  \"basePrice\": 0,\n  \"boxedBasePrice\": 0,\n  \"licence\": \"Fabi Figures\"\n}\n", new UTF8Encoding(false));
            }
            catch { }
        }

        static float F(Dictionary<string, string> d, string k, float def) =>
            d.TryGetValue(k, out var v) && float.TryParse(v.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f) ? f : def;
        static string S(Dictionary<string, string> d, string k, string def) => d.TryGetValue(k, out var v) && v.Length > 0 ? v : def;

        static void WriteDefaultCfg(string path, string folder)
        {
            try
            {
                File.WriteAllText(path,
                    "# FigureShop figure pack - edit and restart the game\n" +
                    $"NameEnglish={folder} Figure\n" +
                    $"NameTurkish={folder} Figürü\n" +
                    $"BoxedNameEnglish={folder} Gift Box Figure\n" +
                    $"BoxedNameTurkish={folder} Kutulu Hediye Figür\n" +
                    "# size inside the vanilla figure's space (1 = same height)\n" +
                    "Scale=1\n" +
                    "# rotation in degrees (0 = faces the customer)\n" +
                    "RotationY=0\n" +
                    "# animation used as the pose (empty = first animation, - = rest pose) and the time in seconds\n" +
                    "Pose=\n" +
                    "PoseTime=0\n" +
                    "# market value (0 = automatic)\n" +
                    "BasePrice=0\n" +
                    "BoxedBasePrice=0\n", new UTF8Encoding(false));
            }
            catch { }
        }

        // UserData\FigureShop altindaki figur klasorlerini bul (OnInitializeMelon'da, oyun yuklenmeden once)
        static void DiscoverFigures(MelonPreferences_Category cat)
        {
            _figs.Clear(); _ourIds.Clear(); _ourProductIds.Clear(); _lics.Clear(); _licIds.Clear();
            var dirs = Directory.GetDirectories(_dataDir)
                .Where(d => !Path.GetFileName(d).StartsWith("_"))
                .OrderBy(d => Path.GetFileName(d).Equals("Marin", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(d => Path.GetFileName(d), StringComparer.OrdinalIgnoreCase).ToList();
            var used = new HashSet<int> { BrandId };
            foreach (var dir in dirs)
            {
                string folder = Path.GetFileName(dir);
                if (!File.Exists(Path.Combine(dir, "model.skin"))) { Log.Warning($"Figur klasoru '{folder}': model.skin yok, atlandi."); continue; }
                var f = new Fig { Folder = folder, Dir = dir, Slug = Slug(folder), IsMarin = folder.Equals("Marin", StringComparison.OrdinalIgnoreCase) };
                if (f.IsMarin)
                {
                    // eski surumlerle ayni ID/anahtar/ayarlar (kayitlar bozulmasin)
                    f.FigPickupId = FigPickupId; f.FigProductId = FigProductId; f.BoxPickupId = BoxPickupId; f.BoxProductId = BoxProductId;
                    f.FigKey = FigKey; f.BoxKey = BoxKey;
                    f.NameEn = () => _figName.Value; f.NameTr = () => _figNameTr.Value;
                    f.BoxNameEn = () => _boxName.Value; f.BoxNameTr = () => _boxNameTr.Value;
                    f.Scale = _scale.Value; f.RotY = _rotY.Value; f.Pose = _pose.Value; f.PoseTime = _poseTime.Value;
                    f.FigBase = _figBase.Value; f.BoxBase = _boxBase.Value;
                }
                else
                {
                    // figure.json (Figure Maker uretir); yoksa eski figure.cfg; ikisi de yoksa varsayilan figure.json yazilir
                    string jsonPath = Path.Combine(dir, "figure.json"), cfgPath = Path.Combine(dir, "figure.cfg");
                    if (!File.Exists(jsonPath) && !File.Exists(cfgPath)) WriteDefaultJson(jsonPath, folder);
                    var c = File.Exists(jsonPath) ? ReadJson(jsonPath) : ReadCfg(cfgPath);
                    string en = S(c, "NameEnglish", folder + " Figure"), tr = S(c, "NameTurkish", folder + " Figürü");
                    string ben = S(c, "BoxedNameEnglish", folder + " Gift Box Figure"), btr = S(c, "BoxedNameTurkish", folder + " Kutulu Hediye Figür");
                    f.NameEn = () => en; f.NameTr = () => tr; f.BoxNameEn = () => ben; f.BoxNameTr = () => btr;
                    f.Scale = F(c, "Scale", 1f); f.RotY = F(c, "RotationY", 0f); f.Pose = S(c, "Pose", ""); f.PoseTime = F(c, "PoseTime", 0f); f.PoseFrame = (int)F(c, "PoseFrame", -1f);
                    f.FigBase = F(c, "BasePrice", 0f); f.BoxBase = F(c, "BoxedBasePrice", 0f);
                    // klasor adindan sabit ID (cakisirsa sonraki bos yuva)
                    int slot = (int)(Fnv(folder) % PackSlots);
                    for (int tries = 0; tries < PackSlots; tries++, slot = (slot + 1) % PackSlots)
                    {
                        int b = PackIdBase + slot * 10;
                        if (!used.Contains(b + 1) && !used.Contains(b + 2) && !used.Contains(b + 3) && !used.Contains(b + 4)) { f.FigPickupId = b + 1; f.FigProductId = b + 2; f.BoxPickupId = b + 3; f.BoxProductId = b + 4; break; }
                    }
                    f.FigKey = "FigureShop/fig_" + f.Slug; f.BoxKey = "FigureShop/fig_" + f.Slug + "_boxed";
                    // lisans: bos / "Fabi Figures" = bizim lisans; baska ad = kendi lisansi (Figure Maker formu)
                    string ln = S(c, "licence.name", c.TryGetValue("licence", out var lv) ? lv : "");
                    if (ln.Length > 0 && !ln.StartsWith("Fabi Figures", StringComparison.OrdinalIgnoreCase))
                    {
                        string ls = Slug(ln);
                        var lic = _lics.FirstOrDefault(x => x.Slug == ls);
                        if (lic == null)
                        {
                            lic = new Lic { Name = ln, NameTr = S(c, "licence.nameTurkish", ln), Slug = ls, Key = "FigureShop/licence_" + ls,
                                            Price = F(c, "licence.price", 1000f), Level = Math.Max(1, (int)F(c, "licence.level", 1f)) };
                            string logo = S(c, "licence.logo", "licence_logo.png");
                            lic.LogoPath = Path.IsPathRooted(logo) ? logo : Path.Combine(dir, logo);
                            int ls2 = (int)(Fnv("licence:" + ls) % LicSlots);
                            while (_licIds.Contains(LicIdBase + ls2 * 10 + 1) || used.Contains(LicIdBase + ls2 * 10 + 1)) ls2 = (ls2 + 1) % LicSlots;
                            lic.Id = LicIdBase + ls2 * 10 + 1; _licIds.Add(lic.Id); used.Add(lic.Id);
                            _lics.Add(lic);
                        }
                        f.Lic = lic; lic.Figs.Add(f);
                    }
                    if (c.TryGetValue("credits.title", out var ct) && ct.Length > 0) Log.Msg($"{folder}: \"{ct}\" by {S(c, "credits.author", "?")}, {S(c, "credits.license", "?")} {S(c, "credits.source", "")}");
                }
                foreach (var id in new[] { f.FigPickupId, f.FigProductId, f.BoxPickupId, f.BoxProductId }) { used.Add(id); _ourIds.Add(id); }
                _ourProductIds.Add(f.FigProductId); _ourProductIds.Add(f.BoxProductId);
                // Mod Ayarlari'nda ac/kapa (ModSettings MelonPreferences girdilerini kendiliginden listeler)
                f.Enabled = cat.CreateEntry("Figure_" + f.Slug, true, "Figure: " + f.NameEn(),
                    $"Sell '{f.NameEn()}' (loose + gift box) under the Fabi Figures licence. Off = hidden from the shop; ones already in the shop stay. Restart needed.");
                _figs.Add(f);
            }
            foreach (var l in _lics) Log.Msg($"Kendi lisansi: '{l.Name}' (ID {l.Id}, fiyat {l.Price}, seviye {l.Level}) - figurler: {string.Join(", ", l.Figs.Select(x => x.Folder))}");
            Log.Msg($"Figurler: {_figs.Count} bulundu - " + string.Join(", ", _figs.Select(x => $"{x.Folder} ({(x.On ? "acik" : "kapali")}, {x.FigPickupId})")));
        }
    }
}
