#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class PlaytestFeedbackPlayVerification
{
    const string Pending = "1832.PlaytestFeedback.Play";
    static int previousFrame = -1;
    static double lastAdvance;
    static string Request => Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/PlaytestFeedbackPlay.request"));

    static PlaytestFeedbackPlayVerification()
    {
        EditorApplication.update += Poll;
    }

    static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;
        if (File.Exists(Request) && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            var output = File.ReadAllText(Request).Trim();
            File.Delete(Request);
            SessionState.SetString(Pending, output);
            SessionState.SetBool(Pending + ".active", true);
            previousFrame = -1;
            lastAdvance = EditorApplication.timeSinceStartup;
            EditorApplication.isPlaying = true;
            return;
        }

        if (!SessionState.GetBool(Pending + ".active", false) || !EditorApplication.isPlaying)
            return;
        EditorApplication.QueuePlayerLoopUpdate();
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        if (Time.frameCount != previousFrame)
        {
            previousFrame = Time.frameCount;
            lastAdvance = EditorApplication.timeSinceStartup;
        }

        if (EditorApplication.timeSinceStartup - lastAdvance > 1)
        {
            EditorApplication.isPaused = true;
            EditorApplication.Step();
        }

        string path = SessionState.GetString(Pending, "");
        if (string.IsNullOrEmpty(path))
            return;
        SessionState.EraseString(Pending);
        Application.runInBackground = true;
        var story = StoryDirector.Current;
        if (story == null)
            story = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Campaign/CampaignRoot")).GetComponent<StoryDirector>();
        story.gameObject.AddComponent<PlaytestFeedbackPlayProbe>().Begin(path);
    }

    public static void Finished()
    {
        SessionState.EraseBool(Pending + ".active");
        EditorApplication.isPlaying = false;
    }
}

public sealed partial class PlaytestFeedbackPlayProbe : MonoBehaviour
{
    [Serializable]
    sealed class Report
    {
        public bool success, framesAdvanced;
        public int checks, startFrame, endFrame;
        public string error;
        public string[] passed;
        public string coverage = "Actual editor Play verification using project components. See passed checks for scope; no full playthrough or hardware input injection. User scene not saved.";
    }

    readonly List<string> passed = new List<string>();
    Report report;
    string output;
    StoryDirector story;
    float listenerVolume;
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    CampaignCinematicView Film => story.GetComponent<CampaignCinematicView>();

    T Get<T>(object target, string field) => (T)target.GetType().GetField(field, Private).GetValue(target);
    void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    void Check(bool ok, string text)
    {
        if (!ok)
            throw new Exception(text);
        passed.Add(text);
    }

    public void Begin(string path)
    {
        int delimiter=path.IndexOf('|');
        string mode=delimiter>=0?path.Substring(0,delimiter):"all";
        output=delimiter>=0?path.Substring(delimiter+1):path;
        story = GetComponent<StoryDirector>();
        report = new Report
        {
            startFrame = Time.frameCount
        };
        listenerVolume = AudioListener.volume;
        AudioListener.volume = 0;
        IEnumerator test;
        switch(mode)
        {
            case "lane": test=LaneTest(); break;
            case "hope": test=HopeTest(); break;
            case "lyrics": test=LyricsTest(); break;
            case "openingwords": test=OpeningWordsTest(); break;
            case "presentationoptions": test=PresentationOptionsTest(); break;
            case "heroplacement": test=HeroPlacementTest(); break;
            case "marker": test=MarkerTest(); break;
            case "hit": test=HitStopTest(); break;
            case "chapter9finale": test=ChapterNineFinaleTest(); break;
            case "weaponjam": test=WeaponJamTest(); break;
            case "printui": test=PrintUiTest(); break;
            case "javertend": test=JavertEndTest(); break;
            case "countdown": test=ConstructionCountdownTest(); break;
            case "titlecalibration": test=TitleCalibrationTest(); break;
            case "nightmare": test=NightmareTest(); break;
            case "persistence": test=PersistenceTest(); break;
            default: test=Test(); break;
        }
        StartCoroutine(Guard(test));
    }

