// FigureShop - Anime Shop Simulator (by Fabianens)
// Kendi satis lisansimiz ("Fabi Figures") ve altinda figur urunleri.
// Ilk surum: Marin Kitagawa - acik figur (Figure, siparis kutusunda 6 adet) + kutulu hediye figur (BoxFigures, 4 adet).
// Lisans ve urunler oyundaki mevcut lisans/urunlerden kopyalanir (adet, raf yerlesimi, el ayarlari kaynaktan gelir).
// Gorunum: AnimeStatuePack'teki Marin modeli sabit bir pozda statik meshe donusturulur ve kaynak figurun boyutuna sigdirilir.
//
// Model: "Marin Kitagawa from Dress up Darling"
// https://sketchfab.com/3d-models/marin-kitagawa-from-dress-up-darling-ef75d04c0a42413692a5b59f938428b7
// by Lappy, CC-BY-4.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;
using UnityEngine.Rendering;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppFishNet;
using Il2CppFishNet.Object;
using Il2CppGPUInstancer;
using Il2CppI2.Loc;
using Il2CppProject.Code.Gameplay.Configs;
using Il2CppProject.Code.Gameplay.Controllers;
using Il2CppProject.Code.Gameplay.Definitions;
using Il2CppProject.Code.Gameplay.Player.Products;
using Il2CppProject.Code.Gameplay.Services;
using DDef = Il2CppProject.Code.Core.Utils.DataDefinition.DataDefinition;
using PickupDB = Il2CppProject.Code.Core.Utils.DataDefinition.DataDefinition<Il2CppProject.Code.Gameplay.Definitions.PickupDefinition>;
using ProductDB = Il2CppProject.Code.Core.Utils.DataDefinition.DataDefinition<Il2CppProject.Code.Gameplay.Definitions.ProductDefinition>;
using DefType = Il2CppProject.Code.Gameplay.Definitions.PickupType;
using NV3 = System.Numerics.Vector3;

[assembly: MelonInfo(typeof(FigureShop.Core), "FigureShop", "1.3.3", "Fabianens")]
[assembly: MelonGame("OneMoreTime", "Anime Shop Simulator")]

namespace FigureShop
{
    public partial class Core : MelonMod
    {
        // --- sabitler (degistirme: kayit dosyalari bu ID'lere bagli) ---
        // ModMarket 1777120000-1777199999 araligini normal magazadan gizler; lisans normal listede gorunsun diye bu aralik disinda.
        const int BrandId = 1777210001;
        const int FigPickupId = 1777210011, FigProductId = 1777210012;
        const int BoxPickupId = 1777210021, BoxProductId = 1777210022;
        const string ObjPrefix = "FigureShop_";
        const string BrandKey = "FigureShop/brand_fabianens_figures";
        const string FigKey = "FigureShop/marin_figure";
        const string BoxKey = "FigureShop/marin_figure_boxed";
        const string FigureFolder = "Marin";
        const string Credit = "Model: \"Marin Kitagawa from Dress up Darling\" (https://sketchfab.com/3d-models/marin-kitagawa-from-dress-up-darling-ef75d04c0a42413692a5b59f938428b7) by Lappy (Sketchfab), CC-BY-4.0";

        static Core _inst;
        static MelonLogger.Instance Log => _inst.LoggerInstance;

        static MelonPreferences_Entry<string> _brandName, _brandNameTr, _figName, _figNameTr, _boxName, _boxNameTr, _pose;
        static MelonPreferences_Entry<float> _brandPrice, _scale, _rotY, _poseTime, _figBase, _boxBase;
        static MelonPreferences_Entry<int> _brandLevel, _srcBrand, _srcFig, _srcBox;
        static MelonPreferences_Entry<bool> _logCatalog, _flipBox, _unpriced;

        static string _dataDir, _figDir;
        static bool _built, _failed, _building;
        static PickupDefinition _brand, _srcBrandDef, _srcFigPickup, _srcBoxPickup;
        static readonly List<NetworkObject> _netPrefabs = new();
        static IntPtr _registeredNm = IntPtr.Zero;
        static ProductsConfig _cfgCache;
        static ProductsController _pcCache;
        static bool _diagDone;
        float _nextCheck;
        bool _sceneReady;

        public override void OnInitializeMelon()
        {
            _inst = this;
            var cat = MelonPreferences.CreateCategory("FigureShop");
            _brandName = cat.CreateEntry("LicenceName", "Fabi Figures - Fabianens", "Licence name (all languages except Turkish)");
            _brandNameTr = cat.CreateEntry("LicenceNameTurkish", "Fabi Figures - Fabianens", "Licence name when the game language is Turkish");
            _brandPrice = cat.CreateEntry("LicencePrice", 1000f, "Licence price (0 = same as the vanilla licence it is copied from)");
            _figBase = cat.CreateEntry("FigureBasePrice", 0f, "Base (market) value of the figure (0 = its own automatic value, not the copied vanilla figure's)");
            _boxBase = cat.CreateEntry("BoxedFigureBasePrice", 0f, "Base (market) value of the boxed figure (0 = its own automatic value)");
            _brandLevel = cat.CreateEntry("LicenceLevel", 1, "Licence unlock level (0 = same as the vanilla licence)");
            _figName = cat.CreateEntry("FigureName", "Marin Kitagawa Figure", "Figure name (all languages except Turkish)");
            _figNameTr = cat.CreateEntry("FigureNameTurkish", "Marin Kitagawa Figürü", "Figure name when the game language is Turkish");
            _boxName = cat.CreateEntry("BoxedFigureName", "Marin Kitagawa Gift Box Figure", "Boxed figure name (all languages except Turkish)");
            _boxNameTr = cat.CreateEntry("BoxedFigureNameTurkish", "Marin Kitagawa Kutulu Hediye Figür", "Boxed figure name when the game language is Turkish");
            _scale = cat.CreateEntry("FigureScale", 1.0f, "Figure size multiplier inside the vanilla figure's size (restart needed)");
            _rotY = cat.CreateEntry("FigureRotationY", 0f, "Figure rotation in degrees (restart needed)");
            // eski varsayilanlari bir kere yenilere cevir (kullanici degistirdiyse dokunma)
            var defFix = cat.CreateEntry("Defaults2", false, "internal", null, true);
            if (!defFix.Value)
            {
                _brandPrice.Value = 1000f;   // 1.0: varsayilan lisans fiyati 1000 (bir kere)
                if (_brandName.Value == "Fabi Figures") _brandName.Value = "Fabi Figures - Fabianens";
                if (_brandNameTr.Value == "Fabi Figures") _brandNameTr.Value = "Fabi Figures - Fabianens";
                defFix.Value = true;
            }
            var rotFix = cat.CreateEntry("RotationFixed", false, "internal", null, true);
            if (!rotFix.Value) { if (Mathf.Approximately(_rotY.Value, 180f)) _rotY.Value = 0f; rotFix.Value = true; }
            _pose = cat.CreateEntry("Pose", "Pose", "Animation used as the fixed pose ('-' = rest pose, restart needed)");
            _poseTime = cat.CreateEntry("PoseTime", 0f, "Time in seconds inside the pose animation (restart needed)");
            _srcBrand = cat.CreateEntry("SourceLicenceId", 0, "ID of the vanilla licence copied (0 = automatic, see catalog.txt)");
            _srcFig = cat.CreateEntry("SourceFigureId", 0, "Pickup ID of the vanilla Figure product copied (0 = automatic)");
            _srcBox = cat.CreateEntry("SourceBoxedFigureId", 0, "Pickup ID of the vanilla BoxFigures product copied (0 = automatic)");
            _flipBox = cat.CreateEntry("FlipGiftBox", false, "Turn the gift box 180 degrees (restart needed)");
            _logCatalog = cat.CreateEntry("LogCatalog", true, "Write the vanilla licence/figure catalog to the log once (also saved to UserData\\FigureShop\\catalog.txt)");
            _unpriced = cat.CreateEntry("StartUnpriced", true, "Figures are never priced when first put on a shelf: the tag stays empty until the player sets a price");

            _dataDir = Path.Combine(MelonEnvironment.UserDataDirectory, "FigureShop");
            _figDir = Path.Combine(_dataDir, FigureFolder);
            Directory.CreateDirectory(_figDir);
            if (!File.Exists(Path.Combine(_figDir, "model.skin")))
                LoggerInstance.Warning("Model dosyasi yok: " + Path.Combine(_figDir, "model.skin") + " (derle.bat kopyalar; paketten kurulduysa zip'i tekrar acin)");
            // 1.2: UserData\FigureShop altindaki her figur klasoru (Marin + paketler); her biri Mod Ayarlari'nda ac/kapa
            DiscoverFigures(cat);

            ApplyPatches();
            LoggerInstance.Msg($"Ready. Data folder: {_dataDir}");
            LoggerInstance.Msg(Credit);
        }

        // ------------------------------------------------------------------ Harmony

