// Graphics Plus - Anime Shop Simulator (by Fabianens)
// Oyun URP (Unity 2022.3, URP 14) kullaniyor; ENB/ReShade yerine oyunun kendi post-processing'i ile goruntuyu iyilestirir.
// - Kendi global Volume'umuz (yuksek oncelik): renk (pozlama, kontrast, doygunluk, sicaklik), bloom, vinyet, tonemapping.
//   Degerler oyunun kendi Volume degerlerinin USTUNE eklenir; 0 olan ayar oyunun degerine dokunmaz.
// - Ana kameranin kenar yumusatmasi (FXAA / SMAA / TAA + keskinlestirme).
// - SSAO (ortam golgesi): renderer'a bir kez eklenen URP ScreenSpaceAmbientOcclusion ozelligi, sonra sadece ac/kapa.
// Her sey sadece ayar degisince / sahne yuklenince uygulanir; her karede is yapilmaz (oyun CPU'ya bagli, takilma olmasin).
// Ayarlar: F10 (ModSettings) ya da UserData\MelonPreferences.cfg [GraphicsPlus].

using System;
using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Il2CppTMPro;
using Il2CppInterop.Runtime;

[assembly: MelonInfo(typeof(GraphicsPlus.Core), "Graphics Plus", "1.0.0", "Fabianens")]
[assembly: MelonGame("OneMoreTime", "Anime Shop Simulator")]

namespace GraphicsPlus
{
    public class Core : MelonMod
    {
        MelonPreferences_Category _cat;
        MelonPreferences_Entry<bool> _enabled, _color, _bloom, _vignette, _ssao, _messages;
        MelonPreferences_Entry<float> _exposure, _contrast, _saturation, _temperature, _bloomBoost, _vignetteAmt, _sharpen, _ssaoAmt;
        MelonPreferences_Entry<string> _preset, _tonemap, _aa;

        // ön ayarlar: exposure, contrast, saturation, temperature, bloom, vignette, tonemap, aa, sharpen, ssao(acik), ssao gucu
        sealed class Preset { public float Exp, Con, Sat, Tmp, Bloom, Vig, Sharp, Ao; public string Tone, AA; public bool Ssao; }
        static readonly Dictionary<string, Preset> Presets = new()
        {
            ["Game"]      = new Preset { Exp = 0,    Con = 0,  Sat = 0,  Tmp = 0,  Bloom = 0,    Vig = 0,    Tone = "Game",    AA = "Game", Sharp = 0.5f, Ssao = false, Ao = 0.8f },
            ["Natural"]   = new Preset { Exp = 0,    Con = 8,  Sat = 10, Tmp = 0,  Bloom = 0.15f, Vig = 0.12f, Tone = "Game",   AA = "SMAA", Sharp = 0.5f, Ssao = false, Ao = 0.8f },
            ["Vivid"]     = new Preset { Exp = 0.1f, Con = 15, Sat = 25, Tmp = 0,  Bloom = 0.3f, Vig = 0.15f, Tone = "Game",    AA = "SMAA", Sharp = 0.5f, Ssao = false, Ao = 0.8f },
            ["Cinematic"] = new Preset { Exp = 0.2f, Con = 12, Sat = 5,  Tmp = 8,  Bloom = 0.4f, Vig = 0.3f, Tone = "ACES",    AA = "SMAA", Sharp = 0.5f, Ssao = true,  Ao = 0.9f },
            ["Crisp"]     = new Preset { Exp = 0,    Con = 10, Sat = 8,  Tmp = 0,  Bloom = 0.1f, Vig = 0,    Tone = "Game",    AA = "TAA",  Sharp = 0.7f, Ssao = false, Ao = 0.8f },
        };
        const string PresetList = "Game|Natural|Vivid|Cinematic|Crisp|Custom";

        bool _dirty = true, _applyingPreset, _ppWarned;
        float _applyAt, _nextCamCheck;
        string _lastPreset;

        GameObject _volGo; Volume _vol; VolumeProfile _profile;
        ColorAdjustments _ca; WhiteBalance _wb; Bloom _bl; Vignette _vg; Tonemapping _tm;

        Camera _cam; UniversalAdditionalCameraData _camData;
        AntialiasingMode _origAA; AntialiasingQuality _origAAQ; float _origSharp; bool _aaSaved, _aaChanged;
        AntialiasingMode _wantAA; bool _wantAASet;

