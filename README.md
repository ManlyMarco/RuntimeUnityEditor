# Runtime Unity Editor / Debugging Tools
In-game inspector, editor and interactive console for applications made with Unity3D game engine. It's designed for debugging and modding Unity games, but can also be used as an universal trainer.

### Features
- Works on most Unity games supported by BepInEx
- GameObject and component browser
- Object inspector that allows modifying values of objects in real time
- REPL C# console
- All parts are integrated together (e.g. REPL console can access inspected object, inspector can focus objects on GameObject list, etc.)

![preview](https://user-images.githubusercontent.com/39247311/64476158-ce1a4c00-d18b-11e9-97d6-084452cdbf0a.PNG)

### How to use
- This is a BepInEx plugin. It requires BepInEx v4 or later. Grab it from [here](https://github.com/BepInEx/BepInEx
) and follow installation instructions.
- Download the latest build from the [Releases](https://github.com/ManlyMarco/RuntimeUnityEditor/releases) page.
- To install place the .dll in the BepInEx directory inside your game directory (BepInEx/Plugins for BepInEx 5).
- To turn on press the F12 key when in-game. A window should appear on top of the game. If it doesn't appear, check logs for errors.

Note: If the plugin fails to load under BepInEx 4 with a type load exception, move RuntimeUnityEditor.Core.dll to BepInEx/core folder.

### How to build
- At least VS 2017 is recommended.
- The mcs_custom dependency is this https://github.com/kkdevs/mcs - basically a port of roslyn-level mcs with all new language features to .Net 2.0
- You have to reference UnityEngine.dll from Unity 5.x, before it was split. The new UnityEngine.dll forwards all of the split types into their new respective dll files, therefore doing this allows runtime editor to run on any Unity version.

---

You can support development of my plugins through my Patreon page: https://www.patreon.com/ManlyMarco
