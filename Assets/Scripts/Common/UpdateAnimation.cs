using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class UpdateAnimation : MonoBehaviour
{
    [Header("Animator references")]
    [Tooltip("Animator của character chính. Nếu để trống sẽ tự tìm Animator trên object này hoặc parent.")]
    [SerializeField] private Animator MainAnimator;
    [Tooltip("Danh sách Animator của 2 character phụ. Nếu để trống sẽ tự tìm tất cả Animator con (không bao gồm MainAnimator).")]
    [SerializeField] private Animator[] SecondaryAnimators;

    [Header("Parameters to sync")]
    [Tooltip("Nếu để trống sẽ sync các parameter mặc định: Speed, IsMoving, MoveX, MoveZ")]
    [SerializeField] private string[] ParameterNames;

    [Header("Settings")]
    [Tooltip("Nếu true sẽ tự động tìm 2 Animator phụ nếu SecondaryAnimators rỗng")]
    [SerializeField] private bool AutoFindSecondaries = true;
    [Tooltip("Sync mỗi frame trong LateUpdate (khuyến nghị để override animations đã apply trên Main)")]
    [SerializeField] private bool SyncInLateUpdate = true;

    // cached parameter types from main animator
    private Dictionary<string, AnimatorControllerParameterType> _mainParameterTypes;
    // cached list for secondaries
    private List<Animator> _secondaries = new List<Animator>();

    void Awake()
    {
        if (MainAnimator == null)
        {
            MainAnimator = GetComponent<Animator>() ?? GetComponentInParent<Animator>();
        }

        // default parameter names if none provided
        if (ParameterNames == null || ParameterNames.Length == 0)
        {
            ParameterNames = new string[] { "Speed", "IsMoving", "MoveX", "MoveZ" };
        }

        // build cache of parameter types
        _mainParameterTypes = new Dictionary<string, AnimatorControllerParameterType>();
        if (MainAnimator != null)
        {
            foreach (var p in MainAnimator.parameters)
            {
                _mainParameterTypes[p.name] = p.type;
            }
        }

        // secondaries assignment / autodetect
        if (SecondaryAnimators != null && SecondaryAnimators.Length > 0)
        {
            foreach (var a in SecondaryAnimators)
            {
                if (a != null && a != MainAnimator)
                {
                    _secondaries.Add(a);
                }
            }
        }

        if (AutoFindSecondaries && _secondaries.Count == 0)
        {
            // find all Animator components in children (exclude main)
            var animators = GetComponentsInChildren<Animator>(true);
            foreach (var a in animators)
            {
                if (a == MainAnimator) continue;
                _secondaries.Add(a);
            }
        }
    }

    void LateUpdate()
    {
        if (!SyncInLateUpdate) return;
        SyncParameters();
    }

    void Update()
    {
        if (SyncInLateUpdate) return;
        SyncParameters();
    }

    private void SyncParameters()
    {
        if (MainAnimator == null || _secondaries == null || _secondaries.Count == 0) return;

        // For each parameter name requested, if main has it, copy to secondaries
        foreach (var paramName in ParameterNames)
        {
            if (!_mainParameterTypes.TryGetValue(paramName, out var pType))
            {
                // main doesn't have this parameter, skip
                continue;
            }

            switch (pType)
            {
                case AnimatorControllerParameterType.Float:
                    float f = MainAnimator.GetFloat(paramName);
                    for (int i = 0; i < _secondaries.Count; i++) _secondaries[i].SetFloat(paramName, f);
                    break;
                case AnimatorControllerParameterType.Int:
                    int n = MainAnimator.GetInteger(paramName);
                    for (int i = 0; i < _secondaries.Count; i++) _secondaries[i].SetInteger(paramName, n);
                    break;
                case AnimatorControllerParameterType.Bool:
                    bool b = MainAnimator.GetBool(paramName);
                    for (int i = 0; i < _secondaries.Count; i++) _secondaries[i].SetBool(paramName, b);
                    break;
                case AnimatorControllerParameterType.Trigger:
                    // cannot read trigger state reliably; skip copying triggers.
                    break;
            }
        }
    }

    // Public helper: refresh caches (call from editor or runtime if animators / parameters changed)
    public void RefreshCaches()
    {
        Awake();
    }
}