        ScreenSpaceAmbientOcclusion _ssaoFeat; bool _ssaoOurs, _ssaoOrigActive, _ssaoTried; float _ssaoOrigIntensity;

        public override void OnInitializeMelon()
        {
            _cat = MelonPreferences.CreateCategory("GraphicsPlus", "Graphics Plus");
            _enabled = _cat.CreateEntry("Enabled", true, "Apply the graphics settings below (off = the game's own look)");
            _preset = _cat.CreateEntry("Preset", "Natural", "Ready-made look. Moving any setting below switches this to Custom.");
            _cat.CreateEntry<string>("PresetOptions", PresetList, "Choices for Preset", null, true);
            _color = _cat.CreateEntry("ColorEnabled", true, "Color changes (exposure, contrast, saturation, temperature)");
            _exposure = _cat.CreateEntry("Exposure", 0f, "Brightness in EV, added to the game's (-1 to 1)");
            _contrast = _cat.CreateEntry("Contrast", 8f, "Contrast added to the game's (-30 to 50)");
            _saturation = _cat.CreateEntry("Saturation", 10f, "Color saturation added to the game's (-50 to 60)");
            _temperature = _cat.CreateEntry("Temperature", 0f, "Color temperature: minus = cooler / blue, plus = warmer / yellow (-40 to 40)");
            _bloom = _cat.CreateEntry("BloomEnabled", true, "Extra glow around bright lights");
            _bloomBoost = _cat.CreateEntry("BloomBoost", 0.15f, "Bloom intensity added to the game's (0 to 2)");
            _vignette = _cat.CreateEntry("VignetteEnabled", true, "Slightly darker screen edges");
            _vignetteAmt = _cat.CreateEntry("Vignette", 0.12f, "Vignette strength (0 to 0.6)");
            _tonemap = _cat.CreateEntry("Tonemapping", "Game", "Tone mapping: Game = unchanged, Neutral = soft, ACES = film look (stronger contrast)");
            _cat.CreateEntry<string>("TonemappingOptions", "Game|Neutral|ACES", "Choices for Tonemapping", null, true);
            _aa = _cat.CreateEntry("AntiAliasing", "SMAA", "Edge smoothing: Game = unchanged, FXAA = fast, SMAA = sharp, TAA = smoothest (needs MSAA off)");
            _cat.CreateEntry<string>("AntiAliasingOptions", "Game|Off|FXAA|SMAA|TAA", "Choices for AntiAliasing", null, true);
            _sharpen = _cat.CreateEntry("Sharpen", 0.5f, "Sharpening for TAA (0 to 1). Only works when AntiAliasing = TAA.");
            _ssao = _cat.CreateEntry("SSAOEnabled", false, "Ambient occlusion: soft shadows in corners and under objects. Costs some GPU.");
            _ssaoAmt = _cat.CreateEntry("SSAOIntensity", 0.8f, "Ambient occlusion strength (0 to 3)");
            _messages = _cat.CreateEntry("ShowMessages", true, "Short on-screen message when a preset is applied");

            _lastPreset = _preset.Value;
            foreach (var e in _cat.Entries)
            {
                var entry = e;
                entry.OnEntryValueChangedUntyped.Subscribe((a, b) => OnChanged(entry));
            }
            LoggerInstance.Msg("Graphics Plus ready. Preset: " + _preset.Value);
        }

        void OnChanged(MelonPreferences_Entry e)
        {
            MarkDirty(0.15f);
            if (_applyingPreset) return;
            if (e == _preset)
            {
                if (_preset.Value == _lastPreset) return;
                _lastPreset = _preset.Value;
                if (Presets.TryGetValue(_preset.Value, out var p)) ApplyPreset(p);
                Show(L("Ön ayar: ", "Preset: ") + PresetName(_preset.Value));
                return;
            }
            if (e == _enabled || e == _messages || e.Identifier.EndsWith("Options", StringComparison.Ordinal)) return;
            if (_preset.Value != "Custom") { _applyingPreset = true; _preset.Value = "Custom"; _lastPreset = "Custom"; _applyingPreset = false; }
        }

