using System;
using System.Collections.Generic;
using UnityEngine;

namespace Yans.BuildExcluder
{
    [Serializable]
    public class BuildExcludeConfig
    {
        [SerializeField]
        public List<BuildExcludeEntry> entries = new List<BuildExcludeEntry>();
        
        public static string ConfigPath => "Assets/BuildExcluder/Editor/BuildExcludeConfig.json";
    }

    [Serializable]
    public class BuildExcludeEntry
    {
        [SerializeField]
        public string assetPath;
        
        [SerializeField]
        public List<string> defines = new List<string>();

        public BuildExcludeEntry()
        {
        }

        public BuildExcludeEntry(string assetPath, List<string> defines)
        {
            this.assetPath = assetPath;
            this.defines = defines ?? new List<string>();
        }
    }
}
