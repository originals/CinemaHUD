<p align="center">
  <img width="256" height="256" alt="CinemaHUD" src="https://www.gw2opus.com/wp-content/uploads/2026/02/quaggantv_highres.png" />
</p>

<p align="center">
  <img height="80" alt="CinemaHUD" src="https://www.gw2opus.com/wp-content/uploads/2026/02/cinemahudtext.png" />
</p>

<p align="center">
  <em>
    A <a href="https://blishhud.com">Blish HUD</a> module for Guild Wars 2 that lets you watch Twitch streams or play any VLC stream directly in-game.
  </em>
</p>

<p align="center">
  Display it on your screen or place video screens in the game world.<br>
  The perfect way to throw an in-game watch party with your guildmates or friends.
</p>

---

## Table of Contents

- [Features](#features)
- [Installation](#installation)
- [Display Modes](#display-modes)
- [Main Settings Window](#main-settings-window)
- [Playback Controls](#playback-controls)
- [Twitch Chat Window](#twitch-chat-window)
- [Editor Windows](#editor-windows)
- [Supported Media Formats](#supported-media-formats)
- [Known Limitations](#known-limitations)
- [Feedback](#feedback)
- [License](#license)

---

## Features

| Feature | Description |
|---------|-------------|
| **Twitch Integration** | Watch live Twitch streams with quality selection |
| **Followed Channels** | Link your Twitch account to see your followed live channels |
| **Custom URLs** | Play any video URL supported by LibVLC |
| **On-Screen Display** | Floating window overlay, freely position and resize |
| **In-Game World Display** | Place video screens at custom locations in the game world |
| **Twitch Chat** | View Twitch chat in a separate dockable window |
| **Presets** | Community-curated stream presets and world locations |
| **Save & Share** | Add, save, export, and import your own streams and locations |

---

## Installation

1. Install [Blish HUD](https://blishhud.com)
2. Download `CinemaHUD.bhm` from the releases page
3. Place the file in `Documents/Guild Wars 2/addons/blishhud/modules`
4. Launch Blish HUD and enable CinemaHUD in the module settings

---

## Display Modes

### On-Screen Display

A floating video window that stays on top of the game. Drag to reposition and resize to your preference.

<!-- IMAGE_PLACEHOLDER: Screenshot of On-Screen Display mode showing a video playing in a floating window overlay -->

| Action | Control |
|--------|---------|
| Move window | Drag anywhere on the video |
| Resize | Drag corners or edges |
| Show controls | Hover over the video |

---

### In-Game World Display

Place a virtual screen at any location in the game world. The screen renders in 3D and responds to your camera position.

<!-- IMAGE_PLACEHOLDER: Screenshot of In-Game World Display showing a video screen placed in the game world -->

Select a location from the Display tab or create your own using the Location Editor.

---

## Main Settings Window

Access the settings window via the CinemaHUD corner icon or Blish HUD module settings.

<!-- IMAGE_PLACEHOLDER: Screenshot of the Main Settings Window -->

### Display Tab

Configure display mode and manage world screen locations.

<!-- IMAGE_PLACEHOLDER: Screenshot of the Display Tab -->

| Element | Description |
|---------|-------------|
| **Enabled** | Toggle CinemaHUD on/off |
| **Display Mode** | Switch between On-Screen and In-Game World |
| **Locations List** | Browse preset and saved world locations |
| **+ Add New** | Create a new world location |
| **Import** | Import location from clipboard (JSON) |

#### Location Card Actions

| Icon | Action |
|------|--------|
| ![Info](<!-- ICON_PLACEHOLDER: info_icon -->) | View location details |
| ![Edit](<!-- ICON_PLACEHOLDER: edit_icon -->) | Edit saved location |
| ![Export](<!-- ICON_PLACEHOLDER: export_icon -->) | Export location to clipboard |
| ![Delete](<!-- ICON_PLACEHOLDER: delete_icon -->) | Delete saved location |

---

### Source Tab (Channel Guide)

Browse and select streams to play.

<!-- IMAGE_PLACEHOLDER: Screenshot of the Source Tab -->

#### Categories Menu

| Category | Description |
|----------|-------------|
| **Preset Categories** | Community-curated stream collections (GW2, Music, etc.) |
| **Followed Channels** | Your followed Twitch channels (requires Twitch login) |
| **My Streams** | Your saved custom streams |

#### Stream Card Actions

| Icon | Action |
|------|--------|
| ![Chat](<!-- ICON_PLACEHOLDER: chat_icon -->) | Open Twitch chat for channel |
| ![Waypoint](<!-- ICON_PLACEHOLDER: waypoint_icon -->) | Copy associated waypoint |
| ![World](<!-- ICON_PLACEHOLDER: world_icon -->) | Apply world position preset |
| ![Edit](<!-- ICON_PLACEHOLDER: edit_icon -->) | Edit saved stream |
| ![Delete](<!-- ICON_PLACEHOLDER: delete_icon -->) | Delete saved stream |

#### Toolbar Icons

| Icon | Action |
|------|--------|
| ![Refresh](<!-- ICON_PLACEHOLDER: refresh_icon -->) | Refresh category data |
| ![Add](<!-- ICON_PLACEHOLDER: add_icon -->) | Add new custom stream |
| ![Import](<!-- ICON_PLACEHOLDER: import_icon -->) | Import stream from clipboard |

---

## Playback Controls

Hover over the video to reveal playback controls.

<!-- IMAGE_PLACEHOLDER: Screenshot of playback controls overlay on the video -->

### Control Bar Icons

| Icon | Action |
|------|--------|
| ![Play/Pause](<!-- ICON_PLACEHOLDER: play_pause_icon -->) | Play or pause playback |
| ![Volume](<!-- ICON_PLACEHOLDER: volume_icon -->) | Adjust volume (click to mute/unmute, drag slider) |
| ![Chat](<!-- ICON_PLACEHOLDER: chat_icon -->) | Open Twitch chat window (Twitch streams only) |
| ![Settings](<!-- ICON_PLACEHOLDER: settings_icon -->) | Open quality selection menu |
| ![Lock](<!-- ICON_PLACEHOLDER: lock_icon -->) | Lock window position (prevents accidental moves) |
| ![Close](<!-- ICON_PLACEHOLDER: close_icon -->) | Close video / disable CinemaHUD |

### Quality Selection

Click the settings icon to open the quality menu. Available qualities depend on the stream source.

<!-- IMAGE_PLACEHOLDER: Screenshot of quality selection dropdown -->

---

## Twitch Chat Window

A separate window displaying Twitch chat for the current channel.

<!-- IMAGE_PLACEHOLDER: Screenshot of the Twitch Chat Window -->

| Feature | Description |
|---------|-------------|
| **Resize** | Drag corners or edges to resize |
| **Move** | Drag title bar to reposition |
| **Lock** | Click lock icon to prevent accidental repositioning |
| **Auto-connect** | Connects automatically when playing a Twitch stream |

### Chat Authentication

To view chat, link your Twitch account via the Source tab ? Followed Channels ? Login.

---

## Editor Windows

### Stream Editor

Add or edit custom streams.

<!-- IMAGE_PLACEHOLDER: Screenshot of the Stream Editor Window -->

| Field | Description |
|-------|-------------|
| **Stream Name** | Display name for the stream |
| **Source Type** | Twitch Channel or URL |
| **Value** | Channel name or media URL |

---

### Location Editor

Create or edit world screen locations.

<!-- IMAGE_PLACEHOLDER: Screenshot of the Location Editor Window -->

| Control | Description |
|---------|-------------|
| **Location Name** | Display name for the location |
| **Set to My Current Position** | Capture current player position |
| **Move Controls** | Fine-tune X/Y/Z position |
| **Rotation Controls** | Adjust screen rotation |
| **Screen Width** | Set the width of the world screen |

---

### Twitch Login Window

Link your Twitch account to access followed channels.

<!-- IMAGE_PLACEHOLDER: Screenshot of the Twitch Login Window -->

| Step | Action |
|------|--------|
| 1 | Click "Connect Twitch" |
| 2 | A code is displayed |
| 3 | Click "Open Activation Page" |
| 4 | Enter the code on Twitch and authorize |

Your token is stored locally only and never shared.

---

### Location Info Window

View details about a preset location.

<!-- IMAGE_PLACEHOLDER: Screenshot of the Location Info Window -->

| Element | Description |
|---------|-------------|
| **Screenshot** | Preview image of the location |
| **Description** | Location description |
| **Copy Waypoint** | Copy the associated waypoint to clipboard |

---

### Third-Party Notices Window

View licenses for third-party libraries used by CinemaHUD.

---

## Supported Media Formats

CinemaHUD uses LibVLC for media playback.

| Type | Formats |
|------|---------|
| **Video Files** | MP4, MKV, AVI, WEBM, MOV, FLV |
| **Live Streams** | M3U8, HLS, RTSP, RTMP |
| **Audio Streams** | MP3, AAC, OGG, internet radio |
| **Local Files** | `file:///C:/path/to/video.mp4` |

---

## Known Limitations

| Limitation | Description |
|------------|-------------|
| **World Screen Occlusion** | In-game world screens render on top of all objects. Players and objects between you and the screen will not occlude it. |
| **Performance** | Video playback may impact performance on lower-end systems. Reduce stream quality if needed. |

---

## Feedback

Contact **Ori (Originals.8492)** in-game for suggestions, ideas, or feedback.

---

## License

See [THIRD-PARTY-NOTICES.txt](ref/THIRD-PARTY-NOTICES.txt) for third-party software licenses.
