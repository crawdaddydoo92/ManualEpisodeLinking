# Manual Episode Linking (Jellyfin Plugin)

## Overview

Manual Episode Linking is a Jellyfin plugin that lets you control what plays next — even across different shows or movies.

Originally built for TV crossovers, it now also supports **automatic movie sequel playback**.

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

- **Movie sequel chaining**
  - Automatically plays the next movie (e.g., trilogies)
  - Smooth, seamless transitions — no delay

- **Improved playback stability**
  - Eliminates duplicate triggers
  - More reliable transitions between items

- **Smarter execution**
  - Movies transition immediately
  - TV waits for safe playback reset

---

## 🆕 New in 1.1.0

- **Full chain support**
  - Link items across any number of steps (A → B → C → D → ...)

- **Last-hop protection**
  - Prevents unwanted playback when starting at the end of a chain
  - Only completes links when part of a valid sequence

- **Works with all playback styles**
  - Natural playback (letting items finish)
  - Skip / next controls

---

## ⚙️ How It Works

The plugin watches playback and prepares the next item ahead of time.

1. **Near the end (~99%)**
   - Detects if a custom next item exists
   - Queues it without interrupting playback

2. **At the correct moment**
   - Movies: transition immediately
   - TV: wait for playback reset, then transition

### Key Idea

> Detect early → transition at the right moment

This avoids interruptions and keeps playback stable.

---

## 📦 Installation

### 1. Build the plugin

```bash
dotnet build -c Release