        void ApplyPreset(Preset p)
        {
            _applyingPreset = true;
            try
            {
                bool game = p.Con == 0 && p.Sat == 0 && p.Exp == 0 && p.Tmp == 0;
                _color.Value = !game; _exposure.Value = p.Exp; _contrast.Value = p.Con; _saturation.Value = p.Sat; _temperature.Value = p.Tmp;
                _bloom.Value = p.Bloom > 0; _bloomBoost.Value = p.Bloom;
                _vignette.Value = p.Vig > 0; _vignetteAmt.Value = p.Vig;
                _tonemap.Value = p.Tone; _aa.Value = p.AA; _sharpen.Value = p.Sharp;
                _ssao.Value = p.Ssao; _ssaoAmt.Value = p.Ao;
                MelonPreferences.Save();
            }
            finally { _applyingPreset = false; }
        }

        static string PresetName(string id) => id switch
        {
            "Game" => L("Oyunun kendisi", "Game default"),
            "Natural" => L("Doğal", "Natural"),
            "Vivid" => L("Canlı", "Vivid"),
            "Cinematic" => L("Sinematik", "Cinematic"),
            "Crisp" => L("Keskin", "Crisp"),
            _ => L("Özel", "Custom"),
        };

        void MarkDirty(float delay) { _dirty = true; _applyAt = Mathf.Max(_applyAt, Time.unscaledTime + delay); }

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            _cam = null; _camData = null; _aaSaved = false; _aaChanged = false;
            MarkDirty(1f);
        }

        public override void OnUpdate()
        {
            UpdateToasts();
            float now = Time.unscaledTime;
            if (now >= _nextCamCheck)
            {
                _nextCamCheck = now + 2f;
                CheckCamera();
            }
            if (_dirty && now >= _applyAt)
            {
                _dirty = false;
                try { Apply(); } catch (Exception ex) { LoggerInstance.Warning("Apply: " + ex.Message); }
            }
        }

        // ------------------------------------------------------------------ uygulama

        void Apply()
        {
            bool on = _enabled.Value;
            ApplyVolume(on);
            ApplyAA(on);
            ApplySsao(on);
        }

        // ---------- Volume ----------

        void EnsureVolume(int layer)
        {
            if (_volGo != null && _vol != null && _profile != null)
            {
                if (_volGo.layer != layer) { _vol.enabled = false; _volGo.layer = layer; _vol.enabled = true; }
                return;
            }
            _volGo = new GameObject("GraphicsPlus_Volume");
            UnityEngine.Object.DontDestroyOnLoad(_volGo);
            _volGo.hideFlags = HideFlags.DontSave;
            _volGo.layer = layer;
            _vol = _volGo.AddComponent<Volume>();
            _vol.isGlobal = true;
            _vol.priority = 1000f;
            _vol.weight = 1f;
            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _profile.name = "GraphicsPlus_Profile";
            _profile.hideFlags = HideFlags.DontSave;
            _vol.sharedProfile = _profile;
            _ca = _profile.Add(Il2CppType.Of<ColorAdjustments>(), false).Cast<ColorAdjustments>();
            _wb = _profile.Add(Il2CppType.Of<WhiteBalance>(), false).Cast<WhiteBalance>();
            _bl = _profile.Add(Il2CppType.Of<Bloom>(), false).Cast<Bloom>();
            _vg = _profile.Add(Il2CppType.Of<Vignette>(), false).Cast<Vignette>();
            _tm = _profile.Add(Il2CppType.Of<Tonemapping>(), false).Cast<Tonemapping>();
        }

        int VolumeLayer()
        {
            int mask = 1;
            try { if (_camData != null) mask = _camData.volumeLayerMask.value; } catch { }
            for (int i = 0; i < 32; i++) if ((mask & (1 << i)) != 0) return i;
            return 0;
        }

        // oyunun kendi global Volume'larindan (en yuksek oncelikli, override edilmis) bir parametrenin degeri
        T GameComp<T>() where T : VolumeComponent
        {
            T best = null; float bestPri = float.MinValue;
            foreach (var o in UnityEngine.Object.FindObjectsByType(Il2CppType.Of<Volume>(), FindObjectsSortMode.None))
            {
                var v = o.TryCast<Volume>();
                if (v == null || v == _vol || !v.enabled || !v.gameObject.activeInHierarchy || !v.isGlobal || v.weight <= 0f) continue;
                var prof = v.sharedProfile;
                if (prof == null || v.priority < bestPri) continue;
                foreach (var c in prof.components)
                {
                    var t = c != null ? c.TryCast<T>() : null;
                    if (t != null && t.active) { best = t; bestPri = v.priority; break; }
                }
            }
            return best;
        }

