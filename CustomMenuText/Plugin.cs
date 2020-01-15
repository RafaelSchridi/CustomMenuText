using IPA;
using IPA.Config;
using IPA.Utilities;
using System.IO;
using UnityEngine.SceneManagement;
using IPALogger = IPA.Logging.Logger;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using BS_Utils.Utilities;
using Config = IPA.Config.Config;

namespace CustomMenuText
{
    public class Plugin : IBeatSaberPlugin
    {
        internal static Ref<PluginConfig> config;
        internal static IConfigProvider configProvider;

        private const string FILE_PATH = "/UserData/CustomMenuText.cfg";
        private const string FONT_PATH = "UserData/CustomMenuTextFont";

        private static GameObject textPrefab;
        private static readonly string[] DEFAULT_TEXT = { "BEAT", "SABER" };
        private static readonly Color defaultMainColor = new Color(0, 0.5019608f, 1);
        private static readonly Color defaultBottomColor = Color.red;

        // caches entries loaded from the file so we don't need to do IO every time the menu loads
        private static List<string[]> allEntries = null;

        // Store the text objects so when we leave the menu and come back, we aren't creating a bunch of them
        public static TextMeshPro mainText;
        public static TextMeshPro bottomText;

        public void Init(IPALogger logger, [Config.Prefer("json")] IConfigProvider cfgProvider)
        {
            Logger.log = logger;
            configProvider = cfgProvider;
            config = cfgProvider.MakeLink<PluginConfig>((p, v) =>
            {
                if (v.Value == null || v.Value.RegenerateConfig)
                {
                    p.Store(v.Value = new PluginConfig());
                }
                config = v;
            });

            BSEvents.OnLoad();
            BSEvents.menuSceneLoadedFresh += SetNewLogo;
        }
        public void OnActiveSceneChanged(Scene prevScene, Scene nextScene)
        {
            if (nextScene.name.Equals("MenuCore"))
            {
                SetNewLogo();
            }
        }

        private void SetNewLogo()
        {
            Scene scene = SceneManager.GetSceneByName("MenuCore");
            if (scene != null)
            {
                Logger.log.Debug("Setting logo for scene " + scene.name);
                if (allEntries == null)
                {
                    allEntries = readFromFile(FILE_PATH);
                }
                if (allEntries.Count == 0)
                {
                    Logger.log.Warn("[CustomMenuText] File found, but it contained no entries! Leaving original logo intact.");
                }
                else
                {
                    System.Random r = new System.Random();
                    int entryPicked = r.Next(allEntries.Count);
                    Logger.log.Debug("Entry picked: " + String.Join(" ", allEntries[entryPicked]) + " From " + allEntries.Count + " options");

                    setText(allEntries[entryPicked]);
                }
            }
        }

