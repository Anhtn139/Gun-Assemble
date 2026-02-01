using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MoreMountains.InventoryEngine
{
#if UNITY_EDITOR
    /// <summary>
    /// This class lets you specify (in code) symbols that will be added
    /// to the build settings define symbols list automatically
    /// </summary>
    [InitializeOnLoad]
    public class InventoryEngineDefineSymbols
    {
        /// <summary>
        /// A list of all the symbols you want added to the build settings
        /// </summary>
        public static readonly string[] Symbols = new string[]
        {
            "MOREMOUNTAINS_INVENTORYENGINE"
        };

        /// <summary>
        /// As soon as this class has finished compiling,
        /// adds the specified define symbols to the build settings
        /// </summary>
        static InventoryEngineDefineSymbols()
        {
            AddDefineSymbols(Symbols);
        }

        private static void AddDefineSymbols(string[] symbols)
        {
            BuildTargetGroup targetGroup =
                BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);

            string definesString =
                PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup);

            List<string> definesList = definesString
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .ToList();

            bool changed = false;

            foreach (string symbol in symbols)
            {
                if (!definesList.Contains(symbol))
                {
                    definesList.Add(symbol);
                    changed = true;
                }
            }

            if (changed)
            {
                PlayerSettings.SetScriptingDefineSymbolsForGroup(
                    targetGroup,
                    string.Join(";", definesList)
                );
            }
        }
    }
#endif
}