        static float Base(FloatParameter p, float def) => p != null && p.overrideState ? p.value : def;
        static float Base(ClampedFloatParameter p, float def) => p != null && p.overrideState ? p.value : def;
        static float Base(MinFloatParameter p, float def) => p != null && p.overrideState ? p.value : def;

        void ApplyVolume(bool on)
        {
            if (!on)
            {
                if (_vol != null) _vol.enabled = false;
                return;
            }
            EnsureVolume(VolumeLayer());
            _vol.enabled = true;

            var gca = GameComp<ColorAdjustments>();
            var gwb = GameComp<WhiteBalance>();
            var gbl = GameComp<Bloom>();
            var gvg = GameComp<Vignette>();

            bool col = _color.Value;
            float exp = col ? Mathf.Clamp(_exposure.Value, -2f, 2f) : 0f;
            float con = col ? Mathf.Clamp(_contrast.Value, -50f, 80f) : 0f;
            float sat = col ? Mathf.Clamp(_saturation.Value, -80f, 80f) : 0f;
            float tmp = col ? Mathf.Clamp(_temperature.Value, -60f, 60f) : 0f;
            Set(_ca.postExposure, exp, Base(gca?.postExposure, 0f) + exp);
            Set(_ca.contrast, con, Mathf.Clamp(Base(gca?.contrast, 0f) + con, -100f, 100f));
            Set(_ca.saturation, sat, Mathf.Clamp(Base(gca?.saturation, 0f) + sat, -100f, 100f));
            _ca.active = exp != 0 || con != 0 || sat != 0;
            Set(_wb.temperature, tmp, Mathf.Clamp(Base(gwb?.temperature, 0f) + tmp, -100f, 100f));
            _wb.active = tmp != 0;

            float bloom = _bloom.Value ? Mathf.Clamp(_bloomBoost.Value, 0f, 3f) : 0f;
            float gBloom = Base(gbl?.intensity, 0f);
            Set(_bl.intensity, bloom, gBloom + bloom);
            // oyunda bloom yoksa makul bir esik (sadece gercekten parlak yerler)
            bool noGameBloom = gbl == null || gBloom <= 0f;
            _bl.threshold.overrideState = bloom > 0 && noGameBloom; if (_bl.threshold.overrideState) _bl.threshold.value = 1.0f;
            _bl.scatter.overrideState = bloom > 0 && noGameBloom; if (_bl.scatter.overrideState) _bl.scatter.value = 0.65f;
            _bl.active = bloom > 0;

            float vig = _vignette.Value ? Mathf.Clamp(_vignetteAmt.Value, 0f, 1f) : 0f;
            Set(_vg.intensity, vig, Mathf.Clamp01(Base(gvg?.intensity, 0f) + vig));
            bool noGameVig = gvg == null || Base(gvg.intensity, 0f) <= 0f;
            _vg.smoothness.overrideState = vig > 0 && noGameVig; if (_vg.smoothness.overrideState) _vg.smoothness.value = 0.45f;
            _vg.active = vig > 0;

            string tone = _tonemap.Value ?? "Game";
            bool toneOn = tone == "Neutral" || tone == "ACES";
            _tm.mode.overrideState = toneOn;
            if (toneOn) _tm.mode.value = tone == "ACES" ? TonemappingMode.ACES : TonemappingMode.Neutral;
            _tm.active = toneOn;

            LoggerInstance.Msg($"Volume: exposure {exp:+0.00;-0.00;0}, contrast {con:+0;-0;0}, saturation {sat:+0;-0;0}, temperature {tmp:+0;-0;0}, bloom +{bloom:0.00}, vignette {vig:0.00}, tonemap {tone}"
                + $" (game: bloom {gBloom:0.00}, contrast {Base(gca?.contrast, 0f):0}, saturation {Base(gca?.saturation, 0f):0})");

            if (_camData != null)
            {
                bool pp = false; try { pp = _camData.renderPostProcessing; } catch { }
                if (!pp && !_ppWarned) { _ppWarned = true; Show(L("Kamerada efektler kapalı (oyun ayarı ya da FPS Doctor 'Efektleri kapat'). Renk/bloom görünmez.",
                                "Post-processing is off on the camera (game setting or FPS Doctor 'Turn effects off'). Color/bloom won't show."), true); }
            }
        }