    IEnumerator Guard(IEnumerator test)
    {
        var stack = new Stack<IEnumerator>();
        stack.Push(test);
        while (stack.Count > 0)
        {
            object current = null;
            bool more = false;
            Exception error = null;
            try
            {
                var top = stack.Peek();
                more = top.MoveNext();
                if (more)
                    current = top.Current;
                else
                    stack.Pop();
            }
            catch (Exception e)
            {
                error = e;
            }

            if (error != null)
            {
                report.error = error.ToString();
                break;
            }

            if (!more)
                continue;
            if (current is IEnumerator child)
            {
                stack.Push(child);
                continue;
            }

            yield return current;
        }

        foreach (var it in stack)
            (it as IDisposable)?.Dispose();
        RestorePreferences();
        AudioListener.volume = listenerVolume;
        AudioListener.pause = false;
        Time.timeScale = 1;
        report.endFrame = Time.frameCount;
        report.framesAdvanced = report.endFrame > report.startFrame;
        report.success = string.IsNullOrEmpty(report.error);
        report.checks = passed.Count;
        report.passed = passed.ToArray();
        File.WriteAllText(Path.Combine(output, "试玩改进Play检查.json"), JsonUtility.ToJson(report, true));
        PlaytestFeedbackPlayVerification.Finished();
    }

    IEnumerator Until(Func<bool> ready, bool skipCards = false)
    {
        float deadline = Time.unscaledTime + 25f;
        for (int i = 0; i < 20000 && Time.unscaledTime < deadline; i++)
        {
            if (ready())
                yield break;
            if (skipCards)
                story.DebugSkip();
            yield return null;
        }

        throw new TimeoutException("Play check waiting for " + story.Phase);
    }

    IEnumerator Frames(int n)
    {
        for (int i = 0; i < n; i++)
            yield return null;
    }

    IEnumerator Progress(CampaignActor actor, float progress)
    {
        Set(actor, "elapsed", actor.duration * progress);
        yield return Frames(2);
    }

    BombHP Bomb(Vector3 position, int group)
    {
        var go = Instantiate(Resources.Load<GameObject>("arrow/Bomb"), position, Quaternion.identity);
        var hp = go.GetComponent<BombHP>();
        hp.Configure(group, position.y);
        return hp;
    }