        void ApplyPatches()
        {
            void P(Type t, string name, string prefix, string postfix = null, string finalizer = null, int postPriority = -1)
            {
                try
                {
                    var m = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                             .FirstOrDefault(x => x.Name == name && !x.IsGenericMethod);
                    if (m == null) { LoggerInstance.Warning($"Patch: {t.Name}.{name} bulunamadi"); return; }
                    HarmonyMethod H(string n) => n == null ? null : new HarmonyMethod(typeof(Core).GetMethod(n, BindingFlags.Static | BindingFlags.NonPublic));
                    var post = H(postfix); if (post != null && postPriority >= 0) post.priority = postPriority;
                    HarmonyInstance.Patch(m, prefix: H(prefix), postfix: post, finalizer: H(finalizer));
                }
                catch (Exception e) { LoggerInstance.Warning($"Patch {t.Name}.{name}: {e.Message}"); }
            }
            // kayit: oyun sozlukleri ve urun listesini kurmadan once bizim tanimlar listelerde olsun
            P(typeof(ProductsConfig), nameof(ProductsConfig.Initialize), nameof(CfgPrefix), nameof(CfgPostfix));
            P(typeof(ProductsConfig), nameof(ProductsConfig.RebuildDefinitions), nameof(CfgPrefix));
            P(typeof(ProductsController), nameof(ProductsController.AddProducts), nameof(PcPrefix));
            P(typeof(ProductsController), nameof(ProductsController.AddBrands), nameof(PcPrefix));
            P(typeof(ProductsController), nameof(ProductsController.OnStartServer), nameof(PcPrefix));
            // gorunum: bizim urunler GPU Instancer'a girmesin (yoksa kaynak figurun prototipi cizilir), MeshRenderer ile cizilsin
            P(typeof(PoolService), nameof(PoolService.SetProductInstancing), nameof(ProductInstancingPrefix));
            // 1.1.2: figurler rafa ilk konuldugunda fiyatsiz (etiket bos); oyuncu fiyat verince bir daha dokunulmaz
            P(typeof(ProductPlace), nameof(ProductPlace.AddItemServer), null, nameof(AddItemServerPostfix));
            P(typeof(ProductsController), nameof(ProductsController.ChangeProductPriceLogic), null, nameof(PriceChangedPostfix));
            P(typeof(PoolService), nameof(PoolService.SetPrefabInstancing), nameof(PrefabInstancingPrefix));
            // bilgisayar: lisans sekmesi listesi bizim lisansi icermiyorsa ekle (tani + garanti)
            // bilgisayar listeleri (1.1): lisans ve urunler sadece ModMarket'in MODLAR ekraninda (RegisterItem ..., modMarketOnly); burada sadece lisans sirasi.
            // Ilk postfix (ModMarket'tan once) bizimkilerin yerini not eder, son postfix (ModMarket'tan sonra) eksikleri geri koyar ve lisansi seviyesine gore siralar.
            P(typeof(Il2CppProject.Code.Gameplay.UI.Computer.MenuOrdersWidget), nameof(Il2CppProject.Code.Gameplay.UI.Computer.MenuOrdersWidget.GetContentPickups), null, nameof(ContentPickupsFirst), null, HarmonyLib.Priority.First);
            P(typeof(Il2CppProject.Code.Gameplay.UI.Computer.MenuOrdersWidget), nameof(Il2CppProject.Code.Gameplay.UI.Computer.MenuOrdersWidget.GetContentPickups), null, nameof(ContentPickupsPostfix), null, HarmonyLib.Priority.Last);

        }

        // ModMarket (varsa): MODLAR ekraninda mi? (ozel alan, yansima ile okunur)
        static FieldInfo _mmModeField; static bool _mmChecked;
        static bool ModMarketModMode()
        {
            if (!_mmChecked)
            {
                _mmChecked = true;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    if (asm.GetName().Name == "ModMarket") { _mmModeField = asm.GetType("ModMarket.Core")?.GetField("_modMode", BindingFlags.Static | BindingFlags.NonPublic); break; }
            }
            try { return _mmModeField != null && (bool)_mmModeField.GetValue(null); } catch { return false; }
        }

        static readonly List<(int index, PickupDefinition def)> _seen = new();
        static void ContentPickupsFirst(Il2CppSystem.Collections.Generic.List<PickupDefinition> __result)
        {
            _seen.Clear();
            if (!_built || __result == null) return;
            try
            {
                for (int i = 0; i < __result.Count; i++)
                {
                    var d = __result[i];
                    if (d != null && IsOurId(d._id)) _seen.Add((i, d));
                }
            }
            catch { }
        }

        static int _contentLogs;
        static void ContentPickupsPostfix(int contentIndex, Il2CppSystem.Collections.Generic.List<PickupDefinition> __result)
        {
            if (!_built || _brand == null || __result == null) return;
            try
            {
                // 1.1: normal magazaya geri koyma yok (urunler sadece ModMarket'te)
                _seen.Clear();
                // bizimkini cikar, sonra seviyesine gore dogru siraya koy (listenin sonunda kalip gozden kacmasin)
                int brands = 0; bool ours = false; int oldPos = -1;
                for (int i = __result.Count - 1; i >= 0; i--)
                {
                    var d = __result[i]; if (d == null) continue;
                    if (d._type != DefType.Brand) continue;
                    brands++;
                    if (d._id == BrandId) { ours = true; oldPos = i; __result.RemoveAt(i); brands--; }
                }
                if (!ours) return;   // listede yoksa (or. normal magaza) ekleme
                if (brands == 0) { __result.Insert(oldPos, _brand); return; }
                int lvl = _brand._level, firstBrand = -1, insertAt = -1;
                for (int i = 0; i < __result.Count; i++)
                {
                    var d = __result[i]; if (d == null || d._type != DefType.Brand) continue;
                    if (firstBrand < 0) firstBrand = i;
                    if (d._level <= lvl) insertAt = i + 1;
                }
                if (insertAt < 0) insertAt = firstBrand;
                __result.Insert(insertAt, _brand);
                if (_contentLogs++ < 8) Log.Msg($"Lisans listesi #{contentIndex}: {brands + 1} lisans, '{_brandName.Value}' sira {insertAt + 1} (onceki {(ours ? (oldPos + 1).ToString() : "yok")})");
            }
            catch (Exception e) { if (_contentLogs++ < 8) Log.Warning("Lisans listesi: " + e.Message); }
        }

        static int _bpLogs;
        static void BrandProductsPostfix(Il2CppSystem.Collections.Generic.List<PickupDefinition> __result)
        {
            if (!_built || __result == null) return;
            try
            {
                bool ours = false;
                for (int i = 0; i < __result.Count; i++) { var d = __result[i]; if (d != null && _ourIds.Contains(d._id)) ours = true; }
                if (ours && _bpLogs++ < 3) Log.Msg($"Lisans urunleri gosteriliyor: {__result.Count}");
            }
            catch { }
        }

        static void CfgPrefix(ProductsConfig __instance) { SafeEnsure(__instance, false); }
        static void CfgPostfix(ProductsConfig __instance) { if (_built) { try { EnsureRegistered(__instance); } catch { } } }
        static void PcPrefix(ProductsController __instance)
        {
            ProductsConfig cfg = null;
            try { cfg = __instance._productsConfig; } catch { }
            SafeEnsure(cfg ?? FindAll<ProductsConfig>().FirstOrDefault(), true);
        }

        static void SafeEnsure(ProductsConfig cfg, bool rebuild)
        {
            if (cfg == null || _failed || _building) return;
            try
            {
                if (!_built) TryBuild(cfg);
                else if (EnsureInLists(cfg) && rebuild) { try { cfg.RebuildDefinitions(); } catch { } }
            }
            catch (Exception e) { _failed = true; Log.Error("Kayit hatasi, mod durduruldu: " + e); }
        }

        static bool IsOurs(GameObject go)
        {
            try { return go != null && go.name != null && go.name.StartsWith(ObjPrefix, StringComparison.Ordinal); } catch { return false; }
        }

        // Rafta oyun urunun kendi MeshRenderer'ini kapatip GPU Instancer ile cizer (odaklaninca kendi renderer'i acilir).
        // Bizim urunler GPUI'ye girmez (kaynak figur cizilirdi); onun yerine oyunun GPUI istedigi her an "FigureShop_Draw" kopyasini aciyoruz.
        const string DrawName = "FigureShop_Draw";
        static void ProductInstancingPrefix(ProductItem productItem, ref bool useInstancing)
        {
            if (productItem == null || !IsOurs(productItem.gameObject)) return;
            useInstancing = false;
            if (_spawnQueue.Count < 1024) _spawnQueue.Add(productItem);
        }

        // ana renderer'in kopyasi (ayni mesh ve malzemeler), baslangicta kapali
        static void AddDrawCopy(MeshRenderer main)
        {
            if (main == null) return;
            var mf = main.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;
            var go = new GameObject(DrawName);
            go.layer = main.gameObject.layer;
            go.transform.SetParent(main.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = main.sharedMaterials;
            r.shadowCastingMode = main.shadowCastingMode;
            r.receiveShadows = main.receiveShadows;
            r.enabled = false;
        }
        static void PrefabInstancingPrefix(GPUInstancerPrefab gpuiPrefab, ref bool useInstancing)
        {
            if (useInstancing && gpuiPrefab != null && IsOurs(gpuiPrefab.gameObject)) useInstancing = false;
        }

        // ------------------------------------------------------------------ yedek dongu (yamalar kacirirsa)

        // ModMarket (varsa): lisans ve urunler MODLAR ekraninda da listelensin. Normal magazadan gizlenmeleri ContentPickupsPostfix ile engellenir.
        public override void OnLateInitializeMelon()
        {
            try
            {
                Type t = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    if (asm.GetName().Name == "ModMarket") { t = asm.GetType("ModMarket.Core"); break; }
                var m = t?.GetMethod("RegisterItem", new[] { typeof(int), typeof(string), typeof(bool) });
                if (m == null) { LoggerInstance.Error(t == null ? "Fabianens Mod Market is REQUIRED (Mods/ModMarket.dll not found) - the licence cannot be bought." : "Fabianens Mod Market 1.4.0 or newer is REQUIRED - please update ModMarket."); return; }
                // 1.1: sadece MODLAR ekraninda, kendi kategorilerinde (lisans: Licences, figurler: Figures)
                m.Invoke(null, new object[] { BrandId, "Licences", true });
                foreach (var l in _lics) m.Invoke(null, new object[] { l.Id, "Licences", true });
                foreach (var fg in _figs) foreach (var id in new[] { fg.FigPickupId, fg.BoxPickupId }) m.Invoke(null, new object[] { id, "Figures", true });
                LoggerInstance.Msg("Mod Market: licence and figures listed only on the MODS screen (Licences / Figures).");
            }
            catch (Exception e) { LoggerInstance.Warning("Mod Market registration: " + e.Message); }
        }

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            _sceneReady = true;
            _nextCheck = Time.unscaledTime + 3f;
        }

