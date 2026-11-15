using System.IO;
using System.Linq;
using System.Reflection;
using AtomIO.Arena;
using AtomIO.Combat;
using AtomIO.Controls;
using AtomIO.Meta;
using AtomIO.Player;
using AtomIO.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AtomIO.EditorTools
{
    /// <summary>
    /// One-click setup of the playable Atom.IO prototype (Lobby + Arena scenes, prefabs,
    /// generated sprites, physics layers, build settings) from the crew's promoted scripts
    /// and tier files. Runs automatically the first time the project opens without
    /// Assets/AtomIO/Scenes/Arena.unity, and any time from the menu: Atom.IO > Build Game Scenes.
    /// Editor-only: never included in a build.
    /// </summary>
    [InitializeOnLoad]
    public static class AtomIOSceneBuilder
    {
        private const string Root = "Assets/AtomIO";
        private const string ArtDir = Root + "/Art";
        private const string PrefabDir = Root + "/Prefabs";
        private const string SceneDir = Root + "/Scenes";
        private const string SettingsDir = Root + "/Settings";
        private const string LobbyPath = SceneDir + "/Lobby.unity";
        private const string ArenaPath = SceneDir + "/Arena.unity";
        private const string AutoKey = "AtomIO.AutoBuildTried.v4";
        private static readonly string[] GameLayers = { "Player", "Enemy", "Electron", "Pickup", "Obstacle" };

        private static Sprite circle, ring, glow, square;
        private static Material lineMaterial;

        // Bump when the generated scenes/prefabs change, so an existing project offers a rebuild.
        // v2: electrons are pooled glowing yellow spheres (option A). v3: fix empty prefab slots. v4: energy orbs + bars are cyan-green.
        private const int BuildVersion = 4;
        private const string VersionPath = SettingsDir + "/builder_version.txt";

        static AtomIOSceneBuilder()
        {
            EditorApplication.delayCall += () =>
            {
                if (SessionState.GetBool(AutoKey, false) || EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    return;
                }

                SessionState.SetBool(AutoKey, true);
                if (!File.Exists(ArenaPath))
                {
                    Build();
                    return;
                }

                string built = File.Exists(VersionPath) ? File.ReadAllText(VersionPath).Trim() : "1";
                if (built != BuildVersion.ToString() && EditorUtility.DisplayDialog("Atom.IO",
                        "The Atom.IO scene builder was updated (yellow electrons, cyan-green energy orbs).\n\n" +
                        "Rebuild the Lobby and Arena scenes and prefabs now? Hand edits to those scenes will be replaced.",
                        "Rebuild", "Later"))
                {
                    Build();
                }
            };
        }

        [MenuItem("Atom.IO/Build Game Scenes")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Atom.IO] Stop play mode before building the scenes.");
                return;
            }

            if (Resources.Load<TextAsset>("AtomIO/Tiers/tier_001") == null)
            {
                EditorUtility.DisplayDialog("Atom.IO", "No tier files found in Assets/Resources/AtomIO/Tiers.\n\n" +
                    "Promote the crew output first:\npython main.py promote --unity-project <this project>", "OK");
                return;
            }

            if (!EnsureTextMeshPro())
            {
                return; // the TMP importer calls Build again when it finishes
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            foreach (var dir in new[] { Root, ArtDir, PrefabDir, SceneDir, SettingsDir })
            {
                Directory.CreateDirectory(dir);
            }

            EnsureLayers();
            SetupPhysics();
            MakeArt();

            var orb = MakeOrbPrefab();
            MakeElectronPrefab();
            MakeEnemyPrefab(orb);
            MakeObstaclePrefab();
            MakeBloomProfile();
            AssetDatabase.SaveAssets();

            BuildArenaScene();
            BuildLobbyScene();

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(LobbyPath, true),
                new EditorBuildSettingsScene(ArenaPath, true),
            };
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            File.WriteAllText(VersionPath, BuildVersion.ToString());
            AssetDatabase.ImportAsset(VersionPath);
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(LobbyPath);
            Debug.Log("[Atom.IO] Lobby and Arena scenes built. Press Play in the Lobby scene.");
            EditorUtility.DisplayDialog("Atom.IO", "Lobby and Arena scenes are ready.\n\nOpen Assets/AtomIO/Scenes/Lobby and press Play.\n" +
                "In the editor: WASD moves, drag the right stick with the mouse to aim and release to throw.", "Let's go");
        }

        // ------------------------------------------------------------------ TextMeshPro
        private static bool EnsureTextMeshPro()
        {
            if (Resources.Load<TMP_Settings>("TMP Settings") != null)
            {
                return true;
            }

            var importer = System.AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("TMPro.TMP_PackageResourceImporter", false))
                .FirstOrDefault(t => t != null);
            var method = importer?.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "ImportResources" && m.GetParameters().Length == 3);
            if (method != null)
            {
                Debug.Log("[Atom.IO] Importing TextMeshPro Essential Resources, then building the scenes...");
                AssetDatabase.importPackageCompleted += OnTmpImported;
                method.Invoke(null, new object[] { true, false, false });
                return false;
            }

            EditorUtility.DisplayDialog("Atom.IO", "Please import the TextMeshPro essentials first:\n" +
                "Window > TextMeshPro > Import TMP Essential Resources.\n\nThen run Atom.IO > Build Game Scenes.", "OK");
            return false;
        }

        private static void OnTmpImported(string packageName)
        {
            AssetDatabase.importPackageCompleted -= OnTmpImported;
            EditorApplication.delayCall += Build;
        }

        // ------------------------------------------------------------------ project settings
        private static void EnsureLayers()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            foreach (var name in GameLayers)
            {
                if (LayerMask.NameToLayer(name) >= 0)
                {
                    continue;
                }

                for (int i = 8; i < layers.arraySize; i++)
                {
                    var slot = layers.GetArrayElementAtIndex(i);
                    if (string.IsNullOrEmpty(slot.stringValue))
                    {
                        slot.stringValue = name;
                        break;
                    }
                }
            }

            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }

        private static int Layer(string name) => Mathf.Max(0, LayerMask.NameToLayer(name));

        private static void SetupPhysics()
        {
            Physics2D.gravity = Vector2.zero;
            Ignore("Electron", "Electron");
            Ignore("Electron", "Pickup");
            Ignore("Electron", "Obstacle");
            Ignore("Pickup", "Pickup");
            Ignore("Pickup", "Enemy");
            Ignore("Pickup", "Obstacle");
        }

        private static void Ignore(string a, string b)
        {
            int la = LayerMask.NameToLayer(a), lb = LayerMask.NameToLayer(b);
            if (la < 0 || lb < 0)
            {
                Debug.LogWarning($"[Atom.IO] Layer '{(la < 0 ? a : b)}' is missing; add it in Tags and Layers, then rebuild.");
                return;
            }

            Physics2D.IgnoreLayerCollision(la, lb, true);
        }

        // ------------------------------------------------------------------ art
        private static void MakeArt()
        {
            circle = MakeSprite("Circle", 256, (u, v, r) => Mathf.Clamp01((1f - r) * 128f));
            ring = MakeSprite("Ring", 256, (u, v, r) => Mathf.Clamp01(1f - Mathf.Abs(r - 0.88f) / 0.08f));
            glow = MakeSprite("Glow", 256, (u, v, r) => Mathf.Pow(Mathf.Clamp01(1f - r), 1.6f));
            square = MakeSprite("Square", 32, (u, v, r) => 1f);

            string matPath = ArtDir + "/Line.mat";
            lineMaterial = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (lineMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
                lineMaterial = new Material(shader);
                AssetDatabase.CreateAsset(lineMaterial, matPath);
            }
        }

        private static Sprite MakeSprite(string name, int size, System.Func<float, float, float, float> alpha)
        {
            string path = $"{ArtDir}/{name}.png";
            if (!File.Exists(path))
            {
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float u = (x + 0.5f) / size * 2f - 1f;
                        float v = (y + 0.5f) / size * 2f - 1f;
                        float r = Mathf.Sqrt(u * u + v * v);
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha(u, v, r)));
                    }
                }

                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
            }

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = size; // every sprite is 1 world unit across
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static GameObject SpriteObject(string name, Sprite sprite, Color color, float scale, int order, string layer)
        {
            var go = new GameObject(name);
            go.layer = Layer(layer);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            go.transform.localScale = new Vector3(scale, scale, 1f);
            return go;
        }

        private static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.white;

        // ------------------------------------------------------------------ prefabs
        private static T SavePrefab<T>(GameObject go, string name) where T : Component
        {
            string path = $"{PrefabDir}/{name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab.GetComponent<T>();
        }

        private static EnergyOrb MakeOrbPrefab()
        {
            var go = SpriteObject("EnergyOrb", glow, Hex("#3DFFB4"), 0.6f, 0, "Pickup"); // cyan-green: never confused with yellow electrons
            go.AddComponent<CircleCollider2D>().isTrigger = true;
            go.AddComponent<EnergyOrb>();
            return SavePrefab<EnergyOrb>(go, "EnergyOrb");
        }

        private static Electron MakeElectronPrefab()
        {
            // Option A: glowing yellow sphere = yellow disc + white-hot centre + soft yellow halo (bloom does the rest).
            var yellow = Hex("#FFD84D");
            var go = SpriteObject("Electron", circle, yellow, 0.35f, 3, "Electron");
            var body = go.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            go.AddComponent<CircleCollider2D>().isTrigger = true;
            var core = SpriteObject("Core", glow, new Color(1f, 1f, 0.9f, 0.9f), 0.55f, 4, "Electron");
            core.transform.SetParent(go.transform, false);
            var halo = SpriteObject("Halo", glow, new Color(yellow.r, yellow.g, yellow.b, 0.55f), 2.2f, 2, "Electron");
            halo.transform.SetParent(go.transform, false);
            var trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.25f;
            trail.widthMultiplier = 0.25f;
            trail.material = lineMaterial;
            trail.startColor = yellow;
            trail.endColor = new Color(yellow.r, yellow.g, yellow.b, 0f);
            trail.sortingOrder = 2;
            var electron = go.AddComponent<Electron>();
            Set(electron, "trail", trail);
            Set(electron, "body", go.GetComponent<SpriteRenderer>());
            Set(electron, "halo", halo.GetComponent<SpriteRenderer>());
            return SavePrefab<Electron>(go, "Electron");
        }

        private static Enemy MakeEnemyPrefab(EnergyOrb orb)
        {
            var go = SpriteObject("Enemy", circle, Hex("#FF3B5C"), 0.9f, 1, "Enemy");
            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            go.AddComponent<CircleCollider2D>();
            var enemy = go.AddComponent<Enemy>();
            Set(enemy, "orbPrefab", orb);
            return SavePrefab<Enemy>(go, "Enemy");
        }

        private static GameObject MakeObstaclePrefab()
        {
            var go = SpriteObject("Obstacle", circle, Color.gray, 1f, 0, "Obstacle");
            go.AddComponent<CircleCollider2D>();
            string path = $"{PrefabDir}/Obstacle.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static VolumeProfile MakeBloomProfile()
        {
            string path = SettingsDir + "/AtomIOBloom.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile != null)
            {
                return profile;
            }

            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.9f);
            bloom.threshold.Override(0.8f);
            bloom.scatter.Override(0.6f);
            AssetDatabase.AddObjectToAsset(bloom, profile);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        // ------------------------------------------------------------------ scenes
        private static Camera MakeCamera(Color background)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 9f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = background;
            go.transform.position = new Vector3(0f, 0f, -10f);
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            return cam;
        }

        private static T LoadPrefab<T>(string name) where T : Component
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/{name}.prefab");
            var component = go != null ? go.GetComponent<T>() : null;
            if (component == null)
            {
                Debug.LogError($"[Atom.IO] Could not load {typeof(T).Name} from {PrefabDir}/{name}.prefab.");
            }

            return component;
        }

        private static void BuildArenaScene()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Re-load the prefabs from disk AFTER opening the new scene: references kept from before
            // NewScene can come back as destroyed objects and would be saved as empty slots.
            var electron = LoadPrefab<Electron>("Electron");
            var enemy = LoadPrefab<Enemy>("Enemy");
            var orb = LoadPrefab<EnergyOrb>("EnergyOrb");
            var obstacle = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/Obstacle.prefab");
            var bloom = AssetDatabase.LoadAssetAtPath<VolumeProfile>(SettingsDir + "/AtomIOBloom.asset");

            var cam = MakeCamera(Hex("#0B1622"));
            var light = new GameObject("Global Light 2D").AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;
            var volume = new GameObject("Global Volume (Bloom)").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = bloom;

            // Player atom: nucleus + rim + aim line, Bohr electrons are spawned by ElectronOrbit.
            var player = new GameObject("Player");
            player.layer = Layer("Player");
            var body = player.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            player.AddComponent<CircleCollider2D>().radius = 0.5f;
            var core = SpriteObject("Core", circle, Color.white, 1f, 2, "Player");
            core.transform.SetParent(player.transform, false);
            var rim = SpriteObject("Rim", ring, Color.cyan, 1.5f, 1, "Player");
            rim.transform.SetParent(player.transform, false);
            var aimGo = new GameObject("AimLine");
            aimGo.transform.SetParent(player.transform, false);
            var aim = aimGo.AddComponent<LineRenderer>();
            aim.material = lineMaterial;
            aim.widthMultiplier = 0.08f;
            aim.positionCount = 2;
            aim.useWorldSpace = true;
            aim.startColor = Color.white;
            aim.endColor = new Color(1f, 1f, 1f, 0.2f);
            aim.sortingOrder = 4;
            aim.enabled = false;
            var atom = player.AddComponent<PlayerAtom>();
            Set(atom, "coreRenderer", core.GetComponent<SpriteRenderer>());
            Set(atom, "rimRenderer", rim.GetComponent<SpriteRenderer>());
            var orbit = player.AddComponent<ElectronOrbit>();
            Set(orbit, "electronPrefab", electron);
            Set(orbit, "aimLine", aim);

            var follow = cam.gameObject.AddComponent<ArenaCamera>();
            Set(follow, "target", player.transform);

            // Arena systems: session, spawner, per-level layout.
            var arena = new GameObject("Arena");
            var boundaryGo = new GameObject("Boundary");
            boundaryGo.transform.SetParent(arena.transform, false);
            var boundary = boundaryGo.AddComponent<LineRenderer>();
            boundary.material = lineMaterial;
            boundary.widthMultiplier = 0.3f;
            boundary.sortingOrder = -1;
            var layout = arena.AddComponent<ArenaLayoutBuilder>();
            Set(layout, "obstaclePrefab", obstacle);
            Set(layout, "boundary", boundary);
            var pool = arena.AddComponent<ElectronPool>();
            Set(pool, "electronPrefab", electron);
            var spawner = arena.AddComponent<ArenaSpawner>();
            Set(spawner, "enemyPrefab", enemy);
            Set(spawner, "orbPrefab", orb);
            Set(spawner, "electronPool", pool);
            Set(spawner, "electronPrefab", electron);
            var session = arena.AddComponent<ArenaSession>();
            Set(session, "player", atom);
            Set(session, "orbit", orbit);
            Set(session, "spawner", spawner);
            Set(session, "layout", layout);
            Set(session, "arenaCamera", cam);

            // HUD + touch controls.
            var canvas = MakeCanvas();
            var t = canvas.transform;
            var element = Label(t, "Element", "H  Hydrogen", 44, new Vector2(0, 1), new Vector2(40, -30), new Vector2(800, 60), TextAlignmentOptions.TopLeft);
            var level = Label(t, "Level", "Level 1/10", 28, new Vector2(0, 1), new Vector2(40, -90), new Vector2(800, 40), TextAlignmentOptions.TopLeft);
            var hp = Bar(t, "HealthBar", Hex("#FF4D6D"), new Vector2(40, -140));
            var energy = Bar(t, "EnergyBar", Hex("#3DFFB4"), new Vector2(40, -186));
            var time = Label(t, "Time", "00:00", 30, new Vector2(0, 1), new Vector2(40, -232), new Vector2(400, 40), TextAlignmentOptions.TopLeft);
            var electrons = Label(t, "Electrons", "e- 1/1", 30, new Vector2(0, 1), new Vector2(40, -272), new Vector2(400, 40), TextAlignmentOptions.TopLeft);
            var banner = Label(t, "Banner", "", 46, new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(1300, 150), TextAlignmentOptions.Center);
            var tutorial = Label(t, "Tutorial", "", 34, new Vector2(0.5f, 0), new Vector2(0, 90), new Vector2(1000, 100), TextAlignmentOptions.Center);
            var exit = MakeButton(t, "Exit", "EXIT", new Vector2(1, 1), new Vector2(-40, -30), new Vector2(200, 80));
            UnityEventTools.AddPersistentListener(exit.onClick, session.ExitArena);

            var move = Stick(t, "MoveStick", new Vector2(0, 0), new Vector2(280, 280));
            var aimStick = Stick(t, "AimStick", new Vector2(1, 0), new Vector2(-280, 280));
            var bridge = canvas.AddComponent<PlayerInputBridge>();
            Set(bridge, "moveStick", move);
            Set(bridge, "aimStick", aimStick);
            Set(bridge, "player", atom);
            Set(bridge, "orbit", orbit);

            var hud = canvas.AddComponent<ArenaHud>();
            Set(hud, "session", session);
            Set(hud, "elementLabel", element);
            Set(hud, "levelLabel", level);
            Set(hud, "healthFill", hp.fill);
            Set(hud, "healthLabel", hp.label);
            Set(hud, "energyFill", energy.fill);
            Set(hud, "energyLabel", energy.label);
            Set(hud, "timeLabel", time);
            Set(hud, "electronLabel", electrons);
            Set(hud, "tutorialLabel", tutorial);
            Set(hud, "bannerLabel", banner);

            MakeEventSystem();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ArenaPath);
        }

        private static void BuildLobbyScene()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            MakeCamera(Hex("#0B1622"));
            var canvas = MakeCanvas();
            var t = canvas.transform;
            var center = new Vector2(0.5f, 0.5f);

            Label(t, "Title", "ATOM.IO", 110, new Vector2(0.5f, 1), new Vector2(0, -60), new Vector2(1200, 140), TextAlignmentOptions.Center);
            var preview = Img(t, "CorePreview", circle, Color.white, center, new Vector2(0, 150), new Vector2(200, 200));
            var element = Label(t, "Element", "H  Hydrogen", 54, center, new Vector2(0, 0), new Vector2(1200, 70), TextAlignmentOptions.Center);
            var level = Label(t, "Level", "Level 1/10", 32, center, new Vector2(0, -60), new Vector2(1200, 50), TextAlignmentOptions.Center);
            var bar = Bar(t, "EnergyBar", Hex("#3DFFB4"), new Vector2(0, -120), center, new Vector2(640, 44));
            var info = Label(t, "Info", "", 30, center, new Vector2(0, -190), new Vector2(1400, 60), TextAlignmentOptions.Center);
            var play = MakeButton(t, "Play", "PLAY", center, new Vector2(0, -300), new Vector2(380, 110));
            var reset = MakeButton(t, "Reset", "Reset progress", new Vector2(1, 0), new Vector2(-40, 40), new Vector2(300, 70));

            var lobby = canvas.AddComponent<LobbyController>();
            Set(lobby, "elementLabel", element);
            Set(lobby, "levelLabel", level);
            Set(lobby, "energyFill", bar.fill);
            Set(lobby, "energyLabel", bar.label);
            Set(lobby, "infoLabel", info);
            Set(lobby, "startButton", play);
            Set(lobby, "resetButton", reset);
            Set(lobby, "corePreview", preview);

            MakeEventSystem();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), LobbyPath);
        }

        // ------------------------------------------------------------------ UI helpers
        private static GameObject MakeCanvas()
        {
            var go = new GameObject("Canvas");
            go.layer = Layer("UI");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return go;
        }

        private static void MakeEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            var inputSystemModule = System.AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule", false))
                .FirstOrDefault(x => x != null);
            if (inputSystemModule != null)
            {
                go.AddComponent(inputSystemModule);
            }
            else
            {
                go.AddComponent<StandaloneInputModule>();
            }
        }

        private static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = Layer("UI");
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        private static TMP_Text Label(Transform parent, string name, string text, float size, Vector2 anchor, Vector2 pos,
            Vector2 box, TextAlignmentOptions align)
        {
            var rt = Rect(parent, name, anchor, pos, box);
            var label = rt.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.alignment = align;
            label.color = Color.white;
            label.raycastTarget = false;
            return label;
        }

        private static Image Img(Transform parent, string name, Sprite sprite, Color color, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var rt = Rect(parent, name, anchor, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private struct BarParts
        {
            public Image fill;
            public TMP_Text label;
        }

        private static BarParts Bar(Transform parent, string name, Color color, Vector2 pos)
        {
            return Bar(parent, name, color, pos, new Vector2(0, 1), new Vector2(440, 36));
        }

        private static BarParts Bar(Transform parent, string name, Color color, Vector2 pos, Vector2 anchor, Vector2 size)
        {
            var back = Img(parent, name, square, new Color(1f, 1f, 1f, 0.15f), anchor, pos, size);
            var fill = Img(back.transform, "Fill", square, color, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0.5f;
            var label = Label(back.transform, "Label", "", 24, new Vector2(0.5f, 0.5f), Vector2.zero, size, TextAlignmentOptions.Center);
            label.color = new Color(0.05f, 0.05f, 0.1f);
            return new BarParts { fill = fill, label = label };
        }

        private static Button MakeButton(Transform parent, string name, string text, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var img = Img(parent, name, square, new Color(1f, 1f, 1f, 0.18f), anchor, pos, size);
            img.raycastTarget = true;
            var button = img.gameObject.AddComponent<Button>();
            Label(img.transform, "Text", text, size.y * 0.42f, new Vector2(0.5f, 0.5f), Vector2.zero, size, TextAlignmentOptions.Center);
            return button;
        }

        private static TouchStick Stick(Transform parent, string name, Vector2 anchor, Vector2 pos)
        {
            var baseImg = Img(parent, name, circle, new Color(1f, 1f, 1f, 0.18f), anchor, pos, new Vector2(320, 320));
            baseImg.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            baseImg.raycastTarget = true;
            var knob = Img(baseImg.transform, "Knob", circle, new Color(1f, 1f, 1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(140, 140));
            var stick = baseImg.gameObject.AddComponent<TouchStick>();
            Set(stick, "knob", knob.rectTransform);
            return stick;
        }

        // ------------------------------------------------------------------ wiring
        private static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogWarning($"[Atom.IO] {target.GetType().Name} has no serialized field '{field}'.");
                return;
            }

            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
