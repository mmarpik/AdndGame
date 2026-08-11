# Wizardry Viewer — Unity project

URP, Unity 6000.4.11f1. The tabletop end of the game: `Adnd.Game` publishes a snapshot of the
party, the level and the current fight to `127.0.0.1:8787` after every turn, and this project
lays that out as a miniatures table you watch on a second monitor.

## First run

1. Open this folder from Unity Hub. First open resolves URP, Newtonsoft and TMP — a few
   minutes.
2. If prompted, **Window → TextMeshPro → Import TMP Essential Resources**.
3. **Wizardry Viewer → Build Sample Table**.
4. **File → Build** for Windows, output into `Build/` (kept out of the repo — it is 100 MB of
   build output).
5. From the repo root, `.\play-wizardry.ps1`. That starts the viewer, waits for it to take the
   port, then starts the game on the other monitor.

To watch the table without the game, press Play in the editor instead of step 4 — but only one
process can hold port 8787, so a Play session and a built viewer cannot run at once.

Step 3 generates everything: URP asset, materials, placeholder prefabs, and the scene with
the table, lamp, camera rig and wiring. Re-running it rebuilds the scene from scratch — the
generated assets under `Assets/Generated` are disposable.

## What is a placeholder

Everything visual. The prefabs are primitives at correct real-world scale:

| Thing | Now | Later |
|---|---|---|
| Floor tile | 25.4 mm card-coloured square | Cardboard texture set, printed dungeon art |
| Wall piece | dark card strip on edge | Card walls with tabs, torch sconces |
| Standee | 28 mm box on a round base | Printed cardboard figures, or plastic minis |
| Lighting | one point light + fill | Baked lightmaps, area light for the lamp |
| DM | nothing | The hand |

Scale is real on purpose — 1-inch tiles, 28 mm figures, a 140×90 cm table at 73 cm. Art can
be swapped in without moving anything, and a headset later sees a correctly-sized table.

## Layout

```
Assets/
  Plugins/WizardryViewer/   copied from the Drive repo — protocol, playback, presentation
  Scripts/                  Unity-side: transport, receiver, table renderer, subtitle
  Editor/                   the scene builder
  Generated/                created by the menu item; disposable
```

`Assets/Plugins/WizardryViewer` is **vendored**: the protocol, playback and presentation types
are shared with the game side, and this is the copy Unity compiles. It is the source of truth in
this repo — edit it here. (Martin maintains a separate working copy of those files outside the
repo; when it changes, the change is copied in, not the other way round.)

## Notes

- The transport sits behind `ISnapshotTransport` because `HttpListener` is unreliable on
  Android/IL2CPP. See `docs/quest-door-open.md` in the Drive repo.
- The subtitle is a world-space TextMeshPro on the table, not a screen overlay — overlay
  canvases do not render in VR.
- The camera is a child of `CameraRig` and never positioned directly.