        static void Set(VolumeParameter<float> p, float delta, float value)
        {
            p.overrideState = delta != 0f;
            if (p.overrideState) p.value = value;
        }

        // ---------- kamera / kenar yumusatma ----------

        void CheckCamera()
        {
            Camera cam = null;
            try { cam = Camera.main; } catch { }
            if (cam == null) return;
            if (cam != _cam)
            {
                if (_cam != null && _camData != null) RestoreAA();
                _cam = cam; _camData = null; _aaSaved = false; _aaChanged = false;
                try { _camData = cam.GetComponent<UniversalAdditionalCameraData>(); } catch { }
                MarkDirty(0.2f);
                return;
            }
            // oyun kendi ayar menusunden degistirdiyse tekrar uygula (sadece okuma, ucuz)
            if (_camData != null && _wantAASet)
            {
                try { if (_camData.antialiasing != _wantAA) { _aaSaved = false; MarkDirty(0.1f); } } catch { }
            }
        }

        static bool ParseAA(string s, out AntialiasingMode m)
        {
            switch (s)
            {
                case "Off": m = AntialiasingMode.None; return true;
                case "FXAA": m = AntialiasingMode.FastApproximateAntialiasing; return true;
                case "SMAA": m = AntialiasingMode.SubpixelMorphologicalAntiAliasing; return true;
                case "TAA": m = AntialiasingMode.TemporalAntiAliasing; return true;
                default: m = AntialiasingMode.None; return false;
            }
        }

        void ApplyAA(bool on)
        {
            if (_camData == null) return;
            if (!on || !ParseAA(_aa.Value, out var mode)) { RestoreAA(); _wantAASet = false; return; }
            if (!_aaSaved)
            {
                _origAA = _camData.antialiasing; _origAAQ = _camData.antialiasingQuality;
                try { _origSharp = _camData.m_TaaSettings.contrastAdaptiveSharpening; } catch { }
                _aaSaved = true;
            }
            _camData.antialiasing = mode;
            _camData.antialiasingQuality = AntialiasingQuality.High;
            _wantAA = mode; _wantAASet = true; _aaChanged = true;
            if (mode == AntialiasingMode.TemporalAntiAliasing)
            {
                try { var s = _camData.m_TaaSettings; s.contrastAdaptiveSharpening = Mathf.Clamp01(_sharpen.Value); _camData.m_TaaSettings = s; }
                catch (Exception ex) { LoggerInstance.Warning("TAA sharpen: " + ex.Message); }
                int msaa = 1;
                try { var urp = GraphicsSettings.currentRenderPipeline?.TryCast<UniversalRenderPipelineAsset>(); if (urp != null) msaa = urp.msaaSampleCount; } catch { }
                if (msaa > 1) Show(L("TAA için MSAA kapalı olmalı (FPS Doctor 'Kenar yumuşatmayı kapat' ya da oyunun ayarı). Şimdilik SMAA kullan.",
                                     "TAA needs MSAA off (FPS Doctor 'Turn MSAA off' or the game's setting). Use SMAA for now."), true);
            }
            LoggerInstance.Msg($"Anti-aliasing: {mode} (was {_origAA})");
        }

        void RestoreAA()
        {
            if (!_aaChanged || _camData == null) return;
            try
            {
                _camData.antialiasing = _origAA; _camData.antialiasingQuality = _origAAQ;
                var s = _camData.m_TaaSettings; s.contrastAdaptiveSharpening = _origSharp; _camData.m_TaaSettings = s;
            }
            catch { }
            _aaChanged = false;
        }

        // ---------- SSAO ----------

