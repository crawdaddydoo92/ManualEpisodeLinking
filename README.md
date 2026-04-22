# Manual Episode Linking (Jellyfin Plugin)

## Overview

Manual Episode Linking is a Jellyfin plugin that allows you to define custom episode-to-episode playback transitions.

It is primarily designed to support **crossovers between different TV series**, where Jellyfin’s default "Next Episode" behavior does not work.

---

## ✨ What This Solves

Jellyfin normally:

* Plays episodes sequentially within a single series
* Does not support cross-series continuity

This plugin allows you to:

* Link episodes across different shows
* Create seamless crossover playback
* Maintain correct viewing order without renaming or reorganizing media

---

## 🆕 New in 1.1.0

* **Full chain support**

  * Link episodes across any number of steps (A → B → C → D → ...)

* **Last-hop protection**

  * Prevents unintended playback when starting at the end of a chain
  * Only allows links to complete when part of a valid sequence

* **Works with both playback styles**

  * Natural playback (letting episodes finish)
  * Skip / next controls

---

## ⚙️ How It Works

The plugin monitors playback and uses a two-phase system:

1. **Near the end (~99%)**

   * Detects if a custom “next episode” exists
   * Queues the next episode (does NOT interrupt playback)

2. **After playback resets (start of next episode)**

   * Starts the queued episode at the correct time

### Key Principle

> **Detect early → act late**

This ensures:

* No early playback interruption
* Stable session handling
* Compatibility with Jellyfin’s internal behavior

---

## 📦 Installation

### 1. Build the plugin

```bash
dotnet build -c Release
```

### 2. Copy files to Jellyfin

Copy the compiled output to:

```
/etc/jellyfin/plugins/ManualEpisodeLinking/
```

Required files:

* `ManualEpisodeLinking.dll`
* `links.json`

### 3. Restart Jellyfin (first install only)

After initial installation, changes to `links.json` are applied automatically.

---

## 🧩 Configuration

The plugin is configured using a `links.json` file.

### Location

```
/etc/jellyfin/plugins/ManualEpisodeLinking/links.json
```

### Example

```json
{
  "Links": {
    "EPISODE_ID_A": "EPISODE_ID_B",
    "EPISODE_ID_B": "EPISODE_ID_C"
  }
}
```

### Notes

* Keys = current episode
* Values = next episode
* IDs must be Jellyfin ItemIds
* IDs should be normalized (lowercase, no dashes)
* Chains of any length are supported (A → B → C → D → ...)

---

### 🔄 Live Reload

The plugin automatically reloads `links.json` when it is modified.

* No restart required after updates
* Changes apply within a second of saving

---

## 🔗 How Linking Works

Links are directional and must be defined for each step in a crossover chain.

Example:

CSI S02E22 → Miami S01E01
Miami S01E01 → CSI S02E23

If you do not include a return link, playback will continue within the crossover series.

This gives full control over multi-part crossovers across multiple shows.

---

## ▶️ Playback Behavior

* If a link exists → plugin overrides the next episode
* If no link exists → Jellyfin continues normally

### Last-Hop Protection

If you start playback at the final episode in a chain:

* The plugin will NOT force a jump
* Jellyfin behaves normally

Once you enter a chain properly, all steps are allowed to complete.

---

## ⚠️ ItemID Stability (Important)

Jellyfin ItemIDs are tied to the underlying media file.

* A normal library refresh will **not** change ItemIDs
* Editing metadata will **not** change ItemIDs

However, if a file is modified or replaced, a new ItemID may be assigned.

If this happens, links in `links.json` must be updated.

### Recommendation

* Prefer editing metadata inside Jellyfin
* Avoid renaming files after linking

---

## 🚧 Limitations

* Manual configuration (no UI yet)
* Uses polling instead of event hooks
* Limited multi-session awareness
* No validation for circular links
* Scrubbing near the end may trigger linking

---

## 🔮 Future Improvements

* Web UI for managing links
* Event-driven playback detection
* Multi-session support
* Smarter crossover handling

---

## 🧠 Why This Approach

Jellyfin recreates playback sessions between episodes.

This plugin works around that by:

* Queuing the next episode instead of playing immediately
* Waiting for a stable playback state
* Executing transitions safely

---

## 📁 Project Structure

```
ManualEpisodeLinking/
├── PlaybackMonitor.cs
├── Plugin.cs
├── links.json
├── ManualEpisodeLinking.csproj
```

---

## 📜 License / Status

Private development project (for now)

---

## 💬 Summary

This plugin gives you precise control over episode order across series, enabling seamless crossover viewing without modifying your media library.

---
