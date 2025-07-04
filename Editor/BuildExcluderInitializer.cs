using UnityEditor;
using UnityEngine;

namespace Yans.BuildExcluder
{
    [InitializeOnLoad]
    public static class BuildExcluderInitializer
    {
        static BuildExcluderInitializer()
        {
            EditorApplication.delayCall += CheckAndRestoreAssets;
        }

        // On Unity startup, restore any excluded assets if needed
        private static void CheckAndRestoreAssets()
        {
            try
            {
                BuildExcluderProcessor.RestoreExcludedAssets();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[BuildExcluder] Error during initialization restore: {e.Message}");
            }
        }
    }
}
