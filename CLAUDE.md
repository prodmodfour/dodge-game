# Unity Project — AI Agent Instructions

## Project Structure
- `Assets/` — All game assets (scripts, scenes, prefabs, materials, etc.)
- `Assets/Scripts/` — C# game scripts
- `ProjectSettings/` — Unity project configuration (YAML, version controlled)
- `Packages/` — Package manifest and overrides

## Key Settings
- **Serialization**: Force Text (all .unity/.prefab/.asset files are human-readable YAML)
- **Meta Files**: Visible (every asset has a .meta sidecar with its GUID)
- **Graphics API**: Vulkan (NVIDIA GPU)

## Working with Unity Files from CLI
- Scene files (`.unity`) and prefabs (`.prefab`) are YAML — you can read and grep them
- C# scripts in `Assets/Scripts/` can be edited directly; Unity hot-reloads on focus
- After editing scripts, run: `unity-ctrl editor refresh --compile`
- Check for errors: `unity-ctrl console --type error`
- Run tests: `unity-ctrl test` (EditMode) or `unity-ctrl test --mode PlayMode`

## unity-ctrl CLI Commands (requires Connector package in project)
- `unity-ctrl status` — check editor state
- `unity-ctrl editor refresh --compile` — recompile scripts
- `unity-ctrl console --type error` — check for errors
- `unity-ctrl exec "<C# code>"` — execute C# in the editor
- `unity-ctrl test --filter <name>` — run tests
- `unity-ctrl screenshot` — capture scene view

## Conventions
- Use `[SerializeField]` for inspector-exposed private fields
- Use namespaces matching folder structure
- One MonoBehaviour per file, filename matches class name
