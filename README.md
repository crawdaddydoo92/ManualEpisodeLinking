# Manual Episode Linking (Jellyfin Plugin)

## 📦 Download

Download the latest plugin from the Releases page:

[Download the latest plugin:](https://github.com/crawdaddydoo92/ManualEpisodeLinking/releases)

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

## ⚙️ How It Works

The plugin monitors playback and uses a two-phase system:

1. **Near the end (~99%)**

   * Detects if a custom “next episode” exists
   * Queues the next episode (does NOT interrupt playback)

2. **After playback resets (0–10%)**

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

### 3. Restart Jellyfin

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
* Chains are supported (A → B → C)

---

## ▶️ Playback Behavior

* If a link exists → plugin overrides the next episode
* If no link exists → Jellyfin default behavior continues

### Additional Behavior

* Skip button works normally
* Rapid transitions are prevented with cooldown logic
* Playback only switches after the current episode finishes

---

## ⚠️ Requirements & Assumptions

* Each episode exists only once in your library
* No duplicate crossover episodes across multiple series
* One authoritative version per episode

---

## 🚧 Limitations

* Manual configuration (no UI yet)
* Uses polling instead of event hooks
* Limited multi-session awareness
* No validation for circular links

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
* Executing the transition safely

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