        public static List<string[]> readFromFile(string relPath)
        {
            List<string[]> entriesInFile = new List<string[]>();

            //// Look for the custom text file
            //string gameDirectory = Environment.CurrentDirectory;
            //gameDirectory = gameDirectory.Replace('\\', '/');
            //if (File.Exists(gameDirectory + relPath))
            //{
            //var linesInFile = File.ReadLines(gameDirectory + relPath);
            string customText = config.Value.CustomText;
            if (!string.IsNullOrEmpty(customText)) {
                IEnumerable<string> linesInFile =
                    config.Value.CustomText.Split(
                        new[] { "\r\n", "\r", "\n" },
                        StringSplitOptions.None);
                Logger.log.Debug(String.Join("\r\n", linesInFile));
                // Strip comments (all lines beginning with #)
                linesInFile = linesInFile.Where(s => s == "" || s[0] != '#');

                // Collect entries, splitting on empty lines
                List<string> currentEntry = new List<string>();
                foreach (string line in linesInFile)
                {
                    if (line == "")
                    {
                        entriesInFile.Add(currentEntry.ToArray());
                        currentEntry.Clear();
                    }
                    else
                    {
                        currentEntry.Add(line);
                    }
                }
                if (currentEntry.Count != 0)
                {
                    // in case the last entry doesn't end in a newline
                    entriesInFile.Add(currentEntry.ToArray());
                }
            }
            //}
            //else
            //{
                // No custom text file found!
                // Create the file and populate it with the default config
            //    try
            //    {
            //        using (FileStream fs = File.Create(gameDirectory + relPath))
            //        {
            //            Byte[] info = new UTF8Encoding(true).GetBytes(config.Value.CustomText
            //                // normalize newlines to CRLF
            //                .Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n"));
            //            fs.Write(info, 0, info.Length);
            //        }
            //    }
            //    catch (Exception ex)
            //    {
            //        Logger.log.Error("[CustomMenuText] No custom text file found, and an error was encountered trying to generate a default one!");
            //        Logger.log.Error("[CustomMenuText] Error:");
            //        Logger.log.Error(ex);
            //        Logger.log.Error("[CustomMenuText] To use this plugin, manually create the file " + relPath + " in your Beat Saber install directory.");
            //        return entriesInFile;
            //    }
            //    // File was successfully created; load from it with a recursive call.
            //    Logger.log.Debug("Created settings in  " + relPath);
            //    return readFromFile(relPath);
            //}
            //Logger.log.Debug("Loaded settings from " + relPath);

            return entriesInFile;
        }

        /// <summary>
        /// Replaces the logo in the main menu (which is an image and not text
        /// as of game version 0.12.0) with an editable TextMeshPro-based
        /// version. Performs only the necessary steps (if the logo has already
        /// been replaced, restores the text's position and color to default
        /// instead).
        /// Warning: Only call this function from the main menu scene!
        /// 
        /// Code generously donated by Kyle1413; edited some by Arti
        /// </summary>
        public static void replaceLogo()
        {
            // Since 0.13.0, we have to create our TextMeshPros differently! You can't change the font at runtime, so we load a prefab with the right font from an AssetBundle. This has the side effect of allowing for custom fonts, an oft-requested feature.
            if (textPrefab == null) textPrefab = loadTextPrefab(FONT_PATH);

            // Destroy Default Logo
            //GameObject defaultLogo = GameObject.Find("Logo")?.GetComponent<GameObject>();
            GameObject defaultLogo = GetAllGameObjectsInLoadedScenes().Where(obj => obj.name == "Logo").FirstOrDefault();
            if (defaultLogo != null) GameObject.Destroy(defaultLogo);

            // Logo Top Pos : 0.63, 21.61, 24.82
            // Logo Bottom Pos : 0, 17.38, 24.82
            if (mainText == null) mainText = GameObject.Find("CustomMenuTextTop")?.GetComponent<TextMeshPro>();
            if (mainText == null)
            {
                GameObject textObj = GameObject.Instantiate(textPrefab);
                textObj.name = "CustomMenuTextTop";
                textObj.SetActive(false);
                mainText = textObj.GetComponent<TextMeshPro>();
                mainText.alignment = TextAlignmentOptions.Center;
                mainText.fontSize = 12;
                mainText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 2f);
                mainText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 2f);
                mainText.richText = true;
                textObj.transform.localScale *= 3.7f;
                mainText.overflowMode = TextOverflowModes.Overflow;
                mainText.enableWordWrapping = false;
                textObj.SetActive(true);
            }
            mainText.rectTransform.position = new Vector3(0f, 21.61f, 24.82f);
            mainText.color = defaultMainColor;
            mainText.text = DEFAULT_TEXT[0];