        // Rafta oyun urunun kendi renderer'ini kapatir (GPUI cizsin diye). Bizim urunler GPUI'de olmadigi icin o an kopyayi gosteriyoruz.
        // Kutu icindeyken (siparis kutusu) ve elde/odakta (ana renderer acik) kopya kapali.
        static readonly List<(ProductItem item, MeshRenderer main, MeshRenderer copy, bool on)> _drawn = new();
        static readonly HashSet<IntPtr> _drawnPtrs = new();
        // 1.3.1 (takilma): sahne taramasi 1 sn yerine 30 sn'de bir (yedek). Rafa konan / yuklenen figurler SetProductInstancing
        // prefix'inden kuyruga girer ve bir sonraki karede listeye eklenir. Kopyasi henuz yoksa 10 sn boyunca tekrar denenir.
        static readonly List<ProductItem> _spawnQueue = new();
        static readonly List<(ProductItem pi, float until)> _retry = new();
        static float _nextScan, _nextRetry;
        static void SyncDrawCopies()
        {
            if (!_built) return;
            float now = Time.unscaledTime;
            if (now >= _nextScan)
            {
                // yedek tarama: 120 sn'de bir ve sadece listede olmayan figurler eklenir (eskiden 30 sn'de bir her sey silinip
                // bastan kuruluyordu). Yeni / havuzdan gelen figurler zaten SetProductInstancing kuyrugundan gelir.
                _nextScan = now + 120f;
                _spawnQueue.Clear(); _retry.Clear();
                _drawnPtrs.Clear();
                for (int i = 0; i < _drawn.Count; i++) _drawnPtrs.Add(_drawn[i].item.Pointer);
                foreach (var o in UnityEngine.Object.FindObjectsByType(Il2CppType.Of<ProductItem>(), FindObjectsSortMode.None))
                {
                    var pi = o?.TryCast<ProductItem>(); if (pi == null) continue;
                    if (_drawnPtrs.Contains(pi.Pointer)) continue;
                    int hint = 0; try { hint = pi._definitionIdHint; } catch { }
                    if (hint != 0 && !_ourProductIds.Contains(hint)) continue;
                    if (hint == 0 && !IsOurs(pi.gameObject)) continue;
                    TryAddDrawn(pi);
                }
            }
            else
            {
                if (_spawnQueue.Count > 0)
                {
                    for (int i = 0; i < _spawnQueue.Count; i++)
                    {
                        var pi = _spawnQueue[i];
                        try { if (pi != null && !pi.WasCollected && !TryAddDrawn(pi)) _retry.Add((pi, now + 10f)); } catch { }
                    }
                    _spawnQueue.Clear();
                }
                if (_retry.Count > 0 && now >= _nextRetry)
                {
                    _nextRetry = now + 0.5f;
                    for (int i = _retry.Count - 1; i >= 0; i--)
                    {
                        var (pi, until) = _retry[i];
                        bool done = true;
                        try { done = pi == null || pi.WasCollected || TryAddDrawn(pi) || now > until; } catch { }
                        if (done) _retry.RemoveAt(i);
                    }
                }
            }
            // kopya ana renderer'in alt objesi: ikisinin kontrolu yeter. Yok edilenler listeden cikar.
            // Kopyanin acik/kapali durumu listede tutulur (onu sadece biz degistiriyoruz), her karede okunmaz.
            for (int i = _drawn.Count - 1; i >= 0; i--)
            {
                var d = _drawn[i];
                try
                {
                    if (d.main.WasCollected || d.copy.WasCollected) { _drawn.RemoveAt(i); continue; }
                    bool want = (!d.main.enabled || d.main.forceRenderingOff) && !d.item._isInsideBox;
                    if (d.on != want) { d.copy.enabled = want; d.on = want; _drawn[i] = d; }
                }
                catch { _drawn.RemoveAt(i); }
            }
        }

        // figurun ana renderer'i + "Draw" kopyasi listeye (ayni nesnenin eski kaydi varsa yenisiyle degisir). Kopya yoksa false.
        static bool TryAddDrawn(ProductItem pi)
        {
            var main = pi._meshRenderer; if (main == null) return false;
            var ct = main.transform.Find(DrawName); if (ct == null) return false;
            var copy = ct.GetComponent<MeshRenderer>(); if (copy == null) return false;
            IntPtr p = pi.Pointer;
            _drawn.RemoveAll(d => d.item.Pointer == p);
            _drawn.Add((pi, main, copy, copy.enabled));
            return true;
        }

        public override void OnUpdate()
        {
            try { SyncDrawCopies(); } catch { }
            try { WatchUnpriced(); } catch { }
            if (_failed || !_sceneReady || Time.unscaledTime < _nextCheck) return;
            _nextCheck = Time.unscaledTime + (_built ? 15f : 2f);
            try
            {
                if (_cfgCache == null || _cfgCache.WasCollected) _cfgCache = FindAll<ProductsConfig>().FirstOrDefault();
                if (_cfgCache == null) return;
                if (!_built) { SafeEnsure(_cfgCache, true); return; }
                EnsureRegistered(_cfgCache);
                EnsureNetworkPrefabs();
                EnsureLocalization(false);
                EnsureController();
                EnsureBoxIcon();
            }
            catch (Exception e)
            {
                _failed = true;
                LoggerInstance.Error("Beklenmeyen hata, mod durduruldu: " + e);
            }
        }

        // ------------------------------------------------------------------ build once

        static void TryBuild(ProductsConfig cfg)
        {
            if (cfg._productsDefinitions == null || cfg._brandDefinitions == null || cfg._productsDefinitions.Count == 0) return;
            _building = true;
            try { Build(cfg); }
            finally { _building = false; }
        }

        static void Build(ProductsConfig cfg)
        {
            var products = ToList(cfg._productsDefinitions);
            var brands = ToList(cfg._brandDefinitions);
            if (_logCatalog.Value || !File.Exists(Path.Combine(_dataDir, "catalog.txt"))) LogCatalog(brands, products);

            // kaynaklar
            _srcFigPickup = Pick(products, _srcFig.Value, EProductType.Figure, null);
            if (_srcFigPickup == null) { Log.Warning("Kaynak Figure urunu bulunamadi (SourceFigureId). catalog.txt'ye bakin."); _failed = true; return; }
            _srcBoxPickup = Pick(products, _srcBox.Value, EProductType.BoxFigures, _srcFigPickup.ProductDefinition);
            if (_srcBoxPickup == null) Log.Warning("Kaynak BoxFigures urunu bulunamadi - sadece acik figur eklenecek.");
            _srcBrandDef = _srcBrand.Value != 0 ? brands.FirstOrDefault(b => SafeId(b) == _srcBrand.Value) : null;
            if (_srcBrandDef == null) _srcBrandDef = brands.FirstOrDefault(b => ListHasId(b._productsList, SafeId(_srcFigPickup))) ?? brands.FirstOrDefault();
            if (_srcBrandDef == null) { Log.Warning("Kaynak lisans bulunamadi."); _failed = true; return; }
            Log.Msg($"Kaynaklar: lisans '{SafeName(_srcBrandDef)}' ({SafeId(_srcBrandDef)}), figur '{SafeName(_srcFigPickup)}' ({SafeId(_srcFigPickup)}), kutulu '{(_srcBoxPickup != null ? SafeName(_srcBoxPickup) : "-")}' ({(_srcBoxPickup != null ? SafeId(_srcBoxPickup) : 0)})");

            var holder = new GameObject(ObjPrefix + "Prefabs");
            holder.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(holder);
            holder.hideFlags = HideFlags.HideAndDontSave;

            var brandIcon = LoadSprite(Path.Combine(_dataDir, "brand_icon.png"));

            // 1.2: her figur klasoru icin acik figur + kutulu hediye figur (kapali olanlar da kurulur: kayitlarda olabilirler)
            foreach (var f in _figs)
            {
                _cur = f;
                try
                {
                    var icon = LoadSprite(Path.Combine(f.Dir, "icon.png")) ?? LoadSprite(Path.Combine(f.Dir, "figure_icon.png"));
                    var boxIcon = LoadSprite(Path.Combine(f.Dir, "box_icon.png")) ?? icon;
                    if (brandIcon == null) brandIcon = icon;
                    var baked = BakeModel();
                    f.FigProd = CloneProduct(_srcFigPickup.ProductDefinition, f.FigProductId, f.FigKey, icon, holder, f.Slug + "_Figure", baked, false);
                    // 1.1.3: kendi piyasa degeri (kopyalanan oyun figurunun degeri degil)
                    f.FigProd._basePrice = f.FigBase > 0 ? f.FigBase : AutoBase(f.FigKey, 34f, 62f);
                    f.FigPickup = ClonePickup(_srcFigPickup, f.FigPickupId, f.FigKey, f.FigProd, icon, null);
                    if (_srcBoxPickup != null)
                    {
                        f.BoxProd = CloneProduct(_srcBoxPickup.ProductDefinition, f.BoxProductId, f.BoxKey, boxIcon, holder, f.Slug + "_BoxedFigure", baked, true);
                        f.BoxProd._basePrice = f.BoxBase > 0 ? f.BoxBase : AutoBase(f.BoxKey, 55f, 85f);
                        f.BoxPickup = ClonePickup(_srcBoxPickup, f.BoxPickupId, f.BoxKey, f.BoxProd, boxIcon, null);
                    }
                }
                catch (Exception e) { Log.Warning($"Figur '{f.Folder}' kurulamadi: {e.Message}"); f.FigProd = null; f.FigPickup = null; f.BoxProd = null; f.BoxPickup = null; }
            }
            _cur = null;
            foreach (var f in BuiltFigs.Where(x => x.Lic == null)) foreach (var p in new[] { f.FigPickup, f.BoxPickup }) if (p != null && brandIcon != null) p._brand = brandIcon;

            // --- lisans
            _brand = UnityEngine.Object.Instantiate<PickupDefinition>(_srcBrandDef);
            _brand.name = ObjPrefix + "Brand_FabiFigures";
            _brand.hideFlags = HideFlags.DontUnloadUnusedAsset;
            _brand._id = BrandId;
            _brand._nameKey = BrandKey;
            _brand._notAvailable = false;
            if (_brandPrice.Value > 0) _brand._price = _brandPrice.Value;
            if (_brandLevel.Value > 0) _brand._level = _brandLevel.Value;
            if (brandIcon != null) { _brand._icon = brandIcon; _brand._brand = brandIcon; }
            var plist = new Il2CppSystem.Collections.Generic.List<PickupDefinition>();
            // sadece acik figurler lisansin urunu (kapali olanlar magazada gorunmez)
            foreach (var f in BuiltFigs.Where(x => x.On && x.Lic == null)) { plist.Add(f.FigPickup); if (f.BoxPickup != null) plist.Add(f.BoxPickup); }
            _brand._productsList = plist;
            // 1.3: kendi lisansi olan paketler
            BuildCustomLicences(brandIcon);

            _built = true;
            EnsureInLists(cfg);
            // config henuz kurulmadiysa (Initialize oncesi) oyun kendisi kuracak; kurulduysa yeniden kur
            bool builtAlready = false;
            try { builtAlready = cfg._definitionsBuilt; } catch { }
            if (builtAlready) { try { cfg.RebuildDefinitions(); } catch (Exception e) { Log.Warning("RebuildDefinitions: " + e.Message); } }
            EnsureRegistered(cfg);
            EnsureLocalization(true);
            Log.Msg($"'{_brandName.Value}' lisansi eklendi (ID {BrandId}, fiyat {_brand._price}, seviye {_brand._level}) - urunler: " +
                    $"{BuiltFigs.Count()} figur ({BuiltFigs.Count(x => x.On)} acik): " + string.Join(", ", BuiltFigs.Select(x => $"{x.Folder} {x.FigPickupId}")));
        }

