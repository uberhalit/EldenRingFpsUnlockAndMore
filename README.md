# EXPERIMENTAL! NOT YET TESTED MUCH
Please make sure your game is set to offline and you start the game without EAC through the utility.

# Elden Ring FPS Unlocker and more

A small utility to remove frame rate limit for [Elden Ring](https://en.bandainamcoent.eu/elden-ring/elden-ring) written in C#. More features soon!
Patches games memory while running, does not modify any game files. Works with every game version (legit steam & oh-not-so-legit), should work with all future updates.

## Download

**[Get the latest release here](https://github.com/uberhalit/EldenRingFpsUnlockAndMore/releases)**

## Features

* does not modify any game files, RAM patches only
* works with legit, unmodified steam version as well as with unpacked, not-so-legit versions
* unlock frame rate (remove FPS limit) by setting a new custom limit

## Usage

The graphic setup has to be done only once but as the patcher hot-patches the memory **you have to start the patcher every time you want to use any of its features**.

The game enforces VSYNC and forces 60 Hz in fullscreen even on 144 Hz monitors so we have to override these.

#### Nvidia: Use Nvidia Control Panel to set 'Vsync' to 'Off' and 'Preferred Refreshrate' to 'Highest available' on a Elden Ring Profile.
#### AMD: Use Radeon Settings to set 'Wait for Vertical Refresh' to 'Enhanced Sync' on a Elden Ring profile. Start Elden Ring in windowed mode and switch to fullscreen once ingame. Troubleshoot: see the guide further down below.

#### 60 Hz monitors: disable VSYNC via driver (use 'Enhanced Sync' on AMD) and use fullscreen, see guide below
#### high refresh rate monitors: use borderless or force monitor to always use highest available refresh rate and then use fullscreen, see guide below

### Follow these steps on Nvidia:
1. Open Nvidia Control Panel
2. Navigate to `Display -> Change resolution`
3. **Make sure your monitor is set to the highest Refresh rate possible:**
4.  [![Make sure your monitor is set to the highest Refresh rate possible](https://camo.githubusercontent.com/331eb420bee67f4e57d7e46601bfd51f462de68f/68747470733a2f2f692e696d6775722e636f6d2f625667767155372e706e67)](#)
5. Navigate to `3D Settings -> Manage 3D settings -> Program Settings -> Elden Ring`
6. **Set `Preferred refresh rate` to `Highest available`**
7. **Set `Vertical sync` to `Off`**
8. Hit apply and close Nvidia Control Panel
9. Start `Elden Ring FPS Unlocker and more` and start the game through the first button
10. Use fullscreen (144 Hz or 60 Hz Monitors) or borderless window mode (144 Hz Monitors)
11. Set your new refresh rate limit and click `Patch game`

### Follow these steps on AMD:
1. Right click on Desktop -> `Display settings`
2. Scroll down and click `Advanced Display Settings -> Display Adapter Properties`
3. **Switch to `Monitor` tab and make sure your monitor is set to the highest Refresh rate possible:**
4. Open Radeon Settings
5. Navigate to `Gaming -> Elden Ring` or add it manually if it's missing: `Add -> Browse -> Elden Ring`
6. **Set `Wait for Vertical Refresh` to `Enhanced Sync`**:
7.  Apply and close Radeon Settings
8. Start `Elden Ring FPS Unlocker and more` and start the game through the first button
9. Use fullscreen (144 Hz or 60 Hz Monitors) or borderless window mode (144 Hz Monitors)
10. Set your new refresh rate limit and click `Patch game`

### To play the game with GSYNC do these additional steps (Nvidia):
1. Under Nvidia Control Panel navigate to `3D Settings -> Manage 3D settings -> Program Settings -> Elden Ring`
2. Set `Monitor Technology` to `G-SYNC`
3. You can keep `Vertical sync` on `Use the 3D application setting` now to help remove frame time stutters ([see here](https://www.blurbusters.com/gsync/gsync101-input-lag-tests-and-settings/15/))
4. Make sure that `Preferred refresh rate` is still set to `Highest available`
5. Don't forget to Apply and close Nvidia Control Panel
6. Use a 3rd party frame rate limiter like [RTSS](https://www.guru3d.com/files-details/rtss-rivatuner-statistics-server-download.html) and set a frame rate limit just a few fps below your monitor refresh rate, on a 144Hz Monitor use 138
7. Start `Elden Ring FPS Unlocker and more` and set FPS lock to your monitors refresh rate
8. Start the game and set it to Fullscreen
9. Enjoy perfectly tearing free variable high refresh rates without VSYNC

### To add a custom resolution:
*soon!*

### To use the FOV changer:
*soon!*

## Troubleshooting:
* Utility can't seem to find the game? - Make sure your game exe is called `eldenring.exe`
* Make sure you followed the appropriate steps and didn't skip any
* Try disabling `Fullscreen optimization` for Elden Ring: right mouse click on `eldenring.exe -> Compatibility-> tick 'Disable fullscreen optimizations'`
* If you are using ReShade make sure your preset doesn't enforce 60 Hz, try removing ReShade and see if it solves the problem
* Try adding the whole game folder and `Elden Ring FPS Unlocker and more` to your antivirus's exclusion list
* Try disabling `Steam Broadcast` (streaming via overlay)
* Try to force disable VSYNC even when you are using GSYNC
* Close and disable all screen recording and streaming applications
* Close and disable all overlays
* Close and disable all performance "booster" programs and alike
* Do a clean reinstall of your graphic driver:
  1. Download latest graphics driver for your GPU
  2. Download [DDU](https://www.guru3d.com/files-get/display-driver-uninstaller-download,1.html)
  3. Disconnect internet so windows update won't auto-install minimal driver as soon as you uninstall them
  4. Boot into safe mode
  5. Completely uninstall graphics driver and all of their utilities using DDU
  6. Reboot
  7. Install the latest driver you previously downloaded
  8. Reconnect internet

## Prerequisites

* .NET Framework 4.8
* administrative privileges (for patching)
* 64 bit OS

## Building

Use Visual Studio 2022 to build

## Contributing

Feel free to open an issue or create a pull request at any time

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details

## Credits

## Limitations

* the game has forced VSYNC so unlocking the frame rate when your monitor has 60Hz will do nothing. You'll have to disable VSYNC in Nvidia Control Panel or AMD Radeon Settings first, see Usage
* in fullscreen the game forces the monitor to 60 Hz so you'll have to handle this with driver override too, see Usage
* if your monitor does not support Hz override (Preferred Refreshrate missing and Profile Inspector won't work either) you won't be able to play at a higher refresh rate in fullscreen, play in windowed mode as an alternative

## Version History
* v0.0.0.2-beta (2022-02-25)
  * added game checks
  * fixed broken game start
  * added prompt to select game installation path
  * removed reference to external MS DLL
  * multiple fixes
* v0.0.0.1-beta (2022-02-25)
  * Initial release