    Color32[] CaptureHeart(int hp, string name)
    {
        GameManager.WallHP = hp;
        foreach (var h in UnityEngine.Object.FindObjectsByType<HeartFill>(FindObjectsSortMode.None))
            h.RefreshNow();
        var hud = UnityEngine.Object.FindFirstObjectByType<AmmoHudView>();
        foreach (var pulse in UnityEngine.Object.FindObjectsByType<Heartbeat>(FindObjectsSortMode.None))
        {
            pulse.enabled = false;
            pulse.transform.localScale = Vector3.one;
        }

        typeof(AmmoHudView).GetMethod("LateUpdate", Private).Invoke(hud, null);
        var canvas = Get<CampaignUI>(story, "ui").View.GetComponent<Canvas>();
        var mode = canvas.renderMode;
        var cam = canvas.worldCamera;
        float distance = canvas.planeDistance;
        var go = new GameObject("HeartRatioCapture");
        var camera = go.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.nearClipPlane = .1f;
        camera.farClipPlane = 100;
        var target = new RenderTexture(1280, 720, 24);
        target.Create();
        var oldActive = RenderTexture.active;
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(output, name + ".png"), image.EncodeToPNG());
            var corners = new Vector3[4];
            hud.heartPulse.GetWorldCorners(corners);
            var a = camera.WorldToScreenPoint(corners[0]);
            var b = camera.WorldToScreenPoint(corners[2]);
            int x = Mathf.Clamp(Mathf.FloorToInt(a.x), 0, 1279), y = Mathf.Clamp(Mathf.FloorToInt(a.y), 0, 719), w = Mathf.Min(1280 - x, Mathf.CeilToInt(b.x) - x), h = Mathf.Min(720 - y, Mathf.CeilToInt(b.y) - y);
            var pixels = image.GetPixels(x, y, w, h);
            return pixels.Select(c => (Color32)c).ToArray();
        }
        finally
        {
            canvas.renderMode = mode;
            canvas.worldCamera = cam;
            canvas.planeDistance = distance;
            RenderTexture.active = oldActive;
            target.Release();
            Destroy(target);
            Destroy(image);
            Destroy(go);
            Canvas.ForceUpdateCanvases();
        }
    }

    readonly Dictionary<string, int?> preferences = new Dictionary<string, int?>();
    float? masterPreference;
    readonly Dictionary<string,float?> presentationPreferences=new Dictionary<string,float?>();
    float originalLatency;
    void RememberPreferences()
    {
        originalLatency=RhythmManager.ExtraOutputDelayMs;
        foreach(var key in new[]{GamePresentationOptions.MusicKey,GamePresentationOptions.EffectsKey,GamePresentationOptions.HeartbeatKey,GamePresentationOptions.SubtitleKey,"Barricade.AudioExtraDelayMs.v3"})presentationPreferences[key]=PlayerPrefs.HasKey(key)?(float?)PlayerPrefs.GetFloat(key):null;
        masterPreference = PlayerPrefs.HasKey("Barricade.MasterVolume") ? (float? )PlayerPrefs.GetFloat("Barricade.MasterVolume") : null;
        var keys = Enum.GetValues(typeof(GameAction)).Cast<GameAction>().Select(a => "Barricade.Key." + a).Concat(new[] { "Barricade.Difficulty", "Barricade.CalibrationReady" });
        foreach (var key in keys)
            preferences[key] = PlayerPrefs.HasKey(key) ? (int? )PlayerPrefs.GetInt(key) : null;
    }

    void RestorePreferences()
    {
        GameSettingsView.Current?.Close();
        RhythmManager.AdjustOutputDelay(originalLatency-RhythmManager.ExtraOutputDelayMs);
        foreach(var item in presentationPreferences){if(item.Value.HasValue)PlayerPrefs.SetFloat(item.Key,item.Value.Value);else PlayerPrefs.DeleteKey(item.Key);}
        foreach (var item in preferences)
        {
            if (item.Value.HasValue)
                PlayerPrefs.SetInt(item.Key, item.Value.Value);
            else
                PlayerPrefs.DeleteKey(item.Key);
        }

        if (masterPreference.HasValue)
            PlayerPrefs.SetFloat("Barricade.MasterVolume", masterPreference.Value);
        else
            PlayerPrefs.DeleteKey("Barricade.MasterVolume");
        PlayerPrefs.Save();
    }

    void CaptureUi(int width, int height, string name)
    {
        var canvas = Get<CampaignUI>(story, "ui").View.GetComponent<Canvas>();
        var mode = canvas.renderMode;
        var oldCamera = canvas.worldCamera;
        float distance = canvas.planeDistance;
        var node = new GameObject("UiCapture");
        var cam = node.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.nearClipPlane = .1f;
        cam.farClipPlane = 100;
        var target = new RenderTexture(width, height, 24);
        target.Create();
        var active = RenderTexture.active;
        var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            cam.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases();
            cam.Render();
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            File.WriteAllBytes(Path.Combine(output, name + ".png"), texture.EncodeToPNG());
        }
        finally
        {
            canvas.renderMode = mode;
            canvas.worldCamera = oldCamera;
            canvas.planeDistance = distance;
            RenderTexture.active = active;
            target.Release();
            Destroy(target);
            Destroy(texture);
            Destroy(node);
            Canvas.ForceUpdateCanvases();
        }
    }

    void ClearThreats()
    {
        foreach (var hp in FindObjectsByType<EnemyHP>(FindObjectsSortMode.None))
            Destroy(hp.gameObject);
        foreach (var hp in FindObjectsByType<BombHP>(FindObjectsSortMode.None))
            Destroy(hp.gameObject);
        foreach (var hp in FindObjectsByType<CannonShell>(FindObjectsSortMode.None))
            Destroy(hp.gameObject);
        foreach (var hp in FindObjectsByType<Cannon>(FindObjectsSortMode.None))
            Destroy(hp.gameObject);
        foreach (var hp in FindObjectsByType<CampaignActor>(FindObjectsSortMode.None))
            Destroy(hp.gameObject);
        foreach (var hp in FindObjectsByType<ArrowDefinition>(FindObjectsSortMode.None))
            Destroy(hp.gameObject);
    }

    IEnumerator Test()
    {
        RememberPreferences();
        yield return Frames(4);
        DebugConsole.Current?.SetOpen(false);
        var settings = GameSettingsView.Current;
        Check(settings != null, "Settings overlay attaches to existing title menu");
        GameBindings.Reset();
        GameOptions.Difficulty = GameDifficulty.Normal;
        var menu = FindFirstObjectByType<CampaignMenuView>();
        Check(menu.titleMenu.transform.parent.Find("Settings") != null && menu.pausePage.transform.Find("Settings") != null, "Both title and pause have settings entry buttons");
        bool wasMenuActive = menu.titleMenu.activeSelf;
        menu.titleMenu.SetActive(false);
        Check(menu.titleMenu.transform.parent.Find("Settings").gameObject.activeInHierarchy, "Settings entry remains available on the first-launch title prompt");
        menu.titleMenu.SetActive(wasMenuActive);
        settings.Open();
        yield return Frames(2);
        Check(GameSettingsView.IsOpen && Time.timeScale == 0, "Settings freeze gameplay and open from title");
        Check(GameBindings.TrySet(GameAction.BuildA, KeyCode.J, out _), "Construction key can be rebound");
        Check(!GameBindings.TrySet(GameAction.BuildD, KeyCode.J, out _), "Conflicting construction key is rejected");
        Check(GameBindings.TrySet(GameAction.Fire, KeyCode.J, out _), "Same key may be reused in separate build/combat contexts");
        Check(!GameBindings.TrySet(GameAction.AimUp, KeyCode.J, out _), "Conflicting combat key is rejected");
        Check(!GameBindings.TrySet(GameAction.Fire, KeyCode.Escape, out _), "Pause key is reserved");
        Check(GameBindings.Key(GameAction.Fire) == KeyCode.J && GameBindings.CombatHint.Contains("J 开火"), "Binding persisted and live combat hint reflects the new key");
        var page = settings.transform.Find("SettingsPage");
        page.Find("ControlsPage/DifficultyNext").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        Check(GameOptions.Difficulty == GameDifficulty.Hard, "Difficulty arrow applies and saves immediately");
        Check(page.Find("ControlsPage/Resolution").GetComponent<UnityEngine.UI.Text>().text.Contains("×"), "Settings expose current resolution");
        Check(Get<Vector2Int[]>(settings, "resolutions").Contains(new Vector2Int(1280, 720)), "Window settings include standard 1280x720");
        Check(page.Find("ControlsPage/WindowMode").GetComponent<UnityEngine.UI.Button>() != null, "Settings expose window/fullscreen control");
        CaptureUi(1280, 720, "设置1280");
        CaptureUi(960, 540, "设置960");
        var allText = page.GetComponentsInChildren<UnityEngine.UI.Text>().Select(t => t.text).Aggregate("", (a, b) => a + b);
        var missing = allText.Where(c => c >= 0x4e00 && c <= 0x9fff && !GameFont.Silver.HasCharacter(c)).Distinct().ToArray();
        Check(missing.Length == 0, "Settings font contains every displayed Chinese character: " + new string (missing));
        settings.Close();
        yield return Frames(2);
        Check(!GameSettingsView.IsOpen && Time.timeScale == 1, "Closing title settings restores simulation without launching a campaign");
        GameBindings.Reset();
        // Run the actual existing calibration coroutine and inspect its real image, without keyboard injection.
        var ui = Get<CampaignUI>(story, "ui");
        Set(story, "phase", CampaignPhase.Calibration);
        var calibration = StartCoroutine(OpeningCalibration.Run(ui, story.transform));
        yield return Frames(2);
        var heart = ui.View.transform.Find("CalibrationHeart");
        Check(heart != null && heart.GetComponent<UnityEngine.UI.Image>().sprite == ParisUI.Style.heart, "Calibration shows the game's own heart sprite");
        float start = Time.unscaledTime;
        while (heart.localScale.x <= 1.01f && Time.unscaledTime - start < 2)
            yield return null;
        Check(heart.localScale.x > 1.01f, "Calibration heart pulses on the audio schedule");
        CaptureUi(1280, 720, "校音心脏");
        OpeningCalibration.DebugSkip = true;
        yield return Frames(3);
        Check(ui.View.transform.Find("CalibrationHeart") == null, "Calibration cleans up its heart and audio after exit");
        // Actual preparation-scene component and reward path.
        StartCoroutine(story.DebugChapter(3, true));
        yield return Until(() => story.Phase == CampaignPhase.Defense && WaveDirector.Current != null);
        StartCoroutine(story.DebugPhase("prep"));
        yield return Until(() => story.Phase == CampaignPhase.Preparation);
        yield return Frames(3);
        var tap = FindFirstObjectByType<Tap>();
        GameBindings.TrySet(GameAction.BuildA, KeyCode.J, out _);
        Set(story, "constructionCycle", story.Clock.Completed);
        Get<ConstructionJudgement>(story, "construction").Reset(0, -1, .14);
        story.ConstructionInput(tap);
        Check(tap.WordUI.text == "J", "Actual construction prompt shows rebound key instead of the original letter");
        GameBindings.Reset();
        GameManager.WallHP = 100;
        Get<ConstructionJudgement>(story, "construction").Reset(0, -1, .14);
        typeof(StoryDirector).GetMethod("ApplyConstructionResult", Private).Invoke(story, new object[] { tap, ConstructionResult.Ordinary });
        Check(GameManager.WallHP == 105, "Ordinary construction input really restores five health");
        Check(CampaignRevisionRules.ConstructionReward(false, true, 20, 5) == 10 && CampaignRevisionRules.ConstructionReward(true, true, 20, 5) == 20 && CampaignRevisionRules.ConstructionReward(true, false, 20, 5) == 5, "Perfect rewards stay unchanged and ordinary chord reward is five");
        // Actual defense-scene lane and difficulty behavior.
        StartCoroutine(story.DebugChapter(3, true));
        yield return Until(() => story.Phase == CampaignPhase.Defense && WaveDirector.Current != null);
        yield return Frames(3);
        CampaignDebug.Value.god = false;
        CampaignDebug.Value.freeze = true;
        var wave = WaveDirector.Current;
        foreach (GameDifficulty difficulty in Enum.GetValues(typeof(GameDifficulty)))
        {
            GameOptions.Difficulty = difficulty;
            Set(wave, "reducedIndex", 0);
            int kept = 0;
            for (int i = 0; i < 100; i++)
                kept+=(int)typeof(WaveDirector).GetMethod("SoldierCopies", Private).Invoke(wave, new object[] { "regular" });
            GameManager.WallHP = 600;
            GameManager.DamageWall(GameManager.Kind.Bomb);
            Check(GameManager.WallHP == 580, "Grenade damage remains twenty at " + difficulty);
            Check(kept == GameOptions.DensityPercent(3), "Actual wave filtering at " + difficulty + " keeps " + kept + " of 100 ordinary soldiers");
            Check(!(bool)typeof(WaveDirector).GetMethod("ReducedSoldier", Private).Invoke(wave, new object[] { "opening" }), "Authored opening formation is preserved at " + difficulty);
        }

        GameOptions.Difficulty = GameDifficulty.Easy;
        Check(GameOptions.DensityPercent(9) == 80, "Chapter nine keeps normal density on easy");
        GameOptions.Difficulty = GameDifficulty.Normal;
        var track = FindFirstObjectByType<CapsuleTrack>();
        track.DebugLane(1);
        yield return Frames(20);
        float oldY = track.transform.position.y;
        track.DebugLane(3);
        Check(Mathf.Approximately(track.transform.position.y, oldY) && CapsuleTrack.AttackIndex == 2 && CapsuleTrack.IsSwitching, "Lane logic changes immediately while player stays fixed and shooting is blocked");
        yield return Frames(2);
        Check(Mathf.Approximately(track.transform.position.y, oldY), "Player remains stationary during lane adjustment");
        yield return new WaitForSeconds(.25f);
        Check(Mathf.Approximately(track.transform.position.y, oldY) && !CapsuleTrack.IsSwitching, "Player stays fixed after adjustment and normal firing resumes");
        var session = CampaignSession.Current;
        session.Pause();
        Check(CampaignSession.Suspended && Time.timeScale == 0, "Existing pause freezes combat");
        settings.Open();
        yield return Frames(2);
        CaptureUi(1280, 720, "暂停中设置");
        settings.Close();
        Check(CampaignSession.Suspended && Time.timeScale == 0, "Closing settings from pause leaves the game paused");
        session.Resume();
        yield return Until(() => !CampaignSession.Suspended);
        yield return Frames(2);
        // Actual damage/display at multiple health levels.
        foreach (int damage in new[]
        {
            1,
            10,
            20,
            150,
            300,
            450,
            580
        }

        )
        {
            GameManager.WallHP = 600;
            GameManager.DamageWallAmount(damage);
            Check(GameManager.WallHP == 600 - damage, "Damage " + damage + " applies the correct wall health");
            CaptureHeart(GameManager.WallHP, "血量" + GameManager.WallHP);
            var hud = FindFirstObjectByType<AmmoHudView>();
            float expected = HeartHealthPresentation.Fill(hud.heartFill.sprite, (600 - damage) / 600f);
            Check(Mathf.Abs(hud.heartFill.fillAmount - expected) < .001f, "Visible fill matches health after " + damage + " damage");
            Check(hud.healthValue.text == (600 - damage) + " / 600", "Numerical health matches damage " + damage);
        }

        GameManager.WallHP = 600;
        GameManager.ResetBulletDamage();
        GameManager.DamageWall(GameManager.Kind.Bullet);
        Check(GameManager.WallHP == 600 && Mathf.Abs(GameManager.PendingBulletDamage - .5f) < .001f, "Half-point enemy bullet damage keeps fractional remainder");
        GameManager.DamageWall(GameManager.Kind.Bullet);
        Check(GameManager.WallHP == 599, "Two half-point enemy bullets apply one full health damage");
        Check(HeartHealthPresentation.Fill(ParisUI.Style.heart, 0) == 0 && HeartHealthPresentation.Fill(ParisUI.Style.heart, 1) == 1, "Empty and full heart endpoints are exact");
        // Stop new spawns at the duration limit, with real living enemies and incoming grenades kept active.
        ClearThreats();
        yield return Frames(2);
        GameManager.WallHP = 600;
        GameManager.ResetBulletDamage();
        wave.DebugSpawn(WaveEventType.Soldier, 2, 1, -1);
        yield return Frames(2);
        var survivor = FindFirstObjectByType<EnemyHP>();
        int originalHP = survivor.HP;
        int group = Spawn.NewGroupId();
        var bomb = Bomb(new Vector3(wave.WallX(2) - 4, wave.LaneY(2), 0), group);
        story.Clock.DebugComplete(wave.Script.defenseBeats - 4);
        wave.Tick();
        Check(!wave.FinaleStarted && survivor.HP == originalHP && bomb.IsAlive, "Last four authored beats no longer trigger automatic kill or freeze");
        story.Clock.DebugComplete(wave.Script.defenseBeats);
        wave.Tick();
        Check(wave.FinaleStarted && !wave.Finished && survivor.HP == originalHP && bomb.IsAlive, "End of authored duration keeps living enemies and grenades without ending chapter");
        CampaignDebug.Value.freeze = false;
        Check(!wave.Frozen, "Drain stage permits enemy movement and incoming impacts");
        var gun = FindFirstObjectByType<SBullet>();
        gun.DebugAmmo(gun.CampaignCapacity);
        yield return new WaitForSeconds(.2f);
        Check(gun.CanFire, "Player can still shoot during end-of-chapter cleanup");
        wave.RecordShellInterception();
        Check(wave.ShellInterceptions > 0, "Late interceptions still count during cleanup");
        var saved = story.CaptureSavedGame();
        Check(saved.wave.Any(f => f.name == "<FinaleStarted>k__BackingField" && f.value == "True"), "Save captures cleanup stage instead of discarding survivors");
        // Real damage remains possible in this extended stage.
        int before = GameManager.WallHP;
        bomb.Explode(true);
        Check(GameManager.WallHP == before - 20, "Grenade collision still damages the wall after duration limit");
        survivor.TakeDamage(10000);
        yield return Frames(2);
        Check(story.Phase == CampaignPhase.Defense || story.Phase == CampaignPhase.Transition || story.Phase == CampaignPhase.EndCard, "Combat can proceed into chapter completion only after threats resolve");
        Check(!wave.HasRemainingThreats(), "End stage reports no threats after the real survivor and bomb resolve");
        // Restore from the actual in-memory save. Never overwrite the user's on-disk save.
        StartCoroutine(story.RestoreSavedGame(saved));
        yield return Until(() => story.Phase == CampaignPhase.Defense && WaveDirector.Current != null && !story.DebugBusy);
        yield return Frames(2);
        wave = WaveDirector.Current;
        Check(wave.FinaleStarted && wave.HasRemainingThreats() && !wave.Finished, "Loading an extended-stage save restores remaining threats and keeps chapter open");
        Check(FindObjectsByType<EnemyHP>(FindObjectsSortMode.None).Any(h => h.HP > 0), "Living enemy survives save/load at chapter end");
        StartCoroutine(story.DebugChapter(9, true));
        yield return Until(() => story.Phase == CampaignPhase.Defense && WaveDirector.Current != null);
        yield return Frames(2);
        CampaignDebug.Value.freeze = true;
        ClearThreats();
        yield return Frames(2);
        wave = WaveDirector.Current;
        var artillery = Instantiate(Resources.Load<GameObject>("enemy/Cannon"), new Vector3(Camera.main.transform.position.x, wave.LaneY(2), 0), Quaternion.identity).GetComponent<Cannon>();
        yield return Frames(2);
        Set(wave, "cannon", artillery);
        story.Clock.DebugComplete(wave.Script.defenseBeats);
        wave.Tick();
        Check(artillery != null && artillery.IsRetreating && !wave.Finished, "Living cannon retreats at end and chapter waits for it to leave");
        var cp = artillery.transform.position;
        cp.x = Camera.main.transform.position.x - Camera.main.orthographicSize * Camera.main.aspect - 20;
        artillery.transform.position = cp;
        yield return Frames(3);
        Check(artillery == null, "Retreating cannon finishes its existing offscreen exit without being killed");
        report.coverage = "Actual title/pause uGUI and calibration coroutine; gameplay reward, wave density filter, lane movement, damage/heart rendering, end-stage shooting/impact/save-load through component APIs. No synthetic keyboard injection, no executable rebuild, no full campaign playthrough. User preferences restored and disk save not overwritten.";
    }
}
#endif