        static PickupDefinition Pick(List<PickupDefinition> products, int wantId, EProductType type, ProductDefinition preferInside)
        {
            if (wantId != 0)
            {
                var w = products.FirstOrDefault(p => SafeId(p) == wantId);
                if (w != null) return w;
                Log.Warning($"ID {wantId} bulunamadi, otomatik seciliyor.");
            }
            var cands = products.Where(p => !IsOurId(SafeId(p)) && TypeOf(p) == type && p.ProductDefinition?.Prefab != null).ToList();
            if (preferInside != null)
            {
                var m = cands.FirstOrDefault(p => { try { return p.ProductDefinition._boxData?.ProductDefinitionInside?._id == preferInside._id; } catch { return false; } });
                if (m != null) return m;
            }
            return cands.FirstOrDefault(p => !p._notAvailable) ?? cands.FirstOrDefault();
        }

        static ProductDefinition CloneProduct(ProductDefinition src, int id, string key, Sprite icon, GameObject holder, string objName, List<BakedPart> baked, bool boxed)
        {
            var go = UnityEngine.Object.Instantiate<GameObject>(src.Prefab.gameObject, holder.transform);
            go.name = ObjPrefix + objName;
            var item = go.GetComponent<ProductItem>();
            // GPU Instancer prototipi kaynak figurun (Ali) prototipi: rafta onu cizer. Prototipi bosaltinca oyun (PoolService.EnsureRuntimeDataReady)
            // bu urunu GPUI'ye eklemez ve normal MeshRenderer ile - yani bizim Marin meshimizle - cizer.
            foreach (var gp in go.GetComponentsInChildren<GPUInstancerPrefab>(true)) { gp.prefabPrototype = null; }

            var def = UnityEngine.Object.Instantiate<ProductDefinition>(src);
            def.name = ObjPrefix + objName;
            def.hideFlags = HideFlags.DontUnloadUnusedAsset;
            def._id = id;
            def._nameKey = key;
            def._prefab = item;
            if (icon != null) def._icon = icon;

            if (boxed)
            {
                // kutu acilinca ne cikiyorsa (figur ise) bizim figurumuz ciksin
                try
                {
                    var bd = src._boxData;
                    if (bd != null)
                    {
                        var nb = new BoxData();
                        nb.ProductDefinitionInside = bd.ProductDefinitionInside;
                        nb.InsideCount = bd.InsideCount;
                        nb.WaitTime = bd.WaitTime;
                        if (bd.ProductDefinitionInside != null && bd.ProductDefinitionInside._type == EProductType.Figure && _cur?.FigProd != null) nb.ProductDefinitionInside = _cur.FigProd;
                        def._boxData = nb;
                        Log.Msg($"Kutu verisi: icinde {(nb.ProductDefinitionInside != null ? nb.ProductDefinitionInside.name : "-")} x{nb.InsideCount}");
                    }
                }
                catch (Exception e) { Log.Warning("BoxData: " + e.Message); }
            }

            LogHierarchy(go, objName);
            try
            {
                if (boxed) BuildBoxVisual(go, item, baked);
                else BuildFigureVisual(go, item, baked);
            }
            catch (Exception e) { Log.Warning($"Gorunum ({objName}): {e}"); }

            var no = go.GetComponent<NetworkObject>();
            if (no != null)
            {
                try { no.SetInitializedValues(null, true); } catch (Exception e) { Log.Warning("SetInitializedValues: " + e.Message); }
                _netPrefabs.Add(no);
            }
            return def;
        }

        static PickupDefinition ClonePickup(PickupDefinition src, int id, string key, ProductDefinition prod, Sprite icon, Sprite brandLogo)
        {
            var p = UnityEngine.Object.Instantiate<PickupDefinition>(src);
            p.name = ObjPrefix + "Pickup_" + key.Replace('/', '_');
            p.hideFlags = HideFlags.DontUnloadUnusedAsset;
            p._id = id;
            p._nameKey = key;
            p._productDefinition = prod;
            p._notAvailable = false;
            if (icon != null) p._icon = icon;
            if (brandLogo != null) p._brand = brandLogo;
            return p;
        }

        // ------------------------------------------------------------------ gorunum

        // Modeli bir kere pozla (tum urunler ayni pisirilmis veriyi kullanir)
        static List<BakedPart> BakeModel()
        {
            string path = Path.Combine(_cur.Dir, "model.skin");
            if (!File.Exists(path)) { Log.Warning("Model dosyasi yok: " + path + " - kaynak figurun gorunumu kalacak."); return null; }
            var t0 = DateTime.Now;
            var model = SkinModel.Load(path);
            SkinClip clip = null;
            if (_cur.Pose != "-" && model.Clips.Count > 0) clip = model.Clips.FirstOrDefault(c => c.Name == _cur.Pose) ?? model.Clips[0];
            // figure.json poseFrame (Figure Maker 1.3+): tam kare; karenin ortasindaki zaman, yuvarlama bir onceki kareye dusurmesin
            float time = clip != null && _cur.PoseFrame >= 0 ? (Math.Min(_cur.PoseFrame, clip.Frames - 1) + 0.5f) / clip.Fps : _cur.PoseTime;
            var parts = model.Bake(clip, time);
            Log.Msg($"Model pozlandi: {parts.Sum(p => p.Pos.Length)} vertex, {parts.Count} parca, poz '{clip?.Name ?? "dinlenme"}'{(clip != null ? $" kare {(int)(time * clip.Fps)}" : "")} ({(DateTime.Now - t0).TotalMilliseconds:0} ms). Animasyonlar: {string.Join(", ", model.Clips.Select(c => c.Name))}");
            return parts;
        }

        static void BuildFigureVisual(GameObject go, ProductItem item, List<BakedPart> baked)
        {
            if (baked == null) return;
            var mr = item != null ? item._meshRenderer : null;
            if (mr == null) mr = go.GetComponentInChildren<MeshRenderer>(true);
            if (mr == null) { Log.Warning("Figur prefab'inda MeshRenderer yok."); return; }
            var target = RendererBounds(mr, go.transform);
            ReplaceWithModel(mr, go.transform, target, baked);
            // diger gorunur parcalari (varsa) gizle
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                if (r.Pointer != mr.Pointer) { r.enabled = false; r.forceRenderingOff = true; }
            AddDrawCopy(mr);
        }

        // Kutulu: oyunun kutusu kalir. Kutunun icinde/penceresinde ayri bir figur parcasi varsa o degistirilir.
        static void BuildBoxVisual(GameObject go, ProductItem item, List<BakedPart> baked)
        {
            var main = item != null ? item._meshRenderer : null;
            // kutunun on yuzu (pencere) bu prefabda arkaya bakiyor: govdeyi kendi merkezi etrafinda 180 derece cevir.
            // Icteki figur daha sonra kok uzayina gore pisirildigi icin musteriye bakmaya devam eder.
            if (main != null && _flipBox.Value)
            {
                try
                {
                    var t = main.transform; var par = t.parent;
                    var rb = RendererBounds(main, par != null ? par : go.transform);
                    var q = Quaternion.Euler(0f, 180f, 0f);
                    var cpos = rb.center; cpos.y = t.localPosition.y;
                    t.localPosition = cpos + q * (t.localPosition - cpos);
                    t.localRotation = q * t.localRotation;
                }
                catch (Exception e) { Log.Warning("Kutu dondurme: " + e.Message); }
            }
            Renderer inner = null;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (main != null && r.Pointer == main.Pointer) continue;
                string n = (r.gameObject.name ?? "").ToLowerInvariant();
                if (r.TryCast<MeshRenderer>() != null && (n.Contains("fig") || n.Contains("statue") || n.Contains("doll") || n.Contains("toy") || n.Contains("inside")))
                { inner = r; break; }
            }
            if (inner != null && baked != null)
            {
                ReplaceWithModel(inner.Cast<MeshRenderer>(), go.transform, RendererBounds(inner, go.transform), baked);
                _innerName = inner.gameObject.name;
                Log.Msg($"Kutudaki figur parcasi degistirildi: {inner.gameObject.name}");
            }
            else Log.Msg("Kutulu figur: kutu modelinde ayri figur parcasi yok, oyunun kutusu kullaniliyor.");

