# Runtime Unity Editor / Debugging Tools
In-game inspector, editor and interactive console for applications made with Unity3D game engine. It's designed for debugging and modding Unity games, but can also be used as a universal trainer. Runs under [BepInEx5](https://github.com/BepInEx/BepInEx), BepInEx6 IL2CPP and [UMM](https://github.com/newman55/unity-mod-manager).

## Features
- Works on most games made in Unity 4.x or newer that use either the mono or IL2CPP runtime (currently IL2CPP support is in beta)
- Minimal impact on the game - no GameObjects or Components are spawned (outside of the plugin component loaded by the mod loader) and no hooks are used (except if requested for profiler)
- GameObject and component browser
- Object inspector (allows modifying values of objects in real time) with clipboard
- REPL C# console with autostart scripts
- Simple Profiler
- Object serialization/dumping
- dnSpy integration (navigate to member in dnSpy)
- Mouse inspect (find objects or UI elements by clicking with mouse)
- Gizmos (Transform origin, Renderer bounds, Collider area, etc.)
- All parts are integrated together (e.g. REPL console can access inspected object, inspector can focus objects on GameObject list, etc.)
- Right click on most objects to bring up a context menu with more options
- and many other...

![preview](https://user-images.githubusercontent.com/39247311/208912018-014154e1-7ad8-4df0-a4a3-662c334ccedc.jpg)

## How to use
RUE is available for different mod loaders in separate builds. They are largely the same, although BepInEx version is the most supported and has some extra features. If you don't already have to use a specific mod loader, BepInEx is recommended.

### BepInEx (mono)
1. Install BepInEx v4.x or v5.x if you don't have it already. You can download it [here](https://github.com/BepInEx/BepInEx).
2. Download the latest BepInEx build (RuntimeUnityEditor_BepInEx5_vX.X.zip) from the [Releases](https://github.com/ManlyMarco/RuntimeUnityEditor/releases) page. Make sure to get the correct version for your BepInEx.
3. Extract the BepInEx folder from the archive directly into your game directory (you should already have a BepInEx folder there from previous step). Replace files if asked.
4. To turn on press the F12 key when in-game. A window should appear on top of the game. If it doesn't appear, check logs for errors.

Note: If the plugin fails to load under BepInEx 4 with a type load exception, move RuntimeUnityEditor.Core.dll to BepInEx/core folder.

### BepInEx (IL2CPP)
**WARNING:** This is a beta build that is unstable and with many features broken. It is likely to not work in many (most?) IL2CPP games, as it relies on unstripped UnityEngine.IMGUIModule.dll (and possibly other game-specific differences). It works in HoneyCome if used together with the [IMGUIModule.Il2Cpp.CoreCLR.Patcher](https://github.com/IllusionMods/BepisPlugins/releases/tag/r19.3.2) which can be used with some other games with some degree of success.

IL2CPP bug reports will be largely ignored, while PRs to fix issues will be greatly appreciated.

1. If you don't have it already, install and configure BepInEx v6 (at least be.664). You can download it [here](https://github.com/BepInEx/BepInEx).
2. Download the latest RUE release for your version of BepInEx from the [Releases](https://github.com/ManlyMarco/RuntimeUnityEditor/releases) page (make sure it's the IL2CPP version).
3. Extract the release archive from the archive directly into your game directory (you should already have a BepInEx folder there from previous step). Replace files if asked.
4. To turn on press the F12 key when in-game. A window should appear on top of the game. If it doesn't appear, check game log for errors.

### Unity Mod Manager
1. Install Unity Mod Manager if you don't have it already. You can download it [here](https://www.nexusmods.com/site/mods/21). Select your game from the the dropdown list and then click the Install button.
2. Download the latest UMM build (RuntimeUnityEditor_UMM_vX.X.zip) from the [Releases](https://github.com/ManlyMarco/RuntimeUnityEditor/releases) page.
3. Goto the Mods tab in Unity Mod Manager and drag the RuntimeUnityEditor zip file into the Unity Mod Manager GUI at the bottom where it says drop zip file here.
4. To turn on press the F12 key when in-game. A window should appear on top of the game. If it doesn't appear, check logs for errors.


## Known issues
- Text is invisible, scrambled, not scaled properly or otherwise looks wrong: Most likely the `Arial.ttf` font is missing from the system (Unity UI default font, may be different in some games). This can happen when running a game on Linux with [misconfigured wine](https://github.com/ManlyMarco/RuntimeUnityEditor/issues/55), and on some regional versions of Windows (e.g. Chinese). To resolve this you have to find the Arial font and install it.
- The C# REPL console is only available in a subset of games. This is because mcs (the current REPL backend) does not work with `.NET Standard` runtime used by default in more recent versions of Unity (because of missing features). There is no fix, the backend will have to be changed, possibly to Lua.
- The wireframe toggle might not work in some games because of an incompatible rendering setup.
- The IL2CPP version is broken in `insert your game name here`. At the moment the main issue is stripped IMGUI assemblies, see https://github.com/ManlyMarco/RuntimeUnityEditor/issues/97


## How to build
1. Get Visual Studio 2019 (or later).
2. Clone the repository recursively (`git clone --recursive https://github.com/ManlyMarco/RuntimeUnityEditor`, VS 2022 can do it too). 
3. Open the solution in Visual Studio and hit Build All.

Notes:
- If you already have the repository cloned or want to update the mcs submodule you need to run `git submodule update --init --recursive` on your local repository (if you have VS 2022 you can do it from the source control tab).
- An old version of UnityEngine.dll (pre-2017) is used for compatibility with all Unity versions 4.x and above, because the new UnityEngine.dll in 2017+ forwards all of the split types into their new respective dll files.

---

You can support development of my plugins through my Patreon page: https://www.patreon.com/ManlyMarco