        void ApplySsao(bool on)
        {
            bool want = on && _ssao.Value;
            if (_ssaoFeat == null && !want) return;
            if (_ssaoFeat == null)
            {
                bool ok = false;
                try { ok = FindOrAddSsao(); } catch (Exception ex) { LoggerInstance.Warning("SSAO setup: " + ex.Message); }
                if (!ok) return;
            }
            try
            {
                var s = _ssaoFeat.m_Settings;
                if (want)
                {
                    s.Intensity = Mathf.Clamp(_ssaoAmt.Value, 0f, 4f);
                    _ssaoFeat.SetActive(true);
                }
                else
                {
                    if (!_ssaoOurs) s.Intensity = _ssaoOrigIntensity;
                    _ssaoFeat.SetActive(_ssaoOurs ? false : _ssaoOrigActive);
                }
                LoggerInstance.Msg($"SSAO: {(want ? "on " + s.Intensity.ToString("0.00") : "off")}{(_ssaoOurs ? "" : " (game's own feature)")}");
            }
            catch (Exception ex) { LoggerInstance.Warning("SSAO: " + ex.Message); }
        }

        bool FindOrAddSsao()
        {
            if (_ssaoTried) return false;
            _ssaoTried = true;
            var urp = GraphicsSettings.currentRenderPipeline?.TryCast<UniversalRenderPipelineAsset>();
            if (urp == null) { LoggerInstance.Warning("SSAO: URP asset not found"); return false; }
            var list = urp.m_RendererDataList;
            int idx = urp.m_DefaultRendererIndex;
            if (list == null || idx < 0 || idx >= list.Length || list[idx] == null) { LoggerInstance.Warning("SSAO: renderer data not found"); return false; }
            var rd = list[idx];
            foreach (var f in rd.rendererFeatures)
            {
                var ao = f != null ? f.TryCast<ScreenSpaceAmbientOcclusion>() : null;
                if (ao == null) continue;
                _ssaoFeat = ao; _ssaoOurs = false; _ssaoOrigActive = ao.isActive;
                try { _ssaoOrigIntensity = ao.m_Settings.Intensity; } catch { }
                LoggerInstance.Msg("SSAO: using the game's own feature (active: " + _ssaoOrigActive + ")");
                return true;
            }
            var shader = Shader.Find("Hidden/Universal Render Pipeline/ScreenSpaceAmbientOcclusion");
            if (shader == null) { LoggerInstance.Warning("SSAO: shader not in the game build"); Show(L("SSAO bu oyunda kullanılamıyor.", "SSAO is not available in this game."), true); return false; }
            var feat = ScriptableObject.CreateInstance(Il2CppType.Of<ScreenSpaceAmbientOcclusion>()).Cast<ScreenSpaceAmbientOcclusion>();
            feat.name = "GraphicsPlus_SSAO";
            feat.hideFlags = HideFlags.DontSave;
            feat.m_Shader = shader;
            var s = feat.m_Settings;
            // ucuz ayarlar: derinlikten normal, yarim cozunurluk, opak sonrasi (ek derinlik on-gecisi istemesin)
            s.AOMethod = ScreenSpaceAmbientOcclusionSettings.AOMethodOptions.InterleavedGradient;
            s.Source = ScreenSpaceAmbientOcclusionSettings.DepthSource.Depth;
            s.NormalSamples = ScreenSpaceAmbientOcclusionSettings.NormalQuality.Medium;
            s.Downsample = true;
            s.AfterOpaque = true;
            s.Samples = ScreenSpaceAmbientOcclusionSettings.AOSampleOption.Medium;
            s.BlurQuality = ScreenSpaceAmbientOcclusionSettings.BlurQualityOptions.Medium;
            s.Radius = 0.3f;
            s.Falloff = 60f;
            s.DirectLightingStrength = 0.25f;
            s.Intensity = Mathf.Clamp(_ssaoAmt.Value, 0f, 4f);
            feat.SetActive(false);
            rd.rendererFeatures.Add(feat);
            rd.SetDirty();
            _ssaoFeat = feat; _ssaoOurs = true;
            LoggerInstance.Msg("SSAO: feature added to renderer " + rd.name);
            return true;
        }

        // ------------------------------------------------------------------ dil + bildirim (UI_STANDARD.md)

        static bool TR { get { try { var c = Il2CppI2.Loc.LocalizationManager.CurrentLanguageCode; return c != null && c.StartsWith("tr", StringComparison.OrdinalIgnoreCase); } catch { return false; } } }
        static string L(string tr, string en) => TR ? tr : en;

        void Show(string m, bool warn = false)
        {
            LoggerInstance.Msg(m);
            if (!_messages.Value) return;
            try { Toast(m, warn); } catch (Exception e) { LoggerInstance.Warning("Toast: " + e.Message); }
        }

        const float ToastSeconds = 4.5f, ToastFade = 0.35f, ToastWidth = 460f, ToastHeight = 64f, ToastGap = 10f, ToastTop = 100f;
        static readonly Color CToast = new Color32(42, 26, 64, 242), CPink = new Color32(255, 115, 184, 255), CYellow = new Color32(255, 217, 102, 255);
        static readonly Color CText = new Color32(250, 244, 255, 255);

        sealed class ToastItem { public RectTransform Rect; public CanvasGroup Group; public float Born; }
        readonly List<ToastItem> _toasts = new();
        GameObject _toastRoot;
        TMP_FontAsset _font;

        void Toast(string msg, bool warn)
        {
            if (_toastRoot == null)
            {
                _toastRoot = new GameObject("GraphicsPlus_Toasts");
                UnityEngine.Object.DontDestroyOnLoad(_toastRoot);
                var canvas = _toastRoot.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 30002;
                var sc = _toastRoot.AddComponent<CanvasScaler>();
                sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.5f;
            }
            if (_font == null)
                foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<TMP_FontAsset>())) { var f = o.TryCast<TMP_FontAsset>(); if (f != null) { _font = f; break; } }

            while (_toasts.Count >= 4) { UnityEngine.Object.Destroy(_toasts[0].Rect.gameObject); _toasts.RemoveAt(0); }

            var box = new GameObject("Toast"); box.transform.SetParent(_toastRoot.transform, false);
            var br = box.AddComponent<RectTransform>();
            br.anchorMin = br.anchorMax = new Vector2(1f, 1f); br.pivot = new Vector2(1f, 1f);
            br.sizeDelta = new Vector2(ToastWidth, ToastHeight);
            var bg = box.AddComponent<Image>(); bg.color = CToast; bg.raycastTarget = false;
            var group = box.AddComponent<CanvasGroup>(); group.blocksRaycasts = false; group.interactable = false; group.alpha = 0f;

            var bar = new GameObject("Bar"); bar.transform.SetParent(box.transform, false);
            var barR = bar.AddComponent<RectTransform>(); barR.anchorMin = new Vector2(0, 0); barR.anchorMax = new Vector2(0, 1); barR.pivot = new Vector2(0, 0.5f); barR.sizeDelta = new Vector2(6, 0);
            var barI = bar.AddComponent<Image>(); barI.color = warn ? CYellow : CPink; barI.raycastTarget = false;

            var head = Text(box.transform, L("Grafik Plus", "Graphics Plus"), 15, warn ? CYellow : CPink, 8, 20);
            head.fontStyle = FontStyles.Bold;
            Text(box.transform, msg, 19, CText, 29, 28);

            _toasts.Add(new ToastItem { Rect = br, Group = group, Born = Time.unscaledTime });
            LayoutToasts();
        }