            // kutu resmi (istege bagli): UserData\FigureShop\Marin\box_art.png
            string art = Path.Combine(_cur.Dir, "box_art.png");
            var tex = File.Exists(art) ? LoadTexture(art) : null;
            if (tex != null && main != null)
            {
                var mats = main.sharedMaterials;
                var nm = new Il2CppReferenceArray<Material>(mats.Length);
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i] != null ? new Material(mats[i]) : null;
                    if (m != null) { m.name = ObjPrefix + "BoxArt"; m.hideFlags = HideFlags.DontUnloadUnusedAsset; SetTex(m, "_BaseMap", tex); SetTex(m, "_MainTex", tex); }
                    nm[i] = m;
                }
                main.sharedMaterials = nm;
                Log.Msg("Kutu resmi uygulandi: " + art);
            }
            else Log.Msg("TODO: kutu resmi degismedi (box_art.png yok) - kutu uzerinde kaynak urunun resmi gorunur.");
            AddDrawCopy(main);
        }

        // Pisirilmis modeli tek mesh (malzeme basina alt mesh) olarak renderer'a koy, hedef sinirlarin icine sigdir.
        static void ReplaceWithModel(MeshRenderer mr, Transform root, Bounds target, List<BakedPart> baked)
        {
            var mf = mr.GetComponent<MeshFilter>();
            if (mf == null) { Log.Warning("MeshFilter yok: " + mr.gameObject.name); return; }
            Material baseMat = null;
            try { baseMat = mr.sharedMaterial; } catch { }

            // model uzayi -> Y ekseninde dondur -> sinirlar
            float ry = _cur.RotY * MathF.PI / 180f, cs = MathF.Cos(ry), sn = MathF.Sin(ry);
            NV3 Rot(NV3 v) => new NV3(cs * v.X + sn * v.Z, v.Y, -sn * v.X + cs * v.Z);
            var lo = new NV3(float.MaxValue); var hi = new NV3(float.MinValue);
            foreach (var p in baked) foreach (var v in p.Pos) { var r = Rot(v); lo = NV3.Min(lo, r); hi = NV3.Max(hi, r); }
            var size = hi - lo;
            // boy kaynak figurun boyu kadar; genislik en fazla kaynagin 1.3 kati (kollar/sac tasmasin diye)
            float s = MathF.Min(target.size.y / Nz(size.Y), 1.3f * MathF.Min(target.size.x / Nz(size.X), target.size.z / Nz(size.Z))) * _cur.Scale;
            // ayaklar: en alttaki %8'lik dilimin ortasi kaidenin/yerin ortasina gelsin (egik pozda one/arkaya kaymasin)
            float footY = lo.Y + size.Y * 0.08f; double fx = 0, fz = 0; int fn = 0;
            foreach (var p in baked) foreach (var v in p.Pos) { var r = Rot(v); if (r.Y <= footY) { fx += r.X; fz += r.Z; fn++; } }
            float ax = fn > 0 ? (float)(fx / fn) : (lo.X + hi.X) * 0.5f, az = fn > 0 ? (float)(fz / fn) : (lo.Z + hi.Z) * 0.5f;
            var tc = target.center;
            var off = new NV3(tc.x - ax * s, target.min.y - lo.Y * s, tc.z - az * s);

            // kok uzayi -> renderer'in yerel uzayi
            var m = mr.transform.worldToLocalMatrix * root.localToWorldMatrix;
            var A = new Aff { m00 = m.m00, m01 = m.m01, m02 = m.m02, m03 = m.m03, m10 = m.m10, m11 = m.m11, m12 = m.m12, m13 = m.m13, m20 = m.m20, m21 = m.m21, m22 = m.m22, m23 = m.m23 };

            // ayni doku/kesme ayarli parcalar tek alt mesh
            var groups = new List<(string key, BakedPart first, List<int> idx)>();
            var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>();
            foreach (var p in baked)
            {
                int vb = verts.Count;
                for (int i = 0; i < p.Pos.Length; i++)
                {
                    var v = A.Point(Rot(p.Pos[i]) * s + off);
                    var n = A.Vector(Rot(p.Nrm[i]));
                    float l = n.Length(); if (l > 1e-8f) n /= l;
                    verts.Add(new Vector3(v.X, v.Y, v.Z));
                    norms.Add(new Vector3(n.X, n.Y, n.Z));
                    uvs.Add(new Vector2(p.Src.Uv[i * 2], p.Src.Uv[i * 2 + 1]));
                }
                string key = $"{p.Src.Texture}|{p.Src.ClipMode}|{p.Src.DoubleSided}";
                var g = groups.FirstOrDefault(x => x.key == key);
                if (g.idx == null) { g = (key, p, new List<int>()); groups.Add(g); }
                foreach (var ix in p.Src.Idx) g.idx.Add(ix + vb);
            }

            var mesh = new Mesh { name = ObjPrefix + _cur.Slug + "Mesh" };
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = verts.ToArray();
            mesh.normals = norms.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.subMeshCount = groups.Count;
            for (int i = 0; i < groups.Count; i++) mesh.SetTriangles(groups[i].idx.ToArray(), i, false);
            mesh.RecalculateBounds();
            mesh.hideFlags = HideFlags.DontUnloadUnusedAsset;
            mf.sharedMesh = mesh;

            var mats = new Il2CppReferenceArray<Material>(groups.Count);
            for (int i = 0; i < groups.Count; i++) mats[i] = MakeMaterial(baseMat, groups[i].first.Src);
            mr.sharedMaterials = mats;
            mr.shadowCastingMode = ShadowCastingMode.On;
            Log.Msg($"Model yerlestirildi: {mr.gameObject.name}, {verts.Count} vertex, {groups.Count} malzeme, olcek {s:0.0000}, hedef boyut {target.size}");
        }

        static Material MakeMaterial(Material baseMat, SkinMesh part)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            Material m;
            if (baseMat != null && baseMat.shader != null && baseMat.shader.name == "Universal Render Pipeline/Lit") m = new Material(baseMat);
            else if (lit != null) m = new Material(lit);
            else m = baseMat != null ? new Material(baseMat) : new Material(Shader.Find("Standard"));
            m.name = ObjPrefix + part.Name;
            m.hideFlags = HideFlags.DontUnloadUnusedAsset;
            try { m.enableInstancing = false; } catch { }

            var tex = string.IsNullOrEmpty(part.Texture) ? null : LoadTexture(Path.Combine(_cur.Dir, part.Texture));
            SetTex(m, "_BaseMap", tex); SetTex(m, "_MainTex", tex);
            SetColor(m, "_BaseColor", Color.white); SetColor(m, "_Color", Color.white);
            foreach (var p in new[] { "_BumpMap", "_MetallicGlossMap", "_OcclusionMap", "_EmissionMap", "_SpecGlossMap", "_ParallaxMap", "_DetailAlbedoMap", "_DetailNormalMap" }) SetTex(m, p, null);
            foreach (var k in new[] { "_NORMALMAP", "_METALLICSPECGLOSSMAP", "_OCCLUSIONMAP", "_EMISSION", "_PARALLAXMAP", "_DETAIL_MULX2", "_SPECGLOSSMAP" }) m.DisableKeyword(k);
            SetColor(m, "_EmissionColor", Color.black);
            SetFloat(m, "_Metallic", 0f);
            SetFloat(m, "_Smoothness", 0.25f);
            SetFloat(m, "_Glossiness", 0.25f);
            SetFloat(m, "_Surface", 0f);
            m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            SetFloat(m, "_SrcBlend", 1f); SetFloat(m, "_DstBlend", 0f); SetFloat(m, "_ZWrite", 1f);
            if (part.ClipMode != 0)
            {
                SetFloat(m, "_AlphaClip", 1f);
                SetFloat(m, "_Cutoff", part.ClipMode == 2 ? 0.3f : 0.5f);
                m.EnableKeyword("_ALPHATEST_ON");
                m.renderQueue = 2450;
            }
            else
            {
                SetFloat(m, "_AlphaClip", 0f);
                m.DisableKeyword("_ALPHATEST_ON");
                m.renderQueue = 2000;
            }
            SetFloat(m, "_Cull", part.DoubleSided ? 0f : 2f);
            return m;
        }

        static readonly Dictionary<string, Texture2D> _texCache = new();
        static Texture2D LoadTexture(string p)
        {
            if (_texCache.TryGetValue(p, out var t)) return t;
            if (!File.Exists(p)) { Log.Warning("Doku yok: " + p); _texCache[p] = null; return null; }
            t = new Texture2D(2, 2, TextureFormat.RGBA32, true);
            if (!ImageConversion.LoadImage(t, File.ReadAllBytes(p), true)) { Log.Warning("Doku okunamadi: " + p); t = null; }
            else { t.name = Path.GetFileName(p); t.hideFlags = HideFlags.DontUnloadUnusedAsset; t.wrapMode = TextureWrapMode.Repeat; t.anisoLevel = 4; }
            _texCache[p] = t;
            return t;
        }

        // Kutulu figur ikonu: kutunun (icinde Marin) resmi. Yoksa bir kere ekran disi kamerayla cizilir ve box_icon.png olarak saklanir.
        // Oyun betikleri calismasin diye prefab degil, sadece gorunen mesh'lerin kopyalari cizilir.
        // Ikonu olmayan figurler: oyun acilirken (sahne hazir olunca, bir kere) urunun kendisinden ikon cizilir ve saklanir.
        // Acik figur: icon.png yoksa figure_icon.png; kutulu figur: box_icon.png. Sonraki acilislarda kayitli dosya kullanilir.
        static bool _iconsDone;
        static void EnsureBoxIcon()
        {
            if (_iconsDone || !_built) return;
            _iconsDone = true;
            foreach (var f in BuiltFigs)
            {
                if (!File.Exists(Path.Combine(f.Dir, "icon.png"))) RenderIcon(f.FigProd, f.FigPickup, Path.Combine(f.Dir, "figure_icon.png"));
                if (f.BoxProd != null) RenderIcon(f.BoxProd, f.BoxPickup, Path.Combine(f.Dir, "box_icon.png"));
            }
            // Figure Maker icin: icindeki figur olmadan hediye kutusu resmi + figurun duracagi alan (box_template.png/.json)
            var tf = BuiltFigs.FirstOrDefault(x => x.BoxProd != null);
            if (tf != null) RenderIcon(tf.BoxProd, null, Path.Combine(_dataDir, "box_template.png"), true);
        }

        static string _innerName;   // kutulu prefabda figurun durdugu parca (sablonda cizilmez)
        static void RenderIcon(ProductDefinition prod, PickupDefinition pick, string path, bool template = false)
        {
            if (prod == null || File.Exists(path)) return;   // kurulumda LoadSprite ile zaten yuklendi
            GameObject root = null, camGo = null, l1 = null, l2 = null; RenderTexture rt = null;
            try
            {
                const int layer = 31, size = 256;
                var src = prod.Prefab != null ? prod.Prefab.transform : null;
                if (src == null) return;
                root = new GameObject(ObjPrefix + "IconRig");
                root.transform.position = new Vector3(0f, -500f, 0f);
                var b = new Bounds(); bool any = false;
                var inner = new List<Vector3>();   // sablon: figurun koselerinin dunya konumlari
                foreach (var mr in src.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (mr == null || !mr.enabled || mr.forceRenderingOff) continue;
                    var mf = mr.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;
                    bool ourFig = template && ((mf.sharedMesh.name ?? "").StartsWith(ObjPrefix, StringComparison.Ordinal) || (_innerName != null && mr.gameObject.name == _innerName));
                    if (template) Log.Msg($"Kutu sablonu parcasi: {mr.gameObject.name} mesh={mf.sharedMesh.name} {(ourFig ? "(figur - cizilmez)" : "")}");
                    var go = new GameObject("m"); go.layer = layer;
                    go.transform.SetParent(root.transform, false);
                    // prefab kokune gore yerel donusum
                    var m = src.worldToLocalMatrix * mr.transform.localToWorldMatrix;
                    go.transform.localPosition = new Vector3(m.m03, m.m13, m.m23);
                    go.transform.localRotation = m.rotation;
                    go.transform.localScale = m.lossyScale;
                    go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                    var r = go.AddComponent<MeshRenderer>(); r.sharedMaterials = mr.sharedMaterials;
                    if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
                    if (ourFig)
                    {
                        var lb = mf.sharedMesh.bounds;
                        for (int k = 0; k < 8; k++) inner.Add(go.transform.TransformPoint(new Vector3((k & 1) == 0 ? lb.min.x : lb.max.x, (k & 2) == 0 ? lb.min.y : lb.max.y, (k & 4) == 0 ? lb.min.z : lb.max.z)));
                        r.enabled = false;   // sablonda figur cizilmez
                    }
                }
                if (!any) return;
                // on taraftan: urunun on yuzu (pencere, figurun baktigi yon) prefabda -Z; hafif yukaridan ve yandan
                var dir = (Quaternion.Euler(12f, 25f, 0f) * Vector3.back).normalized;
                float rad = b.extents.magnitude;
                camGo = new GameObject(ObjPrefix + "IconCam");
                var cam = camGo.AddComponent<Camera>();
                cam.enabled = false; cam.cullingMask = 1 << layer; cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0f, 0f, 0f, 0f); cam.fieldOfView = 25f;
                cam.nearClipPlane = 0.01f; cam.farClipPlane = 100f;
                float dist = rad / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.05f;
                camGo.transform.position = b.center + dir * dist;
                camGo.transform.LookAt(b.center);
                Light L(string n, Vector3 euler, float i) { var g = new GameObject(n); var li = g.AddComponent<Light>(); li.type = LightType.Directional; li.intensity = i; li.cullingMask = 1 << layer; g.transform.rotation = Quaternion.Euler(euler); return li; }
                l1 = L(ObjPrefix + "IconKey", new Vector3(30f, 200f, 0f), 1.3f).gameObject;
                l2 = L(ObjPrefix + "IconFill", new Vector3(10f, 20f, 0f), 0.6f).gameObject;
                rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;
                cam.Render();
                var prev = RenderTexture.active; RenderTexture.active = rt;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, size, size), 0, 0); tex.Apply();
                RenderTexture.active = prev;
                File.WriteAllBytes(path, ImageConversion.EncodeToPNG(tex));
                UnityEngine.Object.Destroy(tex);
                if (template)
                {
                    // figurun resimdeki alani (0-1, sol ust kose), Figure Maker figur resmini buraya yerlestirir
                    float x0 = 1, y0 = 1, x1 = 0, y1 = 0;
                    foreach (var w in inner) { var v = cam.WorldToViewportPoint(w); x0 = Mathf.Min(x0, v.x); x1 = Mathf.Max(x1, v.x); y0 = Mathf.Min(y0, 1f - v.y); y1 = Mathf.Max(y1, 1f - v.y); }
                    var ci = System.Globalization.CultureInfo.InvariantCulture;
                    File.WriteAllText(Path.ChangeExtension(path, ".json"), inner.Count == 0 ? "{}" :
                        "{ \"x0\": " + x0.ToString("0.0000", ci) + ", \"y0\": " + y0.ToString("0.0000", ci) + ", \"x1\": " + x1.ToString("0.0000", ci) + ", \"y1\": " + y1.ToString("0.0000", ci) + " }");
                    Log.Msg("Kutu sablonu olusturuldu: " + path);
                    return;
                }
                var sp = LoadSprite(path);
                if (sp != null) { prod._icon = sp; if (pick != null) pick._icon = sp; }
                Log.Msg("Figur ikonu olusturuldu: " + path);
            }
            catch (Exception e) { Log.Warning("Kutu ikonu: " + e.Message); }
            finally
            {
                foreach (var o in new UnityEngine.Object[] { root, camGo, l1, l2 }) if (o != null) UnityEngine.Object.Destroy(o);
                if (rt != null) { rt.Release(); UnityEngine.Object.Destroy(rt); }
            }
        }

        static Sprite LoadSprite(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                if (!ImageConversion.LoadImage(tex, File.ReadAllBytes(path), false)) { Log.Warning("Ikon okunamadi: " + path); return null; }
                tex.hideFlags = HideFlags.DontUnloadUnusedAsset;
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.Apply(true, true);
                var sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                sp.name = ObjPrefix + Path.GetFileNameWithoutExtension(path);
                sp.hideFlags = HideFlags.DontUnloadUnusedAsset;
                return sp;
            }
            catch (Exception e) { Log.Warning("Ikon: " + e.Message); return null; }
        }

        static void SetTex(Material m, string n, Texture t) { if (m.HasProperty(n)) m.SetTexture(n, t); }
        static void SetColor(Material m, string n, Color c) { if (m.HasProperty(n)) m.SetColor(n, c); }
        static void SetFloat(Material m, string n, float v) { if (m.HasProperty(n)) m.SetFloat(n, v); }
        static float Nz(float v) => MathF.Abs(v) < 1e-6f ? 1f : v;

        // renderer'in mesh sinirlari, root'un yerel uzayinda (pasif objelerde de calisir)
        static Bounds RendererBounds(Renderer r, Transform root)
        {
            Mesh mesh = null;
            var mf = r.GetComponent<MeshFilter>();
            if (mf != null) mesh = mf.sharedMesh;
            var b = mesh != null ? mesh.bounds : new Bounds(Vector3.zero, Vector3.one * 0.2f);
            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue); var max = -min;
            for (int i = 0; i < 8; i++)
            {
                var p = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                var lp = root.InverseTransformPoint(r.transform.TransformPoint(p));
                min = Vector3.Min(min, lp); max = Vector3.Max(max, lp);
            }
            var res = new Bounds(); res.SetMinMax(min, max); return res;
        }

        static void LogHierarchy(GameObject go, string label)
        {
            try
            {
                var sb = new StringBuilder($"Prefab '{label}' parcalari:\n");
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    var mf = r.GetComponent<MeshFilter>();
                    string mesh = mf != null && mf.sharedMesh != null ? $"{mf.sharedMesh.name} ({mf.sharedMesh.vertexCount} v)" : "-";
                    string mat = r.sharedMaterial != null ? $"{r.sharedMaterial.name} [{r.sharedMaterial.shader?.name}]" : "-";
                    sb.Append($"  {TPath(r.transform, go.transform)}  {r.GetIl2CppType().Name}  mesh={mesh}  mat={mat}\n");
                }
                var gp = go.GetComponentInChildren<GPUInstancerPrefab>(true);
                sb.Append($"  GPUI: {(gp != null ? gp.gameObject.name : "yok")}, NetworkObject: {(go.GetComponent<NetworkObject>() != null ? "var" : "yok")}");
                Log.Msg(sb.ToString());
            }
            catch (Exception e) { Log.Warning("Hiyerarsi: " + e.Message); }
        }

        static string TPath(Transform t, Transform root)
        {
            var parts = new List<string>();
            for (var c = t; c != null && c.Pointer != root.Pointer; c = c.parent) parts.Add(c.name);
            parts.Reverse();
            return parts.Count == 0 ? "(kok)" : string.Join("/", parts);
        }

        // ------------------------------------------------------------------ katalog

        static void LogCatalog(List<PickupDefinition> brands, List<PickupDefinition> products)
        {
            try
            {
                var brandOf = new Dictionary<int, string>();
                var sb = new StringBuilder("FigureShop katalog - lisanslar ve figur urunleri\n\nLISANSLAR (id, ad, fiyat, seviye, urun sayisi):\n");
                foreach (var b in brands)
                {
                    int cnt = 0;
                    try { var l = b._productsList; if (l != null) { cnt = l.Count; for (int i = 0; i < l.Count; i++) if (l[i] != null) brandOf[SafeId(l[i])] = SafeName(b); } } catch { }
                    sb.Append($"  {SafeId(b)}  '{SafeName(b)}'  fiyat={b._price}  seviye={b._level}  urun={cnt}{(b._notAvailable ? "  (kapali)" : "")}\n");
                }
                sb.Append("\nFIGURLER (pickup id, ad, urun id, tur, siparis fiyati, taban fiyat, lisans, kutu ici):\n");
                foreach (var p in products)
                {
                    var t = TypeOf(p);
                    if (t != EProductType.Figure && t != EProductType.BoxFigures) continue;
                    var pd = p.ProductDefinition;
                    string inside = "-";
                    try { var bd = pd?._boxData; if (bd != null && bd.ProductDefinitionInside != null) inside = $"{bd.ProductDefinitionInside.name} x{bd.InsideCount}"; } catch { }
                    sb.Append($"  {SafeId(p)}  '{SafeName(p)}'  urun={SafeId(pd)}  {t}  fiyat={p._price}  taban={(pd != null ? pd._basePrice : 0)}  lisans='{(brandOf.TryGetValue(SafeId(p), out var bn) ? bn : "-")}'  kutu={inside}{(p._notAvailable ? "  (kapali)" : "")}\n");
                }
                File.WriteAllText(Path.Combine(_dataDir, "catalog.txt"), sb.ToString());
                if (_logCatalog.Value) { Log.Msg(sb.ToString()); _logCatalog.Value = false; MelonPreferences.Save(); }
            }
            catch (Exception e) { Log.Warning("Katalog: " + e.Message); }
        }

        // ------------------------------------------------------------------ kayitli tut

        static IEnumerable<(PickupDefinition def, Il2CppSystem.Collections.Generic.List<PickupDefinition> list)> Wanted(ProductsConfig cfg)
        {
            yield return (_brand, cfg._brandDefinitions);
            foreach (var b in CustomBrands) yield return (b, cfg._brandDefinitions);
            foreach (var f in BuiltFigs)
            {
                yield return (f.FigPickup, cfg._productsDefinitions);
                if (f.BoxPickup != null) yield return (f.BoxPickup, cfg._productsDefinitions);
            }
        }

        // listelerde yoksa ekle; bir sey eklendiyse true
        static readonly Dictionary<(IntPtr, int), int> _listCounts = new();
        static bool EnsureInLists(ProductsConfig cfg)
        {
            if (!_built) return false;
            bool changed = false;
            foreach (var (def, list) in Wanted(cfg))
            {
                if (def == null || list == null) continue;
                // liste degismediyse (ayni liste, ayni sayi) tekrar tarama: her eleman icin oyuna cagri yapmak takilma yapar
                var k = (list.Pointer, def._id); int n = list.Count;
                if (_listCounts.TryGetValue(k, out int last) && last == n) continue;
                if (!ListHasId(list, SafeId(def))) { list.Add(def); changed = true; n = list.Count; }
                _listCounts[k] = n;
            }
            return changed;
        }

        static void EnsureRegistered(ProductsConfig cfg)
        {
            if (!_built || cfg == null) return;
            if (EnsureInLists(cfg)) { try { cfg.RebuildDefinitions(); } catch { } }
            try
            {
                var pd = cfg._pickupDict;
                if (pd != null)
                {
                    foreach (var d in AllPickups())
                        if (d != null && !pd.ContainsKey(d._id)) pd.Add(d._id, d);
                }
                var prd = cfg._productDict;
                var ppd = cfg._productPickupDict;
                foreach (var f in BuiltFigs)
                    foreach (var (prod, pick) in new[] { (f.FigProd, f.FigPickup), (f.BoxProd, f.BoxPickup) })
                    {
                        if (prod == null) continue;
                        if (prd != null && !prd.ContainsKey(prod._id)) prd.Add(prod._id, prod);
                        if (ppd != null && !ppd.ContainsKey(prod._id)) ppd.Add(prod._id, pick);
                    }
            }
            catch (Exception e) { Log.Warning("Config sozlukleri: " + e.Message); }
            try
            {
                var d = PickupDB.s_DefinitionsById;
                if (d != null)
                    foreach (var x in AllPickups())
                        if (x != null && !d.ContainsKey(x._id)) d.Add(x._id, x);
            }
            catch { }
            try
            {
                var d = ProductDB.s_DefinitionsById;
                if (d != null)
                {
                    foreach (var p in AllProducts())
                        if (!d.ContainsKey(p._id)) d.Add(p._id, p);
                }
            }
            catch { }
        }

        // ProductsController kurulduktan sonra eklendiysek (yamalar kacirdiysa): eksikleri tamamla
        void EnsureController()
        {
            try
            {
                var pcx = _pcCache; var mp = pcx != null && !pcx.WasCollected ? pcx._brandToProductDefinitionIds : null;
                if (mp != null)
                    foreach (var lc in _lics.Where(x => x.Def != null && !mp.ContainsKey(x.Id)))
                    {
                        var ll = new Il2CppSystem.Collections.Generic.List<int>();
                        foreach (var f in lc.Figs.Where(x => x.On && x.FigProd != null)) { ll.Add(f.FigProductId); if (f.BoxProd != null) ll.Add(f.BoxProductId); }
                        mp.Add(lc.Id, ll);
                    }
            }
            catch { }
            if (_pcCache == null || _pcCache.WasCollected)
            {
                _pcCache = null;
                foreach (var o in UnityEngine.Object.FindObjectsByType(Il2CppType.Of<ProductsController>(), FindObjectsSortMode.None))
                { var c = o?.TryCast<ProductsController>(); if (c != null) { _pcCache = c; break; } }
            }
            var pc = _pcCache;
            if (pc == null || !pc._isInitialized) return;
            bool server = false;
            try { server = InstanceFinder.IsServerStarted; } catch { }
            if (!_diagDone)
            {
                _diagDone = true;
                try
                {
                    var cfg = _cfgCache;
                    bool inPickups = false;
                    var all = cfg?._pickupDefinitions;
                    if (all != null) for (int i = 0; i < all.Count; i++) { var d = all[i]; if (d != null && d._id == BrandId) { inPickups = true; break; } }
                    LoggerInstance.Msg($"Tani: sunucu={server}, _brands'ta={(pc._brands != null && pc._brands.ContainsKey(BrandId))}, lisans alindi={(pc._brands != null && pc._brands.ContainsKey(BrandId) && pc._brands[BrandId])}, " +
                        $"_defDict'te figur={(pc._defDict != null && pc._defDict.ContainsKey(FigProductId))}, config listesinde={ListHasId(cfg?._brandDefinitions, BrandId)}, PickupDefinitions'ta={inPickups}, " +
                        $"lisans->urun={(pc._brandToProductDefinitionIds != null && pc._brandToProductDefinitionIds.ContainsKey(BrandId))}");
                }
                catch (Exception e) { LoggerInstance.Warning("Tani: " + e.Message); }
            }
            var dd = pc._defDict;
            if (server && dd != null && AllProducts().Any(p => !dd.ContainsKey(p._id)))
            {
                LoggerInstance.Msg("Urunler kontrolcude yok, ekleniyor (AddProducts)...");
                try { pc.AddProducts(); pc.RebuildProductIndexDictionary(); } catch (Exception e) { LoggerInstance.Warning("AddProducts: " + e.Message); }
            }
            var br = pc._brands;
            if (server && br != null && (!br.ContainsKey(BrandId) || CustomBrands.Any(b => !br.ContainsKey(b._id))))
            {
                LoggerInstance.Msg("Lisans kontrolcude yok, ekleniyor (AddBrands)...");
                try { pc.AddBrands(); } catch (Exception e) { LoggerInstance.Warning("AddBrands: " + e.Message); }
            }
            if (server && dd != null) EnsureMarket(dd);
            var b2p = pc._brandToProductDefinitionIds;
            if (b2p != null && !b2p.ContainsKey(BrandId))
            {
                var l = new Il2CppSystem.Collections.Generic.List<int>();
                foreach (var f in BuiltFigs.Where(x => x.On && x.Lic == null)) { l.Add(f.FigProductId); if (f.BoxProd != null) l.Add(f.BoxProductId); }
                b2p.Add(BrandId, l);
                LoggerInstance.Msg("Lisans -> urun eslemesi eklendi.");
            }
        }

        // ------------------------------------------------------------------ fiyatsiz ilk yerlesim (1.1.2)
        // Figurler rafa ILK konuldugunda etiket bos ("-") olmali. Tetik: ProductPlace.AddItemServer (kayit yukleme ve kutu doldurma SetItemsServer kullanir, onlarda calismaz); urun kaydi (ProductInfo kimligi)
        // "tamam" degilse fiyat bir kez kaldirilir ve "tamam" sayilir. Oyuncu fiyat verirse (ChangeProductPriceLogic, recordAnalytics) "tamam".
        // Yedek: "tamam" olmayan figurlerde oyunun / baska modlarin koydugu fiyat yarim saniyede bir kaldirilir.
        // Liste: UserData\FigureShop\_unpriced_placed.txt (kayit dosyasina dokunulmaz).
        static HashSet<string> _doneInfos;
        static bool _allPriced, _unpricedDiag;
        static float _nextPriceCheck;
        static string DoneFile => Path.Combine(_dataDir, "_unpriced_placed.txt");
        static void LoadDone()
        {
            if (_doneInfos != null) return;
            _doneInfos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try { if (File.Exists(DoneFile)) foreach (var l in File.ReadAllLines(DoneFile)) if (l.Trim().Length > 0) _doneInfos.Add(l.Trim()); } catch { }
        }
        static void SaveDone() { try { File.WriteAllLines(DoneFile, _doneInfos); } catch { } }
        static bool IsAutoPrice(ProductInfo i) => Math.Abs(i.CurrentPrice - Mathf.Ceil(i.MarketPrice)) <= 1f || Math.Abs(i.CurrentPrice - i.MarketPrice) <= 1f || Math.Abs(i.CurrentPrice - i.BasePrice) <= 0.01f;
        static void ClearPrice(ProductInfo info, string why)
        {
            if (!info.IsSetupPrice) return;
            info.IsSetupPrice = false;
            try { var pc = _pcCache; if (pc != null && !pc.WasCollected) pc.OnChanged?.Invoke(info); } catch { }
            Log.Msg($"{why}: price {info.CurrentPrice} removed, shelf tag empty");
        }
        static void AddItemServerPostfix(ProductPlace __instance, Il2CppSystem.Guid id)
        {
            try
            {
                if (!_built || !_unpriced.Value) return;
                try { if (__instance != null && __instance._pickupProducts != null) return; } catch { }
                var pc = _pcCache; if (pc == null || pc.WasCollected) return;
                ProductInfo product = null; try { product = pc.GetProductInfo(id); } catch { }
                if (product == null) return;
                int defId = product.DefinitionId;
                if (!_ourProductIds.Contains(defId)) return;
                LoadDone();
                string g = product.ProductId.ToString();
                if (_doneInfos.Contains(g)) return;
                ClearPrice(product, "first shelf placement of " + (_figs.Any(x => x.FigProductId == defId) ? "figure" : "boxed figure"));
                _doneInfos.Add(g); SaveDone();
            }
            catch (Exception e) { Log.Warning("First placement: " + e.Message); }
        }
        static void PriceChangedPostfix(Il2CppSystem.Guid productId, bool recordAnalytics)
        {
            try { if (!recordAnalytics || !_built) return; LoadDone(); if (_doneInfos.Add(productId.ToString())) SaveDone(); } catch { }
        }
        static void WatchUnpriced()
        {
            if (!_built || _allPriced || !_unpriced.Value || Time.unscaledTime < _nextPriceCheck) return;
            _nextPriceCheck = Time.unscaledTime + 0.5f;
            var pc = _pcCache;
            if (pc == null || pc.WasCollected || !pc._isInitialized) return;
            bool server = false;
            try { server = InstanceFinder.IsServerStarted; } catch { }
            var dd = pc._defDict;
            if (!server || dd == null) return;
            LoadDone();
            bool pending = false, changed = false;
            foreach (var id in AllProducts().Select(p => p._id))
            {
                if (!dd.ContainsKey(id)) { pending = true; continue; }
                var info = dd[id]; if (info == null) { pending = true; continue; }
                if (!_unpricedDiag) Log.Msg($"Price info {id}: current {info.CurrentPrice}, market {info.MarketPrice}, set {info.IsSetupPrice}, count {info.AllCount}");
                string g = info.ProductId.ToString();
                if (_doneInfos.Contains(g)) continue;
                // zaten satilan ve oyuncunun fiyat verdigi figur (bu surumden once): dokunma
                if (info.AllCount > 0 && info.IsSetupPrice && !IsAutoPrice(info)) { _doneInfos.Add(g); changed = true; continue; }
                pending = true;
                if (info.IsSetupPrice) ClearPrice(info, "figure " + id + " (not placed yet)");
            }
            _unpricedDiag = true;
            // 1.3.2: tek gecis yeter (eski kayitlardaki oyuncu fiyatlarini isaretler). Eskiden bekleyen figur oldukca her 0.5 sn
            // sonsuza dek calisiyordu; rafa ilk konuldugunda fiyat zaten AddItemServerPostfix'te silinir.
            _allPriced = true;
            if (changed) SaveDone();
        }

        static float AutoBase(string key, float lo, float hi)
        {
            uint h = 2166136261; foreach (var c in ("market:" + key).ToLowerInvariant()) { h ^= c; h *= 16777619; }
            return (float)Math.Round(lo + (hi - lo) * ((h % 1000) / 999f), 2);
        }

        // kayitlardaki figurler kopyalanan figurun degeriyle kurulmus olabilir: bir kez kendi degerine cek (sonra gunluk oynama oyunun)
        static HashSet<int> _marketDone;
        static void EnsureMarket(Il2CppSystem.Collections.Generic.Dictionary<int, ProductInfo> dd)
        {
            string file = Path.Combine(_dataDir, "_market_done.txt");
            if (_marketDone == null)
            {
                _marketDone = new HashSet<int>();
                try { if (File.Exists(file)) foreach (var l in File.ReadAllLines(file)) if (int.TryParse(l.Trim(), out var v)) _marketDone.Add(v); } catch { }
            }
            bool changed = false;
            foreach (var (pid, def) in AllProducts().Select(p => (p._id, p)).ToList())
            {
                if (def == null || _marketDone.Contains(pid) || !dd.ContainsKey(pid)) continue;
                var info = dd[pid]; if (info == null) continue;
                float bp = def._basePrice, mp = bp;
                try { mp = def.OptPrice(bp); } catch { }
                if (Math.Abs(info.BasePrice - bp) > 0.01f || Math.Abs(info.MarketPrice - mp) > 0.01f)
                {
                    Log.Msg($"Product {pid}: market value {info.BasePrice:0.##}/{info.MarketPrice:0.##} -> {bp:0.##}/{mp:0.##} (own value)");
                    info.BasePrice = bp; info.MarketPrice = mp;
                    try { var pc = _pcCache; if (pc != null && !pc.WasCollected) pc.OnChanged?.Invoke(info); } catch { }
                }
                _marketDone.Add(pid); changed = true;
            }
            if (changed) { try { File.WriteAllLines(file, _marketDone.Select(x => x.ToString())); } catch { } }
        }

        static void EnsureNetworkPrefabs()
        {
            if (_netPrefabs.Count == 0) return;
            var nm = InstanceFinder.NetworkManager;
            if (nm == null || nm.Pointer == _registeredNm) return;
            var objs = nm.SpawnablePrefabs;
            if (objs == null) return;
            foreach (var no in _netPrefabs) objs.AddObject(no, true, true);
            _registeredNm = nm.Pointer;
            Log.Msg($"Ag prefab'lari kaydedildi ({_netPrefabs.Count}).");
        }

        static void EnsureLocalization(bool log)
        {
            AddTerm(BrandKey, _brandNameTr.Value, _brandName.Value, SafeKey(_srcBrandDef), log);
            foreach (var l in _lics.Where(x => x.Def != null)) AddTerm(l.Key, l.NameTr, l.Name, SafeKey(_srcBrandDef), log);
            foreach (var f in BuiltFigs)
            {
                AddTerm(f.FigKey, f.NameTr(), f.NameEn(), SafeKey(_srcFigPickup), log);
                if (f.BoxPickup != null) AddTerm(f.BoxKey, f.BoxNameTr(), f.BoxNameEn(), SafeKey(_srcBoxPickup), log);
            }
        }

        static void AddTerm(string key, string tr, string en, string refKey, bool log)
        {
            try
            {
                LanguageSourceData src = null;
                if (!string.IsNullOrEmpty(refKey)) LocalizationManager.GetTermData(refKey, out src);
                if (src == null) { var all = LocalizationManager.Sources; if (all != null && all.Count > 0) src = all[0]; }
                if (src == null) return;
                var term = src.GetTermData(key, false) ?? src.AddTerm(key);
                if (term == null) return;
                var langs = src.mLanguages;
                int n = langs != null ? langs.Count : 0;
                if (term.Languages == null || term.Languages.Length < n) term.Languages = new Il2CppStringArray(n);
                for (int i = 0; i < n; i++)
                {
                    string code = langs[i].Code ?? "";
                    string want = code.StartsWith("tr", StringComparison.OrdinalIgnoreCase) ? tr : en;
                    if (term.Languages[i] != want) term.Languages[i] = want;
                }
                var dict = src.mDictionary;
                if (dict != null && !dict.ContainsKey(key)) dict.Add(key, term);
                if (log) Log.Msg($"Ceviri eklendi: {key} ({n} dil)");
            }
            catch (Exception e) { if (log) Log.Warning("Ceviri eklenemedi: " + e.Message); }
        }

        // ------------------------------------------------------------------ helpers

        static bool IsOurId(int id) => id == BrandId || _ourIds.Contains(id) || _licIds.Contains(id);
        static IEnumerable<PickupDefinition> AllPickups()
        {
            if (_brand != null) yield return _brand;
            foreach (var b in CustomBrands) yield return b;
            foreach (var f in BuiltFigs) { if (f.FigPickup != null) yield return f.FigPickup; if (f.BoxPickup != null) yield return f.BoxPickup; }
        }
        static IEnumerable<ProductDefinition> AllProducts()
        {
            foreach (var f in BuiltFigs) { if (f.FigProd != null) yield return f.FigProd; if (f.BoxProd != null) yield return f.BoxProd; }
        }

        static EProductType? TypeOf(PickupDefinition p)
        {
            try { var pd = p.ProductDefinition; return pd != null ? pd._type : (EProductType?)null; } catch { return null; }
        }

        static List<PickupDefinition> ToList(Il2CppSystem.Collections.Generic.List<PickupDefinition> l)
        {
            var r = new List<PickupDefinition>();
            if (l == null) return r;
            for (int i = 0; i < l.Count; i++) if (l[i] != null) r.Add(l[i]);
            return r;
        }

        static bool ListHasId(Il2CppSystem.Collections.Generic.List<PickupDefinition> l, int id)
        {
            if (l == null) return false;
            for (int i = 0; i < l.Count; i++) { var e = l[i]; if (e != null && SafeId(e) == id) return true; }
            return false;
        }

        static int SafeId(DDef d) { try { return d != null ? d._id : 0; } catch { return 0; } }
        static string SafeKey(DDef d) { try { return d?._nameKey; } catch { return null; } }
        static string SafeName(PickupDefinition d) { try { return d.Name; } catch { try { return d.name; } catch { return "?"; } } }

        static IEnumerable<T> FindAll<T>() where T : Il2CppObjectBase
        {
            var arr = Resources.FindObjectsOfTypeAll(Il2CppType.Of<T>());
            foreach (var o in arr)
            {
                var t = o != null ? o.TryCast<T>() : null;
                if (t != null) yield return t;
            }
        }
    }
}
