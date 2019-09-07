using RuntimeUnityEditor.Core.Utils;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RuntimeUnityEditor.Core.UI
{
    public static class InterfaceMaker
    {
        public static void EatInputInRect(Rect eatRect)
        {
            if (eatRect.Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y)))
                Input.ResetInputAxes();
        }

        public static GUISkin CreateSkin()
        {
            var newSkin = Object.Instantiate(GUI.skin);

            // Load the custom skin from resources
            var texData = ResourceUtils.GetEmbeddedResource("guisharp-box.png");
            var boxTex = UnityFeatureHelper.LoadTexture(texData);
            newSkin.box.onNormal.background = null;
            newSkin.box.normal.background = boxTex;
            newSkin.box.normal.textColor = Color.white;

            texData = ResourceUtils.GetEmbeddedResource("guisharp-window.png");
            var winTex = UnityFeatureHelper.LoadTexture(texData);
            newSkin.window.onNormal.background = null;
            newSkin.window.normal.background = winTex;
            newSkin.window.padding = new RectOffset(6, 6, 22, 6);
            newSkin.window.border = new RectOffset(10, 10, 20, 10);
            newSkin.window.normal.textColor = Color.white;

            newSkin.button.padding = new RectOffset(4, 4, 3, 3);
            newSkin.button.normal.textColor = Color.white;

            newSkin.textField.normal.textColor = Color.white;

            newSkin.label.normal.textColor = Color.white;

            return newSkin;
        }
    }
}
