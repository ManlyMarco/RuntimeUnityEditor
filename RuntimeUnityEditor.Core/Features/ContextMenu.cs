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
        private Func<object> _getValue;
        private Action<object> _setValue;
        private string _objName;

        private Rect _windowRect;
        private int _windowId;

        /// <summary>
        /// Is the menu currently visible. A valid object must be set first or it will always be false.
        /// </summary>
        public override bool Enabled
        {
            get => base.Enabled && (_obj != null || _objMemberInfo != null);
            set
            {
                if (value && _obj == null && _objMemberInfo == null)
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
            // TODO This mess needs a rewrite with a sane API
            MenuContents.AddRange(new[]
            {
                new MenuEntry("! Destroyed unity Object !", obj => obj is UnityEngine.Object uobj && !uobj, null),

                new MenuEntry("Preview", o => o != null && ObjectViewWindow.Initialized && ObjectViewWindow.Instance.CanPreview(o), o => ObjectViewWindow.Instance.SetShownObject(o, _objName)),

                new MenuEntry("Show event details", o => o is UnityEventBase && ObjectViewWindow.Initialized,
                              o => ObjectViewWindow.Instance.SetShownObject(ReflectionUtils.GetEventDetails((UnityEventBase)o), o + " - Event details")),

                new MenuEntry("Find in object tree", o => o is GameObject || o is Component, o => ObjectTreeViewer.Instance.SelectAndShowObject((o as GameObject)?.transform ?? ((Component)o).transform)),

                new MenuEntry(),

                new MenuEntry("Send to inspector", o => o != null && Inspector.Inspector.Initialized, o =>
                {
                    if (o is Type t)
                        Inspector.Inspector.Instance.Push(new StaticStackEntry(t, _objName), true);
                    else
                        Inspector.Inspector.Instance.Push(new InstanceStackEntry(o, _objName), true);
                }),

                new MenuEntry("Send to REPL", o => o != null && REPL.ReplWindow.Initialized, o => REPL.ReplWindow.Instance.IngestObject(o)),

                new MenuEntry(),
            });

            AddBreakpointControls(MenuContents);

            MenuContents.AddRange(new[]
            {
                new MenuEntry("Copy to clipboard", o => o != null && Clipboard.ClipboardWindow.Initialized, o =>
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
                                   o is Texture && _setValue != null ||
                                   o is Material ||
                                   o is RawImage ||
                                   (o is Renderer r && (r.sharedMaterial != null || r.material != null)),
                              o =>
                              {
                                  string filename = "null";
                                  var newTex = TextureUtils.LoadTextureFromFileWithDialog(ref filename);

                                  if (o is Texture t)
                                  {
                                      try
                                      {
                                          if (_setValue != null)
                                          {
                                              var current = _getValue?.Invoke();
                                              Change.Action($"(ContextMenu)::{_objName} = Texture2D.LoadImage(File.ReadAllBytes(\"{filename}\"))", o, action: _ => _setValue(newTex), undoAction: current != null ? _ => _setValue(current) : (Action<object>)null);
                                          }
                                          else throw new NotImplementedException();
                                      }
                                      catch (Exception e)
                                      {
                                          if(!(e is NotImplementedException))
                                              RuntimeUnityEditorCore.Logger.Log(LogLevel.Error, e);

                                          if (t is Texture2D t2d)
                                          {
                                              // Backup texture replace
                                              try
                                              {
                                                  // GetRawTextureData is not available in Unity 4.x so it can't be touched directly
                                                  new Action<Texture2D, Texture2D>((target, source) => { target.LoadRawTextureData(source.GetRawTextureData()); target.Apply(true); }).Invoke(t2d, newTex);
                                                  Change.Report($"(ContextMenu)::{_objName}.LoadImage(File.ReadAllBytes(\"{filename}\"))");
                                              }
                                              catch (Exception e2)
                                              {
                                                  RuntimeUnityEditorCore.Logger.Log(LogLevel.Error, e2);
                                              }
                                              finally
                                              {
                                                  UnityEngine.Object.Destroy(newTex);
                                              }
                                          }
                                      }
                                  }
                                  else if (o is Material m)
                                  {
                                      Change.WithUndo($"(ContextMenu)::{_objName}.mainTexture = Texture2D.LoadImage(File.ReadAllBytes(\"{filename}\"))",
                                                      m, newTex, (material, texture2D) => material.mainTexture = texture2D, oldValue: m.mainTexture);
                                  }
                                  else if (o is Image i && i.material != null)
                                  {
                                      Change.WithUndo($"(ContextMenu)::{_objName}.material.mainTexture = Texture2D.LoadImage(File.ReadAllBytes(\"{filename}\"))",
                                                      i.material, newTex, (material, texture2D) => material.mainTexture = texture2D, oldValue: i.material.mainTexture);
                                  }
                                  else if (o is RawImage ri)
                                  {
                                      Change.WithUndo($"(ContextMenu)::{_objName}.texture = Texture2D.LoadImage(File.ReadAllBytes(\"{filename}\"))",
                                                      ri, newTex, (rawImage, texture2D) => rawImage.texture = texture2D, oldValue: ri.texture);
                                  }
                                  else if (o is Renderer r)
                                  {
                                      if (r.sharedMaterial != null)
                                          Change.WithUndo($"(ContextMenu)::{_objName}.sharedMaterial.mainTexture = Texture2D.LoadImage(File.ReadAllBytes(\"{filename}\"))",
                                                          r.sharedMaterial, newTex, (material, texture2D) => material.mainTexture = texture2D, oldValue: r.sharedMaterial.mainTexture);
                                      else
                                          Change.WithUndo($"(ContextMenu)::{_objName}.material.mainTexture = Texture2D.LoadImage(File.ReadAllBytes(\"{filename}\"))",
                                                          r.material, newTex, (material, texture2D) => material.mainTexture = texture2D, oldValue: r.material.mainTexture);
                                  }
                              }),

                new MenuEntry("Export mesh to .obj", o => o is Renderer r && MeshExport.CanExport(r), o => MeshExport.ExportObj((Renderer)o, false, false)),
                new MenuEntry("Export mesh to .obj (Baked)", o => o is Renderer r && MeshExport.CanExport(r), o => MeshExport.ExportObj((Renderer)o, true, false)),
                new MenuEntry("Export mesh to .obj (World)", o => o is Renderer r && MeshExport.CanExport(r), o => MeshExport.ExportObj((Renderer)o, true, true)),

                new MenuEntry("Dump object to file...", o => o != null, o => Dumper.DumpToTempFile(o, _objName)),

                new MenuEntry("Destroy", o => o is UnityEngine.Object uo && uo, o => Change.Action("(ContextMenu)::UnityEngine.Object.Destroy({0})", o is Transform t ? t.gameObject : (UnityEngine.Object)o, UnityEngine.Object.Destroy)),

                new MenuEntry(),

                new MenuEntry("Find references in scene", o => o != null && ObjectViewWindow.Initialized && o.GetType().IsClass, o => ObjectTreeViewer.Instance.FindReferencesInScene(o)),

                new MenuEntry("Find member in dnSpy", o => DnSpyHelper.IsAvailable && _objMemberInfo != null, o => DnSpyHelper.OpenInDnSpy(_objMemberInfo)),
                new MenuEntry("Find member type in dnSpy", o => o != null && DnSpyHelper.IsAvailable, o => DnSpyHelper.OpenInDnSpy(o.GetType()))
            });

            _windowId = base.GetHashCode();
            Enabled = false;
            DisplayType = FeatureDisplayType.Hidden;
        }

        private void AddBreakpointControls(List<MenuEntry> menuContents)
        {
            menuContents.AddRange(AddGroup("call", (o, info) => info as MethodBase));
            menuContents.AddRange(AddGroup("getter", (o, info) => info is PropertyInfo pi ? pi.GetGetMethod(true) : null));
            menuContents.AddRange(AddGroup("setter", (o, info) => info is PropertyInfo pi ? pi.GetSetMethod(true) : null));
            menuContents.Add(new MenuEntry());
            return;

            IEnumerable<MenuEntry> AddGroup(string name, Func<object, MemberInfo, MethodBase> getMethod)
            {
                yield return new MenuEntry("Attach " + name + " breakpoint (this instance)", o =>
                {
                    if (o == null) return false;
                    var target = getMethod(o, _objMemberInfo);
                    return target != null && !Breakpoints.Breakpoints.IsAttached(target, o);
                }, o => Breakpoints.Breakpoints.AttachBreakpoint(getMethod(o, _objMemberInfo), o));
                yield return new MenuEntry("Detach " + name + " breakpoint (this instance)", o =>
                {
                    if (o == null) return false;
                    var target = getMethod(o, _objMemberInfo);
                    return target != null && Breakpoints.Breakpoints.IsAttached(target, o);
                }, o => Breakpoints.Breakpoints.DetachBreakpoint(getMethod(o, _objMemberInfo), o));

                yield return new MenuEntry("Attach " + name + " breakpoint (all instances)", o =>
                {
                    var target = getMethod(o, _objMemberInfo);
                    return target != null && !Breakpoints.Breakpoints.IsAttached(target, null);
                }, o => Breakpoints.Breakpoints.AttachBreakpoint(getMethod(o, _objMemberInfo), null));
                yield return new MenuEntry("Detach " + name + " breakpoint (all instances)", o =>
                {
                    var target = getMethod(o, _objMemberInfo);
                    return target != null && Breakpoints.Breakpoints.IsAttached(target, null);
                }, o => Breakpoints.Breakpoints.DetachBreakpoint(getMethod(o, _objMemberInfo), null));
            }
        }

        /// <summary>
        /// Show the context menu at current cursor position.
        /// </summary>
        /// <param name="obj">Instance of the object to show the menu for. Can be null if objEntry can be used to get it instead.</param>
        /// <param name="objEntry">Info about the member containing the displayed object.</param>

        public void Show(object obj, ICacheEntry objEntry)
        {
            if (objEntry == null) throw new ArgumentNullException(nameof(objEntry));

            if (obj == null && objEntry.CanEnterValue()) //todo this is a bit of a hack, maybe add a CanGetValue to ICacheEntry? This is conservative to avoid triggering side effects
                obj = objEntry.GetValue();

            string name;
            switch (objEntry)
            {
                case FieldCacheEntry f:
                    name = $"{f.FieldInfo.DeclaringType?.Name}.{f.FieldInfo.Name}";
                    break;
                case PropertyCacheEntry p:
                    name = $"{p.PropertyInfo.DeclaringType?.Name}.{p.PropertyInfo.Name}";
                    break;
                case null:
                    name = obj.GetType().FullDescription();
                    break;
                default:
                    name = objEntry.Name();
                    break;
            }

            var entryValid = objEntry.CanSetValue();
            Show(obj, objEntry.MemberInfo, name, entryValid ? objEntry.SetValue : (Action<object>)null, entryValid ? objEntry.GetValue : (Func<object>)null);
        }
        
        /// <summary>
        /// Show the context menu at current cursor position.
        /// </summary>
        /// <param name="obj">Object to show the menu for. Set to null to hide the menu.</param>
        public void Show(object obj)
        {
            Show(obj, null, obj?.GetType().FullDescription(), null, null);
        }

        /// <summary>
        /// Show the context menu at a specific screen position.
        /// </summary>
        /// <param name="obj">Object to show the menu for. Set to null to hide the menu (getObj also needs to be null or it will be used to get the obj).</param>
        /// <param name="memberInfo">MemberInfo of wherever the object came from. Can be null.</param>
        /// <param name="memberFullName">Name to show in the title bar and in change history.</param>
        /// <param name="setObj">Set value of the object</param>
        /// <param name="getObj">Get current value of the object</param>
        public void Show(object obj, MemberInfo memberInfo, string memberFullName, Action<object> setObj, Func<object> getObj)
        {
            var m = UnityInput.Current.mousePosition;
            var clickPoint = new Vector2(m.x, Screen.height - m.y);
#if IL2CPP
            _windowRect = new Rect(clickPoint, new Vector2(100, 100)); // This one doesn't get stripped it seems
#else
            _windowRect = new Rect(clickPoint.x, clickPoint.y, 100, 100); // Unity4 only has the 4xfloat constructor
#endif
            if (obj == null && getObj != null)
                obj = getObj();

            if (obj != null || memberInfo != null)
            {
                _obj = obj;
                _objMemberInfo = memberInfo ?? obj as MemberInfo;
                _objName = memberFullName ?? (_objMemberInfo != null ? $"{_objMemberInfo.DeclaringType?.Name}.{_objMemberInfo.Name}" : obj?.GetType().FullDescription());
                _setValue = setObj;
                _getValue = getObj;

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
        public void DrawContextButton(object obj, ICacheEntry objEntry)
        {
            if (obj != null && GUILayout.Button("...", IMGUIUtils.LayoutOptionsExpandWidthFalse))
                Show(obj, objEntry);
        }
        /// <summary>
        /// Draw a GUILayout button that opens the context menu when clicked. It's only shown if the object is not null.
        /// </summary>
        public void DrawContextButton(object obj, MemberInfo memberInfo, string memberFullName, Action<object> setObj, Func<object> getObj)
        {
            if (obj != null && GUILayout.Button("...", IMGUIUtils.LayoutOptionsExpandWidthFalse))
                Show(obj, memberInfo, memberFullName, setObj, getObj);
        }

        /// <inheritdoc />
        protected override void OnGUI()
        {
            // Make an invisible window in the back to reliably capture mouse clicks outside of the context menu.
            // If mouse clicks in a different window then the input will be eaten and context menu would go to the back and stay open instead of closing.
            // Can't use GUI.Box and such because it will always be behind GUI(Layout).Window no matter what you do.
            // Use a label skin with no content to make both the window and button invisible while spanning the entire screen without borders.
            var backdropWindowId = _windowId - 10;
            GUILayout.Window(backdropWindowId, new Rect(0, 0, Screen.width, Screen.height), (GUI.WindowFunction)DisableOnClickWindowFunc, string.Empty, GUI.skin.label);

            GUI.color = Color.white;

            IMGUIUtils.DrawSolidBox(_windowRect);

            _windowRect = GUILayout.Window(_windowId, _windowRect, (GUI.WindowFunction)DrawMenu, _objName);

            IMGUIUtils.EatInputInRect(_windowRect);

            // Ensure the context menu always stays on top while it's open. Can't use FocusWindow here because it locks up everything else.
            GUI.BringWindowToFront(backdropWindowId);
            GUI.BringWindowToFront(_windowId);

            if (_windowRect.xMax > Screen.width) _windowRect.x = Screen.width - _windowRect.width;
            if (_windowRect.yMax > Screen.height) _windowRect.y = Screen.height - _windowRect.height;
        }

        private void DisableOnClickWindowFunc(int id)
        {
            if (GUILayout.Button(GUIContent.none, GUI.skin.label, GUILayoutShim.ExpandWidth(true), GUILayoutShim.ExpandHeight(true))) Enabled = false;
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
