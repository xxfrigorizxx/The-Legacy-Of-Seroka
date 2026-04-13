# AGENTS.md

## Cursor Cloud specific instructions

### Project overview

Zero-K: Frozen Legacy is a Godot 4.6 voxel survival game written in C#/.NET 8.0. All code, comments, and documentation are in French.

### Prerequisites (installed on the VM)

- **.NET 8.0 SDK** — `sudo apt-get install -y dotnet-sdk-8.0`
- **Godot 4.6.1 Mono (Linux x86_64)** — installed at `/opt/godot/`, symlinked to `/usr/local/bin/godot`

### Build

```bash
cd /workspace
dotnet restore "Zero-K - Frozen Legacy.csproj"
dotnet build "Zero-K - Frozen Legacy.csproj"
```

### Lint / code quality

There is no dedicated linter config (no `.editorconfig`, no Roslyn analyzers). The build itself surfaces C# warnings. Run with `-warnaserror` for strict checks:

```bash
dotnet build "Zero-K - Frozen Legacy.csproj" --no-restore -warnaserror
```

### Running the game

- **Headless** (no GPU required, for CI/build verification):
  ```bash
  godot --headless --quit          # just verifies startup
  godot --headless --build-solutions --quit  # rebuilds C# from Godot
  ```
- **Editor GUI** (requires DISPLAY):
  ```bash
  DISPLAY=:1 godot -e              # opens the Godot editor
  ```
- **Game GUI** (requires DISPLAY + GPU/software renderer):
  ```bash
  DISPLAY=:1 godot                 # runs the game from main scene (menu_principal.tscn)
  ```

### Gotchas

- The `.csproj` file name contains spaces: `"Zero-K - Frozen Legacy.csproj"` — always quote it in shell commands.
- Godot 4.6 uses Forward Plus renderer and Jolt Physics with very large limits (10M bodies). Headless mode works fine for build verification, but actual gameplay needs a GPU or software rendering fallback.
- The main scene is `menu_principal.tscn` (French main menu). Creating a "Nouveau Monde" generates procedural voxel terrain.
- No automated test suite exists in this project. Verification is done via build + headless startup + manual play-testing.
- No external databases or services are required — all persistence is file-based (binary `.dat` saves).