            if (bottomText == null) bottomText = GameObject.Find("CustomMenuTextBottom")?.GetComponent<TextMeshPro>();
            if (bottomText == null)
            {
                GameObject textObj2 = GameObject.Instantiate(textPrefab);
                textObj2.name = "CustomMenuTextBottom";
                textObj2.SetActive(false);
                bottomText = textObj2.GetComponent<TextMeshPro>();
                bottomText.alignment = TextAlignmentOptions.Center;
                bottomText.fontSize = 12;
                bottomText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 2f);
                bottomText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 2f);
                bottomText.richText = true;
                textObj2.transform.localScale *= 3.7f;
                bottomText.overflowMode = TextOverflowModes.Overflow;
                bottomText.enableWordWrapping = false;
                textObj2.SetActive(true);
            }
            bottomText.rectTransform.position = new Vector3(0f, 17f, 24.82f);
            bottomText.color = defaultBottomColor;
            mainText.text = DEFAULT_TEXT[1];
        }

        private static List<GameObject> GetAllGameObjectsInLoadedScenes()
        {

            List<GameObject> gameObjects = new List<GameObject>();

            // Add root objects from all scenes.
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                gameObjects.AddRange(scene.GetRootGameObjects());
            }

            // All scenes are empty.
            if (gameObjects.Count == 0)
            {
                return gameObjects;
            }

            // Add all the children.
            int idx = 0;
            do
            {
                Transform goTransform = gameObjects[idx].transform;
                int childCount = goTransform.childCount;
                for (int i = 0; i < childCount; i++)
                {
                    gameObjects.Add(goTransform.GetChild(i).gameObject);
                }
                idx++;
            } while (idx < gameObjects.Count);

            return gameObjects;
        }

        public static GameObject loadTextPrefab(string path)
        {
            Logger.log.Debug("loadTextPrefab");
            var oldFonts = GameObject.FindObjectsOfType<TMP_FontAsset>();
            var glowFonts = oldFonts.Where(f => !f.name.Contains("No Glow"));
            var mat = glowFonts.FirstOrDefault()?.material;

            GameObject prefab;
            string fontPath = Path.Combine(Environment.CurrentDirectory, path);
            if (!File.Exists(fontPath))
            {
                File.WriteAllBytes(fontPath, Properties.Resources.Beon);
                Logger.log.Debug("No custom font found, writing default to " + fontPath);
            }
            AssetBundle fontBundle = AssetBundle.LoadFromFile(fontPath);
            prefab = fontBundle.LoadAsset<GameObject>("Text");
            if (prefab == null)
            {
                Logger.log.Debug("[CustomMenuText] No text prefab found in the provided AssetBundle! Using Beon.");
                AssetBundle beonBundle = AssetBundle.LoadFromMemory(Properties.Resources.Beon);
                prefab = beonBundle.LoadAsset<GameObject>("Text");
            }

            if (mat != null) prefab.GetComponent<TextMeshPro>().font.material = mat;

            return prefab;
        }

        /// <summary>
        /// Sets the text in the main menu (which normally reads BEAT SABER) to
        /// the text of your choice. TextMeshPro formatting can be used here.
        /// Additionally:
        /// - If the text is exactly 2 lines long, the first line will be
        ///   displayed in blue, and the second will be displayed in red.
        /// Warning: Only call this function from the main menu scene!
        /// </summary>
        /// <param name="lines">
        /// The text to display, separated by lines (from top to bottom).
        /// </param>
        public static void setText(string[] lines)
        {
            // Set up the replacement logo
            replaceLogo();

            if (lines.Length == 2)
            {
                mainText.text = lines[0];
                bottomText.text = lines[1];
            }
            else
            {
                // Hide the bottom line entirely; we're just going to use the main one
                bottomText.text = "";

                // Center the text vertically (halfway between the original positions)
                Vector3 newPos = mainText.transform.position;
                newPos.y = (newPos.y + bottomText.transform.position.y) / 2;
                mainText.transform.position = newPos;

                // Set text color to white by default (users can change it with formatting anyway)
                mainText.color = Color.white;

                // Set the text
                mainText.text = String.Join("\n", lines);
            }
        }

        // Unused overrides
        public void OnApplicationStart() { }
        public void OnApplicationQuit() { }
        public void OnFixedUpdate() { }
        public void OnUpdate() { }
        public void OnSceneLoaded(Scene scene, LoadSceneMode sceneMode) { }
        public void OnSceneUnloaded(Scene scene) { }
    }
}
