// Popup list created by Eric Haines
// ComboBox Extended by Hyungseok Seo.(Jerry) sdragoon@nate.com
// this oop version of ComboBox is refactored by zhujiangbo jumbozhu@gmail.com
// Modified by MarC0 / ManlyMarco

using System;
using UnityEngine;

namespace RuntimeUnityEditor.Core.Utils
{
    /// <summary>
    /// Dropdown control for use in GUILayout areas and windows. Keep the instance and call Show on it to draw it inside OnGUI.
    /// Remember to call `DrawDropdownIfOpen` at the very end of the OnGUI area/window to actually display the dropdown list if it's open.
    /// Only one dropdown list can be open globally. If a new dropdown is opened, all others are closed without changing the selected index.
    /// </summary>
    public class ImguiComboBox
    {
        private static ImguiComboBox _visibleBox;

        private Vector2 _scrollPosition = Vector2.zero;
        private GUIContent[] _cachedContents;

        private int _newIndex;
        private bool _wasChanged;

        /// <summary>
        /// Show a button that when clicked opens a dropdown list. Returns new index if user selected a different option, or the old index.
        /// Warning: The list itself is not drawn here, you have to call DrawDropdownIfOpen at the end of your GUILayout area/window.
        /// </summary>
        /// <param name="selectedIndex">Currently selected item from the content list. It will be shown on the button.</param>
        /// <param name="listContent">All items shown in the dropdown list when it is open.</param>
        /// <param name="windowYmax">How low the dropdown can reach. Used to prevent the dropdown from extending off-screen. Set it to the GUILayout.Window rect yMax value.</param>
        /// <param name="listStyle">Optional style of list buttons. By default the standard button style is used.</param>
        public int Show(int selectedIndex, GUIContent[] listContent, int windowYmax = int.MaxValue, GUIStyle listStyle = null)
        {
            var content = selectedIndex >= 0 && selectedIndex < listContent.Length ? listContent[selectedIndex] : null;

            Show(content, () => listContent, (i) =>
            {
                _newIndex = i;
                _wasChanged = true;
            }, windowYmax, listStyle);

            if (_wasChanged)
            {
                GUI.changed = true;
                _wasChanged = false;
                return _newIndex;
            }

            return selectedIndex;
        }

        /// <summary>
        /// Show a button that when clicked opens a dropdown list.
        /// Warning: The list itself is not drawn here, you have to call DrawDropdownIfOpen at the end of your GUILayout area/window.
        /// </summary>
        /// <param name="selectedContent">Content shown on the button. It will also be selected in the dropdown list if it exists.</param>
        /// <param name="getListContent">Called once to get the list contents when the button is clicked. It should return contents of the dropdown list, including <paramref name="selectedContent"/>.</param>
        /// <param name="onIndexChanged">Called when user selects a new item</param>
        /// <param name="windowYmax"></param>
        /// <param name="listStyle"></param>
        public void Show(GUIContent selectedContent, Func<GUIContent[]> getListContent, Action<int> onIndexChanged, int windowYmax = int.MaxValue, GUIStyle listStyle = null)
        {
            if (getListContent == null) throw new ArgumentNullException(nameof(getListContent));
            if (onIndexChanged == null) throw new ArgumentNullException(nameof(onIndexChanged));

            if (listStyle == null) listStyle = GUI.skin.button;
            var content = selectedContent ?? GUIContent.none;

            var rect = GUILayoutUtility.GetRect(content, listStyle, GUILayout.ExpandWidth(true));

            var originalChanged = GUI.changed;

            var done = false;
            var controlId = GUIUtility.GetControlID(FocusType.Passive);

            var currentMousePosition = Vector2.zero;
            if (Event.current.GetTypeForControl(controlId) == EventType.MouseUp)
            {
                if (_visibleBox == this)
                {
                    done = true;
                    currentMousePosition = Event.current.mousePosition;
                }
            }

            if (GUI.Button(rect, content, listStyle))
            {
                _visibleBox = this;
                _cachedContents = getListContent();
            }

            if (_visibleBox == this)
            {
                GUI.enabled = false;
                GUI.color = new Color(1, 1, 1, 2);

                _dropdownLocation = GUIUtility.GUIToScreenPoint(new Vector2(rect.x, rect.y + listStyle.CalcHeight(_cachedContents[0], 1.0f)));
                var size = new Vector2(rect.width, listStyle.CalcHeight(_cachedContents[0], 1.0f) * _cachedContents.Length);

                _innerRect = new Rect(0, 0, size.x, size.y);

                _outerRectScreen = new Rect(_dropdownLocation.x, _dropdownLocation.y, size.x, size.y);
                if (_outerRectScreen.yMax > windowYmax)
                {
                    _outerRectScreen.height = windowYmax - _outerRectScreen.y;
                    _outerRectScreen.width += 20;
                }

                if (selectedContent == null)
                    _selectedItem = -1;
                else
                    _selectedItem = Array.FindIndex(_cachedContents, c => c.text == selectedContent.text && c.image == selectedContent.image);

                _onIndexChanged = onIndexChanged;

                if (currentMousePosition != Vector2.zero && _outerRectScreen.Contains(GUIUtility.GUIToScreenPoint(currentMousePosition)))
                    done = false;
            }

            if (done)
                _visibleBox = null;

            GUI.changed = originalChanged;
        }

        private Action<int> _onIndexChanged;
        private int _selectedItem;
        private Vector2 _dropdownLocation;
        private Rect _outerRectScreen;
        private Rect _innerRect;

        private void DrawDropdownList()
        {
            GUI.enabled = true;

            var scrpos = GUIUtility.ScreenToGUIPoint(_dropdownLocation);
            var outerRectLocal = new Rect(scrpos.x, scrpos.y, _outerRectScreen.width, _outerRectScreen.height);

            IMGUIUtils.DrawSolidBox(outerRectLocal);

            _scrollPosition = GUI.BeginScrollView(outerRectLocal, _scrollPosition, _innerRect, false, false);
            {
                var newSelectedItemIndex = GUI.SelectionGrid(_innerRect, _selectedItem, _cachedContents, 1);
                if (newSelectedItemIndex != _selectedItem)
                {
                    _onIndexChanged(newSelectedItemIndex);
                    _visibleBox = null;
                }
            }
            GUI.EndScrollView(true);
        }

        /// <summary>
        /// Draws the dropdown list on top of all other window controls if it is open.
        /// This should always be called at the very end of area/window that `Show` was called in.
        /// Returns true if the dropdown list was opened and subsequently drawn.
        /// </summary>
        public bool DrawDropdownIfOpen()
        {
            if (_visibleBox == this)
            {
                _visibleBox.DrawDropdownList();
                return true;
            }

            return false;
        }
    }
}
