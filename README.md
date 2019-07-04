# Runtime Unity Editor / Debugging Tools
In-game inspector, editor and interactive console for applications made with Unity3D game engine. It's designed for debugging and modding Unity games, but can also be used as an universal trainer.

### Features
- Works on most Unity games supported by BepInEx
- GameObject and component browser
- Object inspector that allows modifying values of objects in real time
- REPL C# console
- All parts are integrated together (e.g. REPL console can access inspected object, inspector can focus objects on GameObject list, etc.)

![preview](https://user-images.githubusercontent.com/39247311/53586063-a8324000-3b87-11e9-8209-57e660d2949d.png)
![preview](https://user-images.githubusercontent.com/39247311/49837301-2d3a6400-fda6-11e8-961a-9a85f1247705.PNG)

### How to use
- This is a BepInEx plugin. It requires BepInEx v4 or later. Grab it from [here](https://github.com/BepInEx/BepInEx
) and follow installation instructions.
- Download the latest build from the [Releases](https://github.com/ManlyMarco/RuntimeUnityEditor/releases) page.
- To install place the .dll in the BepInEx directory inside your game directory (BepInEx/Plugins for BepInEx 5).
- To turn on press the F12 key when in-game. A window should appear on top of the game. If it doesn't appear, check logs for errors.

Note: If the plugin fails to load under BepInEx 4 with a type load exception, move RuntimeUnityEditor.Core.dll to BepInEx/core folder.

You can support development of my plugins through my Patreon page: https://www.patreon.com/ManlyMarco
