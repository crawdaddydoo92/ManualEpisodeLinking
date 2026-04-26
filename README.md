# Manual Episode Linking (Jellyfin Plugin)

## Overview

Manual Episode Linking is a Jellyfin plugin that lets you control what plays next — even across different shows or movies.

Originally built for TV crossovers, it now also supports automatic movie sequel playback.

---

## ✨ What This Solves

Jellyfin normally:

- Plays episodes only within the same series
- Does not support crossovers between shows
- Does not link separate movies together

This plugin lets you:

- Seamlessly continue crossovers between different series
- Automatically play the next movie in a franchise
- Maintain the correct viewing order without renaming or reorganizing files

---

## 🆕 New in 1.1.1

- Movie sequel chaining
  - Automatically plays the next movie (e.g., trilogies)
  - Smooth, seamless transitions

- Improved playback stability
  - Eliminates duplicate triggers
  - More reliable transitions

- Smarter execution
  - Movies transition immediately
  - TV waits for a safe playback reset

---

## 🆕 New in 1.1.0

- Full chain support
  - Link items across any number of steps (A → B → C → D → ...)

- Last-hop protection
  - Prevents unwanted playback when starting at the end of a chain
  - Only completes links when part of a valid sequence

- Works with all playback styles
  - Natural playback (letting items finish)
  - Skip / next controls

---

## ⚙️ How It Works

The plugin monitors playback and prepares the next item ahead of time.

1. Near the end (~99%)
   - Detects if a custom next item exists
   - Queues it without interrupting playback

2. At the correct moment
   - Movies: transition immediately
   - TV: wait for playback reset, then transition

### Key Idea

Detect early → transition at the right moment

---

## 📦 Installation

### 1. Build the plugin

dotnet build -c Release

### 2. Copy files to Jellyfin

/etc/jellyfin/plugins/ManualEpisodeLinking/

Required:

- ManualEpisodeLinking.dll

Optional:

- JSON config files (e.g., csi.json, movies.json)

### 3. Restart Jellyfin (first install only)

After installation, config changes reload automatically.

---

## 🧩 Configuration

The plugin uses one or more JSON files to define links.

### Location

/etc/jellyfin/plugins/ManualEpisodeLinking/

### Example

{
  "Links": {
    "ITEM_ID_A": "ITEM_ID_B",
    "ITEM_ID_B": "ITEM_ID_C"
  }
}

### Notes

- Keys = current item
- Values = next item
- IDs must be Jellyfin ItemIds
- Use lowercase, no dashes
- Chains can be any length

---

## ⚠️ Movie Configuration Requirement

For movie chaining to work correctly when starting playback manually, you must enable:

"AllowLastHopFromManualStart": true

### Why this matters

By default:

AllowLastHopFromManualStart = false

This is ideal for TV crossovers, but for movies it can block the final transition.

### Recommended movie config

{
  "AllowLastHopFromManualStart": true,
  "Links": {
    "MOVIE_1_ID": "MOVIE_2_ID",
    "MOVIE_2_ID": "MOVIE_3_ID"
  }
}

---

## 🔄 Live Reload

- Changes apply automatically
- No restart required
- Updates are detected within ~1 second

---

## 🔗 How Linking Works

Links are directional and defined step-by-step.

Example:

Episode A → Episode B  
Episode B → Episode C

If no link exists, Jellyfin behaves normally.

---

## ▶️ Playback Behavior

- If a link exists → plugin overrides playback
- If no link exists → Jellyfin continues normally

### TV Behavior

- Requires entering the chain correctly
- Final step is protected (no forced jump if started there)

### Movie Behavior

- Always allows the final step (when configured)
- Designed for seamless franchise playback

---

## ⚠️ ItemID Stability (Important)

Links rely on Jellyfin ItemIds.

- Library refreshes do NOT change IDs
- Metadata edits do NOT change IDs

However:

- Replacing or modifying a file may generate a new ID

If this happens, update your JSON config.

---

## 🚧 Limitations

- No UI (manual configuration required)
- Uses polling (not event-based yet)
- Limited multi-session support
- No validation for circular links
- Scrubbing near the end may trigger linking

---

## 🔮 Future Plans

- Web UI for managing links
- Event-based playback detection
- Better multi-user support
- Link validation tools

---

## 🧠 Why This Works

Jellyfin resets playback sessions between items.

This plugin works around that by:

- Preparing the next item early
- Waiting for a stable moment to switch
- Avoiding interruptions and timing issues

---

## 📁 Project Structure

ManualEpisodeLinking/
├── PlaybackMonitor.cs
├── Plugin.cs
├── csi.json
├── movies.json
├── ManualEpisodeLinking.csproj

---

## 📜 License / Status

Private development project (for now)

---

## 💬 Summary

Manual Episode Linking gives you full control over playback order:

- Cross-series episode continuity
- Automatic movie sequel playback
- Clean, uninterrupted viewing experience
