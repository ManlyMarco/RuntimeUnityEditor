# Runtime Unity Editor / Debugging Tools
In-game inspector, editor and interactive console for applications made with Unity3D game engine. It's designed for debugging and modding Unity games, but can also be used as a universal trainer. Runs under [BepInEx](https://github.com/BepInEx/BepInEx).

### Features
- Works on most games made in Unity 4.x or newer that use the mono runtime (IL2CPP is not supported at the moment)
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
- and many other...

![preview](https://user-images.githubusercontent.com/39247311/208912018-014154e1-7ad8-4df0-a4a3-662c334ccedc.jpg)

### How to use
1. Install BepInEx v4.x or v5.x if you don't have it already. You can download it [here](https://github.com/BepInEx/BepInEx
).
2. Download the latest build from the [Releases](https://github.com/ManlyMarco/RuntimeUnityEditor/releases) page. Make sure to get the correct version for your BepInEx.
3. Extract the BepInEx folder from the archive directly into your game directory (you should already have a BepInEx folder there from previous step). Replace files if asked.
4. To turn on press the F12 key when in-game. A window should appear on top of the game. If it doesn't appear, check logs for errors.

Note: If the plugin fails to load under BepInEx 4 with a type load exception, move RuntimeUnityEditor.Core.dll to BepInEx/core folder.

### How to build
1. Get Visual Studio 2019 (or later).
2. Clone the repository recursively (`git clone --recursive https://github.com/ManlyMarco/RuntimeUnityEditor`, VS 2022 can do it too). 
3. Open the solution in Visual Studio and hit Build All.

Notes:
- If you already have the repository cloned or want to update the mcs submodule you need to run `git submodule update --init --recursive` on your local repository (if you have VS 2022 you can do it from the source control tab).
- An old version of UnityEngine.dll (pre-2017) is used for compatibility with all Unity versions 4.x and above, because the new UnityEngine.dll in 2017+ forwards all of the split types into their new respective dll files.

---

You can support development of my plugins through my Patreon page: https://www.patreon.com/ManlyMarco
