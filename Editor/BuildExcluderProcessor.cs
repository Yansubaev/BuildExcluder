using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Yans.BuildExcluder
{
    public class BuildExcluderProcessor : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        private const string SESSION_STATE_KEY = "BuildExcluder_ExcludedPaths";
        private const string EXCLUDED_ASSETS_FOLDER = "ExcludedAssets";
        
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            Debug.Log("[BuildExcluder] Starting pre-build exclusion process...");
            
            var config = LoadConfig();
            if (config == null)
            {
                Debug.LogWarning("[BuildExcluder] No config found, skipping exclusion.");
                return;
            }

            var excludedPaths = new List<string>();
            var currentDefines = GetCurrentDefines(report.summary.platformGroup);

            foreach (var entry in config.entries)
            {
                if (ShouldExclude(entry, currentDefines))
                {
                    if (ExcludeAsset(entry.assetPath))
                    {
                        excludedPaths.Add(entry.assetPath);
                        Debug.Log($"[BuildExcluder] Excluded: {entry.assetPath}");
                    }
                }
            }

            // Save the list of excluded paths in SessionState
            SessionState.SetString(SESSION_STATE_KEY, string.Join(";", excludedPaths));
            
            // Refresh the asset database
            AssetDatabase.Refresh();
            
            Debug.Log($"[BuildExcluder] Pre-build complete. Excluded {excludedPaths.Count} assets.");
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            Debug.Log("[BuildExcluder] Starting post-build restoration process...");
            
            RestoreExcludedAssets();
            
            Debug.Log("[BuildExcluder] Post-build restoration complete.");
        }

        public static void RestoreExcludedAssets()
        {
            var excludedPathsString = SessionState.GetString(SESSION_STATE_KEY, "");
            if (string.IsNullOrEmpty(excludedPathsString))
            {
                // Check if there is anything in the ExcludedAssets folder
                CheckAndRestoreOrphanedAssets();
                return;
            }

            var excludedPaths = excludedPathsString.Split(';');
            var restoredCount = 0;

            foreach (var assetPath in excludedPaths)
            {
                if (!string.IsNullOrEmpty(assetPath) && RestoreAsset(assetPath))
                {
                    restoredCount++;
                    Debug.Log($"[BuildExcluder] Restored: {assetPath}");
                }
            }

            // Clear SessionState
            SessionState.EraseString(SESSION_STATE_KEY);
            
            // Check for remaining files
            CheckAndRestoreOrphanedAssets();
            
            // Refresh the asset database
            AssetDatabase.Refresh();
            
            Debug.Log($"[BuildExcluder] Restored {restoredCount} assets.");
        }

        private static void CheckAndRestoreOrphanedAssets()
        {
            var excludedAssetsPath = Path.Combine(Application.dataPath, "..", EXCLUDED_ASSETS_FOLDER);
            if (!Directory.Exists(excludedAssetsPath))
                return;

            var directories = Directory.GetDirectories(excludedAssetsPath);
            var files = Directory.GetFiles(excludedAssetsPath);

            if (directories.Length > 0 || files.Length > 0)
            {
                Debug.LogWarning($"[BuildExcluder] Found orphaned assets in {EXCLUDED_ASSETS_FOLDER}, attempting to restore...");
                
                foreach (var dir in directories)
                {
                    var dirName = Path.GetFileName(dir);
                    var originalPath = Path.Combine(Application.dataPath, dirName);
                    
                    if (!Directory.Exists(originalPath))
                    {
                        try
                        {
                            Directory.Move(dir, originalPath);
                            Debug.Log($"[BuildExcluder] Restored orphaned directory: Assets/{dirName}");
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"[BuildExcluder] Failed to restore orphaned directory {dirName}: {e.Message}");
                        }
                    }
                }

                foreach (var file in files)
                {
                    var fileName = Path.GetFileName(file);
                    var originalPath = Path.Combine(Application.dataPath, fileName);
                    
                    if (!File.Exists(originalPath))
                    {
                        try
                        {
                            File.Move(file, originalPath);
                            Debug.Log($"[BuildExcluder] Restored orphaned file: Assets/{fileName}");
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"[BuildExcluder] Failed to restore orphaned file {fileName}: {e.Message}");
                        }
                    }
                }
            }
        }

        private static BuildExcludeConfig LoadConfig()
        {
            try
            {
                if (!File.Exists(BuildExcludeConfig.ConfigPath))
                    return null;

                var json = File.ReadAllText(BuildExcludeConfig.ConfigPath);
                return JsonUtility.FromJson<BuildExcludeConfig>(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[BuildExcluder] Failed to load config: {e.Message}");
                return null;
            }
        }

        private static string[] GetCurrentDefines(BuildTargetGroup targetGroup)
        {
            var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup);
            return defines.Split(';', StringSplitOptions.RemoveEmptyEntries);
        }

        private static bool ShouldExclude(BuildExcludeEntry entry, string[] currentDefines)
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

        private static bool ExcludeAsset(string assetPath)
        {
            var fullAssetPath = Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));
            var excludedAssetsPath = Path.Combine(Application.dataPath, "..", EXCLUDED_ASSETS_FOLDER);
            
            if (!Directory.Exists(excludedAssetsPath))
                Directory.CreateDirectory(excludedAssetsPath);

            var assetName = Path.GetFileName(fullAssetPath);
            var destinationPath = Path.Combine(excludedAssetsPath, assetName);

            try
            {
                if (Directory.Exists(fullAssetPath))
                {
                    // Move the folder
                    if (Directory.Exists(destinationPath))
                    {
                        Debug.LogWarning($"[BuildExcluder] Destination already exists: {destinationPath}");
                        return false;
                    }
                    
                    Directory.Move(fullAssetPath, destinationPath);
                    
                    // Move the .meta file
                    var metaPath = fullAssetPath + ".meta";
                    var destinationMetaPath = destinationPath + ".meta";
                    if (File.Exists(metaPath))
                    {
                        File.Move(metaPath, destinationMetaPath);
                    }
                    
                    return true;
                }
                else if (File.Exists(fullAssetPath))
                {
                    // Move the file
                    if (File.Exists(destinationPath))
                    {
                        Debug.LogWarning($"[BuildExcluder] Destination already exists: {destinationPath}");
                        return false;
                    }
                    
                    File.Move(fullAssetPath, destinationPath);
                    
                    // Move the .meta file
                    var metaPath = fullAssetPath + ".meta";
                    var destinationMetaPath = destinationPath + ".meta";
                    if (File.Exists(metaPath))
                    {
                        File.Move(metaPath, destinationMetaPath);
                    }
                    
                    return true;
                }
                else
                {
                    Debug.LogWarning($"[BuildExcluder] Asset not found: {assetPath}");
                    return false;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[BuildExcluder] Failed to exclude asset {assetPath}: {e.Message}");
                return false;
            }
        }

        private static bool RestoreAsset(string assetPath)
        {
            var excludedAssetsPath = Path.Combine(Application.dataPath, "..", EXCLUDED_ASSETS_FOLDER);
            var assetName = Path.GetFileName(assetPath.Substring("Assets/".Length));
            var excludedPath = Path.Combine(excludedAssetsPath, assetName);
            var originalPath = Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));

            try
            {
                if (Directory.Exists(excludedPath))
                {
                    // Restore the folder
                    if (Directory.Exists(originalPath))
                    {
                        Debug.LogWarning($"[BuildExcluder] Original path already exists: {originalPath}");
                        return false;
                    }
                    
                    Directory.Move(excludedPath, originalPath);
                    
                    // Restore the .meta file
                    var metaPath = excludedPath + ".meta";
                    var originalMetaPath = originalPath + ".meta";
                    if (File.Exists(metaPath))
                    {
                        File.Move(metaPath, originalMetaPath);
                    }
                    
                    return true;
                }
                else if (File.Exists(excludedPath))
                {
                    // Restore the file
                    if (File.Exists(originalPath))
                    {
                        Debug.LogWarning($"[BuildExcluder] Original path already exists: {originalPath}");
                        return false;
                    }
                    
                    File.Move(excludedPath, originalPath);
                    
                    // Restore the .meta file
                    var metaPath = excludedPath + ".meta";
                    var originalMetaPath = originalPath + ".meta";
                    if (File.Exists(metaPath))
                    {
                        File.Move(metaPath, originalMetaPath);
                    }
                    
                    return true;
                }
                else
                {
                    Debug.LogWarning($"[BuildExcluder] Excluded asset not found: {excludedPath}");
                    return false;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[BuildExcluder] Failed to restore asset {assetPath}: {e.Message}");
                return false;
            }
        }
    }
}
