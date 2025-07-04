using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Yans.BuildExcluder
{
    [Serializable]
    public class BuildExcludeConfigWrapper : ScriptableObject
    {
        public BuildExcludeConfig config;
    }

    public class BuildExcluderWindow : EditorWindow
    {
        private BuildExcludeConfig config;
        private SerializedObject serializedConfig;

        [MenuItem("Window/Build Excluder")]
        public static void ShowWindow()
        {
            var window = GetWindow<BuildExcluderWindow>("Build Excluder");
            window.minSize = new Vector2(400, 300);
        }

        private void OnEnable()
        {
            LoadConfig();
            Selection.selectionChanged += OnSelectionChanged;
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
        }

        private void OnSelectionChanged()
        {
            Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginVertical();

            DrawHeader();
            DrawSelectedAssetInfo();
            DrawConfigEntries();
            DrawButtons();

            EditorGUILayout.EndVertical();
        }

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("Build Excluder", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            if (config == null)
            {
                EditorGUILayout.HelpBox("No configuration found. Click 'Create Config' to get started.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField($"Entries: {config.entries.Count}", EditorStyles.miniLabel);
            EditorGUILayout.Space();
        }

        private void DrawSelectedAssetInfo()
        {
            if (Selection.activeObject == null)
                return;

            var assetPath = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith("Assets/"))
                return;

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Selected Asset", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Path:", assetPath, EditorStyles.wordWrappedLabel);

            if (config != null)
            {
                // Find existing entry or create temporary one for display
                var entry = config.entries.FirstOrDefault(e => e.assetPath == assetPath);
                bool isTemporary = false;
                
                if (entry == null)
                {
                    // Create temporary entry for UI display only
                    entry = new BuildExcludeEntry(assetPath, new List<string>());
                    isTemporary = true;
                }

                // Always show defines list using Unity's built-in serialization
                if (serializedConfig != null)
                {
                    // If it's a temporary entry, add it to config temporarily
                    if (isTemporary)
                    {
                        config.entries.Add(entry);
                        UpdateSerializedConfig();
                    }
                    
                    serializedConfig.Update();
                    
                    var entriesProperty = serializedConfig.FindProperty("config.entries");
                    if (entriesProperty != null)
                    {
                        // Find the entry that matches our asset path
                        for (int i = 0; i < entriesProperty.arraySize; i++)
                        {
                            var entryProperty = entriesProperty.GetArrayElementAtIndex(i);
                            var assetPathProperty = entryProperty.FindPropertyRelative("assetPath");
                            
                            if (assetPathProperty.stringValue == assetPath)
                            {
                                var definesProperty = entryProperty.FindPropertyRelative("defines");
                                EditorGUILayout.PropertyField(definesProperty, new GUIContent("Define Constraints"), true);
                                
                                // Apply changes and save only if modified
                                if (serializedConfig.ApplyModifiedProperties())
                                {
                                    // If the defines list is empty and this was temporary, remove it
                                    if (isTemporary && entry.defines.Count == 0)
                                    {
                                        config.entries.Remove(entry);
                                    }
                                    SaveConfig();
                                }
                                break;
                            }
                        }
                    }
                    
                    // If it was temporary and still empty, remove it without saving
                    if (isTemporary && entry.defines.Count == 0)
                    {
                        config.entries.Remove(entry);
                        UpdateSerializedConfig();
                    }
                }

                // Check current conditions and show final status
                var currentDefines = GetCurrentDefines();
                if (entry != null && entry.defines.Count > 0)
                {
                    var wouldExclude = ShouldExclude(entry, currentDefines);
                    var statusColor = wouldExclude ? Color.red : Color.green;
                    var statusText = wouldExclude ? "Would be EXCLUDED" : "Would be INCLUDED";
                    
                    var oldColor = GUI.color;
                    GUI.color = statusColor;
                    EditorGUILayout.LabelField("Status:", statusText, EditorStyles.boldLabel);
                    GUI.color = oldColor;
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        private void DrawConfigEntries()
        {
            if (config == null || config.entries.Count == 0)
                return;

            EditorGUILayout.LabelField("Configured Assets", EditorStyles.boldLabel);

            if (serializedConfig != null)
            {
                serializedConfig.Update();
                
                var entriesProperty = serializedConfig.FindProperty("config.entries");
                if (entriesProperty != null)
                {
                    EditorGUILayout.PropertyField(entriesProperty, true);
                    
                    if (serializedConfig.ApplyModifiedProperties())
                    {
                        SaveConfig();
                    }
                }
            }
        }

        private void DrawButtons()
        {
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Create/Reload Config"))
            {
                CreateDefaultConfig();
                LoadConfig();
            }

            if (GUILayout.Button("Save Config"))
            {
                SaveConfig();
            }

            if (GUILayout.Button("Restore All Assets"))
            {
                BuildExcluderProcessor.RestoreExcludedAssets();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Current Defines:", EditorStyles.boldLabel);
            var defines = GetCurrentDefines();
            if (defines.Length > 0)
            {
                EditorGUILayout.LabelField(string.Join(", ", defines), EditorStyles.wordWrappedLabel);
            }
            else
            {
                EditorGUILayout.LabelField("No defines set", EditorStyles.miniLabel);
            }
        }

        private void LoadConfig()
        {
            try
            {
                if (File.Exists(BuildExcludeConfig.ConfigPath))
                {
                    var json = File.ReadAllText(BuildExcludeConfig.ConfigPath);
                    config = JsonUtility.FromJson<BuildExcludeConfig>(json);
                }
                else
                {
                    config = null;
                }
                
                UpdateSerializedConfig();
            }
            catch (Exception e)
            {
                Debug.LogError($"[BuildExcluder] Failed to load config: {e.Message}");
                config = null;
                serializedConfig = null;
            }
        }
        
        private void UpdateSerializedConfig()
        {
            if (config != null)
            {
                serializedConfig = new SerializedObject(ScriptableObject.CreateInstance<BuildExcludeConfigWrapper>());
                var wrapper = serializedConfig.targetObject as BuildExcludeConfigWrapper;
                wrapper.config = config;
                serializedConfig.Update();
            }
            else
            {
                serializedConfig = null;
            }
        }

        private void SaveConfig()
        {
            try
            {
                if (config == null)
                    config = new BuildExcludeConfig();

                // Clean up entries with empty defines lists
                config.entries.RemoveAll(entry => entry.defines == null || entry.defines.Count == 0);

                var directory = Path.GetDirectoryName(BuildExcludeConfig.ConfigPath);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                var json = JsonUtility.ToJson(config, true);
                File.WriteAllText(BuildExcludeConfig.ConfigPath, json);
                AssetDatabase.Refresh();
                
                UpdateSerializedConfig();
            }
            catch (Exception e)
            {
                Debug.LogError($"[BuildExcluder] Failed to save config: {e.Message}");
            }
        }

        private void CreateDefaultConfig()
        {
            config = new BuildExcludeConfig();
            config.entries.Add(new BuildExcludeEntry(
                "Assets/StoreSpecific/GooglePlay",
                new List<string> { "STORE_GOOGLEPLAY" }
            ));
            config.entries.Add(new BuildExcludeEntry(
                "Assets/StoreSpecific/AppGallery", 
                new List<string> { "STORE_APPGALLERY" }
            ));
            config.entries.Add(new BuildExcludeEntry(
                "Assets/DebugTools",
                new List<string> { "!DEBUG_BUILD" }
            ));
            config.entries.Add(new BuildExcludeEntry(
                "Assets/DeveloperAssets",
                new List<string> { "!DEVELOPMENT_BUILD" }
            ));
            
            SaveConfig();
        }

        private string[] GetCurrentDefines()
        {
            var targetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup);
            return defines.Split(';', StringSplitOptions.RemoveEmptyEntries);
        }

        private bool ShouldExclude(BuildExcludeEntry entry, string[] currentDefines)
        {
            if (entry.defines == null || entry.defines.Count == 0)
                return false;

            // New logic: defines specify when assets should be INCLUDED
            // If ANY define condition is met, the asset should be INCLUDED (not excluded)
            // If NO define conditions are met, the asset should be EXCLUDED

            foreach (var define in entry.defines)
            {
                if (string.IsNullOrEmpty(define))
                    continue;

                if (define.StartsWith("!"))
                {
                    // !DEFINE means "include when DEFINE is NOT active" (exclude when DEFINE is active)
                    var defineToCheck = define.Substring(1);
                    if (!Array.Exists(currentDefines, d => d.Equals(defineToCheck, StringComparison.OrdinalIgnoreCase)))
                        return false; // Include this asset (don't exclude)
                }
                else
                {
                    // DEFINE means "include when DEFINE is active" (exclude when DEFINE is NOT active)
                    if (Array.Exists(currentDefines, d => d.Equals(define, StringComparison.OrdinalIgnoreCase)))
                        return false; // Include this asset (don't exclude)
                }
            }

            // No conditions were met, so exclude the asset
            return true;
        }
    }
}
