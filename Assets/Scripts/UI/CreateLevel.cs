using System;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MoreMountains.TopDownEngine
{
    [RequireComponent(typeof(RectTransform))]
    public class CreateLevel : MonoBehaviour
    {
        [SerializeField] private LevelInfo levels;
        [SerializeField] private LevelSelect levelPrefab;
        [SerializeField] Transform levelParent;
        [SerializeField] bool buildOnStart = true;

        void Start()
        {
            if (buildOnStart)
                BuildLevels();
        }

        [ContextMenu("Build Levels")]
        public void BuildLevels()
        {
            if (levels == null)
            {
                Debug.LogWarning("CreateLevel: Levels ScriptableObject is not assigned.", this);
                return;
            }

            if (levelPrefab == null)
            {
                Debug.LogWarning("CreateLevel: levelPrefab is not assigned.", this);
                return;
            }

            if (levelParent == null)
                levelParent = this.transform;

            // Clear existing children created by this component
            for (int i = levelParent.childCount - 1; i >= 0; i--)
            {
                var child = levelParent.GetChild(i);
                DestroyImmediate(child.gameObject);
            }

            var infos = levels.LevelsArray;
            if (infos == null || infos.Length == 0)
                return;

            for (int i = 0; i < infos.Length; i++)
            {
                var info = infos[i];
                var go = Instantiate(levelPrefab, levelParent);
                go.name = $"Level_{i + 1}_{SanitizeName(info?.LevelName.ToString())}";
                PopulateLevel(go.gameObject, info);
                go.levelInfo = info;
                if (i <= LevelController.Instance.CurrentLevel - 1)
                {
                    go.lockIcon.SetActive(false);
                    go.GetComponent<Button>().interactable = true;
                }
            }
        }

        static string SanitizeName(string s) => string.IsNullOrWhiteSpace(s) ? "Unnamed" : s.Replace('/', '-').Replace('\\', '-');

        void PopulateLevel(GameObject instance, LevelInfo.LevelCondition info)
        {
            if (instance == null || info == null) return;

            var components = instance.GetComponents<MonoBehaviour>();
            foreach (var comp in components)
            {
                if (comp == null) continue;
                var method = comp.GetType().GetMethod("Populate", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new Type[] { typeof(LevelInfo.LevelCondition) }, null);
                if (method != null)
                {
                    method.Invoke(comp, new object[] { info });
                    return;
                }
            }

            // 2) Fallback: find children by conventional names and set texts (supports TMP and legacy UI Text)
            SetTextByName(instance.transform, "LevelName", info.LevelName.ToString());
        }

        void SetTextByName(Transform root, string childName, string value)
        {
            if (root == null) return;
            var child = root.Find(childName);
            if (child != null)
            {
                var tmp = child.GetComponent<TMP_Text>();
                if (tmp != null)
                {
                    tmp.text = value;
                    return;
                }

                var text = child.GetComponent<Text>();
                if (text != null)
                {
                    text.text = value;
                    return;
                }
            }

            // If a specific child wasn't found, try to find the first matching component in children
            var tmpInChildren = root.GetComponentInChildren<TMP_Text>(true);
            if (tmpInChildren != null && tmpInChildren.gameObject.name.IndexOf(childName, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                tmpInChildren.text = value;
                return;
            }

            var textInChildren = root.GetComponentInChildren<Text>(true);
            if (textInChildren != null && textInChildren.gameObject.name.IndexOf(childName, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                textInChildren.text = value;
            }
        }
    }
}
