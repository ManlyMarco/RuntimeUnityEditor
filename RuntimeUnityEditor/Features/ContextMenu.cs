using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RuntimeUnityEditor.Core.ChangeHistory;
using RuntimeUnityEditor.Core.Inspector.Entries;
using RuntimeUnityEditor.Core.ObjectTree;
using RuntimeUnityEditor.Core.ObjectView;
using RuntimeUnityEditor.Core.Utils;
using RuntimeUnityEditor.Core.Utils.Abstractions;
using RuntimeUnityEditor.Core.Utils.ObjectDumper;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RuntimeUnityEditor.Core
{
    /// <summary>
    /// Context menu invoked by right clicking on many things.
    /// </summary>
    public class ContextMenu : FeatureBase<ContextMenu>
    {
        private object _obj;
        private MemberInfo _objMemberInfo;
        private string _objName;

        private Rect _windowRect;
        private int _windowId;

        /// <summary>
        /// Is the menu currently visible. A valid object must be set first or it will always be false.
        /// </summary>
        public override bool Enabled
        {
            get => base.Enabled && _obj != null;
            set
            {
                if (value && _obj == null)
                    value = false;
                base.Enabled = value;
            }
        }

        /// <summary>
        /// Contents of the context menu.
        /// </summary>
        public List<MenuEntry> MenuContents { get; } = new List<MenuEntry>();
        private List<MenuEntry> _currentContents;

        /// <inheritdoc />
        protected override void Initialize(InitSettings initSettings)
        {
            MenuContents.AddRange(new[]
            {
                new MenuEntry("! Destroyed unity Object !", obj => obj is UnityEngine.Object uobj && !uobj, null),

                new MenuEntry("Preview", o => ObjectViewWindow.Initialized && ObjectViewWindow.Instance.CanPreview(o), o => ObjectViewWindow.Instance.SetShownObject(o, _objName)),

                new MenuEntry("Show event details", o => o is UnityEventBase && ObjectViewWindow.Initialized,
                              o => ObjectViewWindow.Instance.SetShownObject(ReflectionUtils.GetEventDetails((UnityEventBase)o), o + " - Event details")),

                new MenuEntry("Find in object tree", o => o is GameObject || o is Component, o => ObjectTreeViewer.Instance.SelectAndShowObject((o as GameObject)?.transform ?? ((Component)o).transform)),

                new MenuEntry(),

                new MenuEntry("Send to inspector", o => Inspector.Inspector.Initialized, o =>
                {
                    if (o is Type t)
                        Inspector.Inspector.Instance.Push(new StaticStackEntry(t, _objName), true);
                    else
                        Inspector.Inspector.Instance.Push(new InstanceStackEntry(o, _objName), true);
                }),

                new MenuEntry("Send to REPL", o => REPL.ReplWindow.Initialized, o => REPL.ReplWindow.Instance.IngestObject(o)),

                new MenuEntry(),

                new MenuEntry("Copy to clipboard", o => Clipboard.ClipboardWindow.Initialized, o =>
                {
                    if (Clipboard.ClipboardWindow.Contents.LastOrDefault() != o)
                        Clipboard.ClipboardWindow.Contents.Add(o);
                }),
                //todo Paste from clipboard, kind of difficult

                new MenuEntry("Export texture...",
                              o => o is Texture ||
                                   o is Sprite ||
                                   (o is Material m && m.mainTexture != null) ||
                                   (o is Image i && i.mainTexture != null) ||
                                   (o is RawImage ri && ri.mainTexture != null) ||
                                   (o is Renderer r && (r.sharedMaterial ?? r.material) != null && (r.sharedMaterial ?? r.material).mainTexture != null),
                              o =>
                              {
                                  if (o is Texture t)
                                      t.SaveTextureToFileWithDialog();
                                  else if (o is Sprite s)
                                      s.texture.SaveTextureToFileWithDialog();
                                  else if (o is Material m)
                                      m.mainTexture.SaveTextureToFileWithDialog();
                                  else if (o is Image i)
                                      i.mainTexture.SaveTextureToFileWithDialog();
                                  else if (o is RawImage ri)
                                      ri.mainTexture.SaveTextureToFileWithDialog();
                                  else if (o is Renderer r)
                                      (r.sharedMaterial ?? r.material).mainTexture.SaveTextureToFileWithDialog();
                              }),

                new MenuEntry("Replace texture...",
                              o => o is Texture2D ||
                                   (o is Material m && m.mainTexture != null) ||
                                   (o is RawImage i && i.mainTexture != null) ||
                                   (o is Renderer r && (r.sharedMaterial ?? r.material) != null && (r.sharedMaterial ?? r.material).mainTexture != null),
                              o =>
                              {
                                  string filename = "null";
                                  var newTex = TextureUtils.LoadTextureFromFileWithDialog(ref filename);

                                  if (o is Texture2D t)
                                  {
                                      t.LoadRawTextureData(newTex.GetRawTextureData());
                                      t.Apply(true);
                                      UnityEngine.Object.Destroy(newTex);
                                      Change.Report($"(ContextMenu)::{_objName}.LoadImage(File.ReadAllBytes(\"{filename}\"))");
                                  }
                                  else if (o is Material m)
                                  {
                                      m.mainTexture = newTex;
                                      Change.Report($"(ContextMenu)::{_objName}.mainTexture = Texture2D.LoadImage(File.ReadAllBytes(\"{filename}\"))");
                                  }
                                  else if (o is Image i && i.material != null)
                                  {
                                      i.material.mainTexture = newTex;
                                      Change.Report($"(ContextMenu)::{_objName}.mainTexture = Texture2D.LoadImage(File.ReadAllBytes(\"{filename}\"))");
                                  }
                                  else if (o is RawImage ri)
                                  {
                                      ri.texture = newTex;
                                      Change.Report($"(ContextMenu)::{_objName}.texture = Texture2D.LoadImage(File.ReadAllBytes(\"{filename}\"))");
                                  }
                                  else if (o is Renderer r)
                                  {
                                      (r.sharedMaterial ?? r.material).mainTexture = newTex;
                                      Change.Report($"(ContextMenu)::{_objName}.mainTexture = Texture2D.LoadImage(File.ReadAllBytes(\"{filename}\"))");
                                  }
                              }),

                new MenuEntry("Export mesh to .obj", o => o is Renderer r && MeshExport.CanExport(r), o => MeshExport.ExportObj((Renderer)o, false, false)),
                new MenuEntry("Export mesh to .obj (Baked)", o => o is Renderer r && MeshExport.CanExport(r), o => MeshExport.ExportObj((Renderer)o, true, false)),
                new MenuEntry("Export mesh to .obj (World)", o => o is Renderer r && MeshExport.CanExport(r), o => MeshExport.ExportObj((Renderer)o, true, true)),

                new MenuEntry("Dump object to file...", o => true, o => Dumper.DumpToTempFile(o, _objName)),

                new MenuEntry("Destroy", o => o is UnityEngine.Object uo && uo, o => Change.Action("(ContextMenu)::UnityEngine.Object.Destroy({0})", o is Transform t ? t.gameObject : (UnityEngine.Object)o, UnityEngine.Object.Destroy)),

                new MenuEntry(),

                new MenuEntry("Find references in scene", o => ObjectViewWindow.Initialized && o.GetType().IsClass, o => ObjectTreeViewer.Instance.FindReferencesInScene(o)),

                new MenuEntry("Find member in dnSpy", o => DnSpyHelper.IsAvailable && _objMemberInfo != null, o => DnSpyHelper.OpenInDnSpy(_objMemberInfo)),
                new MenuEntry("Find member type in dnSpy", o => DnSpyHelper.IsAvailable, o => DnSpyHelper.OpenInDnSpy(o.GetType()))
            });

            _windowId = base.GetHashCode();
            Enabled = false;
            DisplayType = FeatureDisplayType.Hidden;
        }

        /// <summary>
        /// Show the context menu at current cursor position.
        /// </summary>
        /// <param name="obj">Object to show the menu for. Set to null to hide the menu.</param>
        /// <param name="objMemberInfo">MemberInfo of wherever the object came from. Can be null.</param>
        public void Show(object obj, MemberInfo objMemberInfo)
        {
            var m = UnityInput.Current.mousePosition;
            Show(obj, objMemberInfo, new Vector2(m.x, Screen.height - m.y));
        }

        /// <summary>
        /// Show the context menu at a specific screen position.
        /// </summary>
        /// <param name="obj">Object to show the menu for. Set to null to hide the menu.</param>
        /// <param name="objMemberInfo">MemberInfo of wherever the object came from. Can be null.</param>
        /// <param name="clickPoint">Screen position to show the menu at.</param>
        public void Show(object obj, MemberInfo objMemberInfo, Vector2 clickPoint)
        {
            _windowRect = new Rect(clickPoint, new Vector2(100, 100));

            if (obj != null)
            {
                _obj = obj;
                _objMemberInfo = objMemberInfo;
                _objName = objMemberInfo != null ? $"{objMemberInfo.DeclaringType?.Name}.{objMemberInfo.Name}" : obj.GetType().FullDescription();

                _currentContents = MenuContents.Where(x => x.IsVisible(_obj)).ToList();

                // hack to discard old state of the window and make sure it appears correctly when rapidly opened on different items
                _windowId++;

                Enabled = true;
            }
            else
            {
                _obj = null;
                Enabled = false;
            }
        }

        /// <summary>
        /// Draw a GUILayout button that opens the context menu when clicked. It's only shown if the object is not null.
        /// </summary>
        public void DrawContextButton(object obj, MemberInfo objMemberInfo)
        {
            if (obj != null && GUILayout.Button("...", GUILayout.ExpandWidth(false)))
                Show(obj, objMemberInfo);
        }

        /// <inheritdoc />
        protected override void OnGUI()
        {
            // Make an invisible window in the back to reliably capture mouse clicks outside of the context menu.
            // If mouse clicks in a different window then the input will be eaten and context menu would go to the back and stay open instead of closing.
            // Can't use GUI.Box and such because it will always be behind GUI(Layout).Window no matter what you do.
            // Use a label skin with no content to make both the window and button invisible while spanning the entire screen without borders.
            var backdropWindowId = _windowId - 10;
            GUILayout.Window(backdropWindowId, new Rect(0, 0, Screen.width, Screen.height), id =>
            {
                if (GUILayout.Button(GUIContent.none, GUI.skin.label, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
                    Enabled = false;
            }, GUIContent.none, GUI.skin.label);

            GUI.color = Color.white;

            IMGUIUtils.DrawSolidBox(_windowRect);

            _windowRect = GUILayout.Window(_windowId, _windowRect, DrawMenu, _objName);

            IMGUIUtils.EatInputInRect(_windowRect);

            // Ensure the context menu always stays on top while it's open. Can't use FocusWindow here because it locks up everything else.
            GUI.BringWindowToFront(backdropWindowId);
            GUI.BringWindowToFront(_windowId);

            if (_windowRect.xMax > Screen.width) _windowRect.x = Screen.width - _windowRect.width;
            if (_windowRect.yMax > Screen.height) _windowRect.y = Screen.height - _windowRect.height;
        }

        private void DrawMenu(int id)
        {
            if (_currentContents == null || _currentContents.Count == 0)
            {
                Enabled = false;
                return;
            }

            GUILayout.BeginVertical();
            {
                foreach (var menuEntry in _currentContents)
                {
                    if (menuEntry.Draw(_obj))
                        Enabled = false;
                }
            }
            GUILayout.EndVertical();
        }

        /// <summary>
        /// A single entry in the context menu.
        /// </summary>
        public readonly struct MenuEntry
        {
            /// <summary>
            /// Create a new context menu entry.
            /// </summary>
            /// <param name="name">Name of the enry.</param>
            /// <param name="onCheckVisible">Callback that checks if this item is visible for a given object.</param>
            /// <param name="onClick">Callback invoked when user clicks on this menu entry with the object as argument.</param>
            public MenuEntry(string name, Func<object, bool> onCheckVisible, Action<object> onClick) : this(new GUIContent(name), onCheckVisible, onClick) { }

            /// <inheritdoc cref="MenuEntry(string,Func&lt;object, bool&gt;, Action&lt;object&gt;)"/>
            public MenuEntry(GUIContent name, Func<object, bool> onCheckVisible, Action<object> onClick)
            {
                _name = name;
                _onCheckVisible = onCheckVisible;
                _onClick = onClick;
            }

            private readonly GUIContent _name;
            private readonly Func<object, bool> _onCheckVisible;
            private readonly Action<object> _onClick;

            /// <summary>
            /// Check if this menu entry should be visible for a given object.
            /// </summary>
            public bool IsVisible(object obj)
            {
                return _onCheckVisible == null || _onCheckVisible(obj);
            }

            /// <summary>
            /// Draw this menu entry. Handles user clicking on the entry too.
            /// </summary>
            public bool Draw(object obj)
            {
                if (_onClick != null)
                {
                    if (GUILayout.Button(_name))
                    {
                        if (IMGUIUtils.IsMouseRightClick())
                            return false;

                        _onClick(obj);
                        return true;
                    }
                }
                else if (_name != null)
                {
                    GUILayout.Label(_name);
                }
                else
                {
                    GUILayout.Space(4);
                }

                return false;
            }
        }
    }
}