        TextMeshProUGUI Text(Transform parent, string s, float size, Color c, float top, float height)
        {
            var g = new GameObject("T"); g.transform.SetParent(parent, false);
            var r = g.AddComponent<RectTransform>();
            r.anchorMin = new Vector2(0, 1); r.anchorMax = new Vector2(1, 1); r.pivot = new Vector2(0, 1);
            r.offsetMin = new Vector2(20, -top - height); r.offsetMax = new Vector2(-14, -top);
            var t = g.AddComponent<TextMeshProUGUI>();
            if (_font != null) t.font = _font;
            t.text = s; t.fontSize = size; t.color = c; t.alignment = TextAlignmentOptions.MidlineLeft;
            t.enableWordWrapping = false; t.overflowMode = TextOverflowModes.Ellipsis; t.raycastTarget = false;
            return t;
        }

        void LayoutToasts()
        {
            for (int i = 0; i < _toasts.Count; i++)
            {
                int slot = _toasts.Count - 1 - i;
                _toasts[i].Rect.anchoredPosition = new Vector2(-24f, -(ToastTop + slot * (ToastHeight + ToastGap)));
            }
        }

        void UpdateToasts()
        {
            if (_toasts.Count == 0) return;
            float now = Time.unscaledTime;
            bool changed = false;
            for (int i = _toasts.Count - 1; i >= 0; i--)
            {
                var t = _toasts[i];
                if (t.Rect == null) { _toasts.RemoveAt(i); changed = true; continue; }
                float age = now - t.Born;
                if (age >= ToastSeconds) { UnityEngine.Object.Destroy(t.Rect.gameObject); _toasts.RemoveAt(i); changed = true; continue; }
                t.Group.alpha = Mathf.Min(1f, age / ToastFade, (ToastSeconds - age) / ToastFade);
            }
            if (changed) LayoutToasts();
        }
    }
}
