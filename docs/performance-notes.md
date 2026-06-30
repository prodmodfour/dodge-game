# Prototype performance notes

Date: 2026-06-30  
Unity: 6000.4.1f1  
Scene: `Assets/Scenes/MainPrototype.unity`

## Capture method

Captured in Editor Play Mode using the ticket validation command:

```bash
unity-ctrl --project "$PWD" editor play --wait
unity-ctrl --project "$PWD" profiler hierarchy --frames 30 --sort self --max 20
unity-ctrl --project "$PWD" editor stop
```

A follow-up `PlayerLoop` drill-down was also captured to separate game/runtime work from editor overhead.

## 30-frame hierarchy summary

Top-level profiler hierarchy, sorted by self time:

| Item | Appeared in | Avg calls | Avg self ms | Avg total ms |
| --- | ---: | ---: | ---: | ---: |
| `EditorLoop` | 30 / 30 | 4 | 100.492 | 100.492 |
| `Profiler.CollectEditorStats` | 30 / 30 | 1 | 0.002 | 0.005 |
| `PlayerLoop` | 30 / 30 | 3 | 0.001 | 4.169 |
| `Profiler.FlushCounters` | 30 / 30 | 1 | 0.001 | 0.232 |

The large `EditorLoop` self time is editor overhead from the interactive profiling environment, not prototype gameplay simulation work.

## PlayerLoop drill-down

Largest observed `PlayerLoop` total-time samples over the same 30-frame capture:

| Item | Appeared in | Avg calls | Avg self ms | Avg total ms | Notes |
| --- | ---: | ---: | ---: | ---: | --- |
| `Mono.JIT` | 5 / 30 | 11.8 | 3.397 | 3.953 | One-off editor/JIT warm-up work during capture. |
| `RenderPlayModeViewCameras` | 30 / 30 | 1 | 0.016 | 3.115 | Game view rendering in editor. |
| `GUI.Repaint` | 30 / 30 | 1 | 0.121 | 2.160 | IMGUI repaint for prototype/debug UI. |
| `UpdateScene` | 30 / 30 | 1 | 0.073 | 1.707 | Scene update/render preparation. |
| `GC.Collect` | 1 / 30 | 1 | 1.697 | 1.697 | Single collection in the measured editor window. |
| `Camera.Render` | 30 / 30 | 1 | 0.041 | 0.919 | Main camera render. |
| `UnitStatusView.OnGUI()` | 30 / 30 | 12 | 0.416 | 0.566 | Nameplate IMGUI cost for visible units. |
| `ActiveActionMenu.OnGUI()` | 30 / 30 | 2 | 0.176 | 0.269 | Prototype action menu. |
| `PrototypeRulesHelpOverlay.OnGUI()` | 30 / 30 | 2 | 0.122 | 0.173 | Help overlay path; hidden/compact in normal play. |
| `CombatLogView.OnGUI()` | 30 / 30 | 2 | 0.097 | 0.144 | Combat log IMGUI repaint. |

## Findings

- No obvious runaway per-frame gameplay work was observed in the editor capture.
- Runtime `PlayerLoop` total time stayed low relative to editor overhead for this small prototype scene.
- Prototype IMGUI views are visible in the hierarchy, but their observed costs were below severe thresholds for this prototype pass.
- A single `GC.Collect` and some `Mono.JIT` samples appeared during the measured window. They look like editor/warm-up artifacts rather than recurring runaway allocations.

## Action taken

No code changes were made. The capture did not show a severe accidental issue that justified optimization within this ticket's limited scope.

## Follow-up note

Re-profile a standalone Linux build later if player-build performance becomes a target; this note only covers an editor Play Mode snapshot.
