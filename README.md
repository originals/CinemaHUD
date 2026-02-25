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
| **On-Demand Content** | Play pre-recorded videos and VODs alongside live streams |
| **Radio Streams** | Play internet radio with live track info display |
| **On-Screen Display** | Floating window overlay, freely position and resize |
| **In-Game World Display** | Place 3D video screens at custom locations in the game world |
| **Twitch Chat** | View Twitch chat in a separate dockable window |
| **Presets** | Pre-configured stream presets and world locations |
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

![On-Screen Display](https://www.gw2opus.com/wp-content/uploads/2026/02/windowedscreen.png)

| Action | Control |
|--------|---------|
| Move window | Drag anywhere on the video |
| Resize | Drag corners or edges |
| Show controls | Hover over the video |
| Lock position | Click lock icon to prevent accidental moves |

---

### In-Game World Display

Place a virtual screen at any location in the game world. The screen renders in 3D and responds to your camera position.

![In-Game World Display](https://www.gw2opus.com/wp-content/uploads/2026/02/ingamescreen.png)

Select a location from the Display tab or create your own using the Location Editor.

---

## Main Settings Window

Access the settings window via the CinemaHUD corner icon or Blish HUD module settings.

![Access Settings](https://www.gw2opus.com/wp-content/uploads/2026/02/accesssettings.png)

### Display Tab

Configure display mode and manage world screen locations.

![Display Tab](https://www.gw2opus.com/wp-content/uploads/2026/02/displaysettings.png)

| Element | Description |
|---------|-------------|
| **Enabled** | Toggle CinemaHUD on/off |
| **Display Mode** | Switch between On-Screen and In-Game World |

#### Categories Menu

| Category | Description |
|----------|-------------|
| **My Locations** | Your saved custom world locations |
| **Preset Categories** | Pre-configured locations for cities, meta events, and points of interest (e.g., Divinity's Reach, Infusion Meta Train) |

#### Toolbar Icons (My Locations)

| Icon | Action |
|------|--------|
| ![Add](https://www.gw2opus.com/wp-content/uploads/2026/02/addnew.png) | Add new custom location |
| ![Import](https://www.gw2opus.com/wp-content/uploads/2026/02/import.png) | Import location from clipboard (JSON) |

#### Location Card Actions

| Icon | Action |
|------|--------|
| ![Info](https://www.gw2opus.com/wp-content/uploads/2026/02/infoloc.png) | View location details (preset locations) |
| ![Waypoint](https://www.gw2opus.com/wp-content/uploads/2026/02/wayploc.png) | Copy associated waypoint (preset locations) |
| ![Edit](https://www.gw2opus.com/wp-content/uploads/2026/02/editloc.png) | Edit saved location |
| ![Export](https://www.gw2opus.com/wp-content/uploads/2026/02/exportloc.png) | Export location to clipboard |
| ![Delete](https://www.gw2opus.com/wp-content/uploads/2026/02/removeloc.png) | Delete saved location |

---

### Source Tab (Channel Guide)

Browse and select streams to play.

![Source Tab](https://www.gw2opus.com/wp-content/uploads/2026/02/channelguide.png)

#### Categories Menu

| Category | Description |
|----------|-------------|
| **Preset Categories** | Pre-configured stream collections (GW2, Music, etc.) |
| **Followed Channels** | Your followed Twitch channels (requires Twitch login) |
| **My Streams** | Your saved custom streams |

#### Stream Types

| Type | Description |
|------|-------------|
| **Live Streams** | Twitch channels and other live broadcasts |
| **On-Demand** | Hosted videos (direct video URLs)  |
| **Radio** | Audio-only streams with track info display |

#### Stream Card Actions

| Icon | Action |
|------|--------|
| ![Chat](https://www.gw2opus.com/wp-content/uploads/2026/02/twitch-chat.png) | Open Twitch chat for channel |
| ![Waypoint](https://www.gw2opus.com/wp-content/uploads/2026/02/wayploc.png) | Copy associated waypoint |
| ![World](https://www.gw2opus.com/wp-content/uploads/2026/02/worldicon.png) | Apply world position preset |
| ![Youtube](https://www.gw2opus.com/wp-content/uploads/2026/02/youtube.png) | Open relevant youtube page |
| ![Edit](https://www.gw2opus.com/wp-content/uploads/2026/02/editloc.png) | Edit saved stream |
| ![Delete](https://www.gw2opus.com/wp-content/uploads/2026/02/removeloc.png) | Delete saved stream |

#### Toolbar Icons

| Icon | Action |
|------|--------|
| ![Refresh](https://www.gw2opus.com/wp-content/uploads/2026/02/refresh.png) | Refresh category data |
| ![Add](https://www.gw2opus.com/wp-content/uploads/2026/02/addnew.png) | Add new custom stream |

---

## Playback Controls

Hover over the video to reveal playback controls. Controls differ based on display mode:

### On-Screen Window Controls

Controls appear as an overlay on the video window.

![On-Screen Controls](https://www.gw2opus.com/wp-content/uploads/2026/02/controlsoverlay.png)

### In-Game World Controls

Controls appear at the bottom of your screen when looking at the world display.

![In-Game World Controls](https://www.gw2opus.com/wp-content/uploads/2026/02/controlsingameworld.png)

### Control Bar Icons

| Icon | Action |
|------|--------|
| ![Play/Pause](https://www.gw2opus.com/wp-content/uploads/2026/02/pauseicon.png) | Play or pause playback |
| ![Volume](https://www.gw2opus.com/wp-content/uploads/2026/02/volumecontrol.png) | Adjust volume (click to mute/unmute, drag slider) |
| ![Chat](https://www.gw2opus.com/wp-content/uploads/2026/02/twitchchat.png) | Open Twitch chat window (Twitch streams only) |
| ![Settings](https://www.gw2opus.com/wp-content/uploads/2026/02/settings.png) | Open quality selection menu |
| ![Lock](https://www.gw2opus.com/wp-content/uploads/2026/02/lockscreen.png) | Lock window position (prevents accidental moves) |
| ![Close](https://www.gw2opus.com/wp-content/uploads/2026/02/closeicon.png) | Close video / disable CinemaHUD |

### Seek Bar

For seekable content (video files, VODs), a seek bar appears showing playback progress. Click or drag to seek to a specific position.

![Seek Bar](https://www.gw2opus.com/wp-content/uploads/2026/02/seekbar.png)

### Stream Info

For radio streams with metadata support (Shoutcast/Icecast), the current track name and artist are displayed on the screen.
For overlay screen mode, info will appear next to the lock icon.

### Quality Selection

Click the settings icon to open the quality menu. Available qualities depend on the stream source.

![Quality Selection](https://www.gw2opus.com/wp-content/uploads/2026/02/quality.png)

---

## Twitch Chat Window

A separate window displaying Twitch chat for the current channel.

![Twitch Chat Window](https://www.gw2opus.com/wp-content/uploads/2026/02/chatwindow.png)

| Feature | Description |
|---------|-------------|
| **Resize** | Drag corners or edges to resize |
| **Move** | Drag title bar to reposition |
| **Lock** | Click lock icon to prevent accidental repositioning |
| **Auto-connect** | Connects automatically when playing a Twitch stream |

### Chat Authentication

To send messages in chat, link your Twitch account via the Source tab → Followed Channels → Login. Viewing chat does not require authentication.

---

## Editor Windows

### Stream Editor

Add or edit custom streams.

![Stream Editor](https://www.gw2opus.com/wp-content/uploads/2026/02/editstream.png)

| Field | Description |
|-------|-------------|
| **Stream Name** | Display name for the stream |
| **Source Type** | Twitch Channel or URL |
| **Value** | Channel name or Stream URL |

> **Note:** On-demand content (videos ending in .mp4, .webm, .mkv, etc.) is automatically detected.

---

### Location Editor

Create or edit world screen locations.

![Location Editor](https://www.gw2opus.com/wp-content/uploads/2026/02/editlocation.png)

| Control | Description |
|---------|-------------|
| **Location Name** | Display name for the location |
| **Set to My Current Position** | Capture current player position |
| **Move Position Controls** | Fine-tune X/Y/Z position |
| **Rotation Controls** | Adjust screen rotation |
| **Screen Width** | Set the width of the world screen |
| **Export** | Copy location data to clipboard (JSON) for sharing |

---

### Twitch Login Window

Link your Twitch account to access followed channels.

![Twitch Login Window](https://www.gw2opus.com/wp-content/uploads/2026/02/twitchwindow.png)

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

![Location Info Window](https://www.gw2opus.com/wp-content/uploads/2026/02/infowindow.png)

| Element | Description |
|---------|-------------|
| **Screenshot** | Preview image of the location |
| **Description** | Location description |
| **Copy Waypoint** | Copy the associated waypoint to clipboard |

---

### Third-Party Notices Window

View licenses for third-party libraries used by CinemaHUD.

---

## Blish HUD Module Settings

Access additional settings via the Blish HUD settings panel (Settings → Modules → CinemaHUD).

![Blish HUD Module Settings](https://www.gw2opus.com/wp-content/uploads/2026/02/blishseettigns.png)


| Element | Description |
|---------|-------------|
| **Enable Cinema** | Toggle CinemaHUD on/off |
| **Twitch Login** | Connect your Twitch account (alternative to Source tab login) |
| **Third-Party Notices** | View open source licenses |

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
| **Twitch Ads** | Ads will play even if you are subscribed to a channel. CinemaHUD is not a first-party Twitch app, so subscription ad-free benefits do not apply. |
| **World Screen Occlusion** | In-game world screens render on top of all objects. Players and objects between you and the screen will not occlude it. |
| **Performance** | Video playback may impact performance on lower-end systems. Reduce stream quality if needed. |

---

## Feedback

Contact **Ori (Originals.8492)** in-game for suggestions, ideas, or feedback.

---

## License

See [THIRD-PARTY-NOTICES.txt](ref/THIRD-PARTY-NOTICES.txt) for third-party software licenses.
