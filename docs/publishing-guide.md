# Flow Launcher Plugin Store Publishing Guide

This guide walks you through releasing SnapSync `v1.0.0` and publishing it to the official Flow Launcher Plugin Store.

---

## 1. Commit and Push Changes to GitHub

Ensure all repository changes are committed and pushed to `main`:

```powershell
git add .
git commit -m "chore: configure release automation and metadata for Flow Launcher Store"
git push origin main
```

---

## 2. Create and Push Release Tag

Trigger the automated GitHub Actions build and release workflow:

```powershell
git tag v1.0.0
git push origin v1.0.0
```

---

## 3. Verify GitHub Release

1. Navigate to `https://github.com/HungNth/Flow.Launcher.Plugin.SnapSync/actions` to monitor the **Publish Release** workflow.
2. Once complete, check `https://github.com/HungNth/Flow.Launcher.Plugin.SnapSync/releases/tag/v1.0.0`.
3. Confirm that `Flow.Launcher.Plugin.SnapSync.zip` is attached as an asset (size should be ~250 KB - 300 KB).

---

## 4. Submit Pull Request to Flow Launcher Store

1. Fork the [Flow.Launcher.PluginsManifest](https://github.com/Flow-Launcher/Flow.Launcher.PluginsManifest) repository on GitHub.
2. In your fork, create or copy the file `plugins/SnapSync-E8332E900BD54E2C854DC406B78B1143.json` located at the root of this repository.
3. Commit the file and open a Pull Request targeting the `main` branch of `Flow-Launcher/Flow.Launcher.PluginsManifest`:
   - **PR Title**: `Add SnapSync plugin`
   - **PR Description**: Include link to `https://github.com/HungNth/Flow.Launcher.Plugin.SnapSync`.
4. Wait for automated CI scans (VirusTotal scan and manifest validation) to pass, followed by review and merge by the Flow Launcher maintainers.

---

## 5. Future Releases & Updates

- **Automatic updates**: Every 3 hours, Flow Launcher's CI checks all registered repositories.
- When you release a new version in the future:
  1. Increment `"Version"` in `plugin.json` (e.g., `1.0.1`).
  2. Push a new tag (e.g., `git tag v1.0.1 && git push origin v1.0.1`).
  3. No PR to `Flow.Launcher.PluginsManifest` is needed for version updates! Flow's bot will update the store manifest automatically.
