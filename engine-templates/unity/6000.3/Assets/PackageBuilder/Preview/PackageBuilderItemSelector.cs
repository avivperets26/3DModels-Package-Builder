using System;
using PackageBuilder.PreviewContract;
using UnityEngine;

namespace PackageBuilder.Preview
{
    /// <summary>Applies shared selection transitions only to scene instances; never writes prefab assets.</summary>
    public sealed class PackageBuilderItemSelector : MonoBehaviour
    {
        public const string PreviousControl = "item-previous";
        public const string NextControl = "item-next";
        public const string SelectControl = "item-select";
        public const string AllControl = "item-all";
        [SerializeField] private GameObject[] items = Array.Empty<GameObject>();
        [SerializeField] private int selectedIndex = -1;
        private Vector2 scroll;
        private bool expanded;

        /// <summary>Gets the snapshotted declaration size, including a safe empty state.</summary>
        public int Count => items.Length;
        /// <summary>Gets the zero-based selection, or -1 for all/empty.</summary>
        public int SelectedIndex => selectedIndex;
        /// <summary>Gets the current scene item name or explicit overview/empty label.</summary>
        public string CurrentName => Count == 0 ? "No items" : selectedIndex < 0 ? "All items" : items[selectedIndex].name;

        /// <summary>Snapshots unique scene references in manifest order and restores the approved overview default.</summary>
        public void Configure(GameObject[] orderedItems)
        {
            if (orderedItems == null) { throw new ArgumentNullException(nameof(orderedItems)); }
            for (int i = 0; i < orderedItems.Length; i++)
            {
                if (orderedItems[i] == null || !orderedItems[i].scene.IsValid() ||
                    Array.IndexOf(orderedItems, orderedItems[i]) != i)
                { throw new ArgumentException("Selection requires unique scene instances.", nameof(orderedItems)); }
            }
            items = (GameObject[])orderedItems.Clone();
            selectedIndex = PreviewSelectionPolicy.InitialIndex(Count, PreviewSelectionPolicy.InitiallyShowAll);
            ApplyVisibility();
        }

        /// <summary>Selects a declaration index, rejecting invalid requests without changing visibility.</summary>
        public bool Select(int index)
        {
            if (index < 0 || index >= Count) { return false; }
            selectedIndex = index;
            ApplyVisibility();
            return true;
        }

        /// <summary>Selects the previous declaration with the shared wrap behavior.</summary>
        public void Previous() => Move(-1);
        /// <summary>Selects the next declaration with the shared wrap behavior.</summary>
        public void Next() => Move(1);
        /// <summary>Restores all scene instances without writing to prefab assets.</summary>
        public void ShowAll() { selectedIndex = -1; ApplyVisibility(); }

        private void Move(int direction)
        {
            selectedIndex = PreviewSelectionPolicy.Move(Count, selectedIndex, direction, PreviewSelectionPolicy.WrapPreviousNext);
            ApplyVisibility();
        }

        private void OnEnable() => ApplyVisibility();

        private void ApplyVisibility()
        {
            for (int i = 0; i < Count; i++)
            {
                if (items[i] != null) { items[i].SetActive(selectedIndex < 0 || selectedIndex == i); }
            }
        }

        /// <summary>Routes focused keyboard controls through the same operations as pointer controls.</summary>
        public bool HandleKeyboard(Event input, string focused)
        {
            bool activate = input.keyCode == KeyCode.Return || input.keyCode == KeyCode.KeypadEnter || input.keyCode == KeyCode.Space;
            if (activate && focused == PreviousControl) { Previous(); }
            else if (activate && focused == NextControl) { Next(); }
            else if (activate && focused == AllControl) { ShowAll(); }
            else if (focused == SelectControl && input.keyCode == KeyCode.LeftArrow) { Previous(); }
            else if (focused == SelectControl && input.keyCode == KeyCode.RightArrow) { Next(); }
            else if (focused == SelectControl && input.keyCode == KeyCode.Home && Count > 0) { Select(0); }
            else if (focused == SelectControl && input.keyCode == KeyCode.End && Count > 0) { Select(Count - 1); }
            else if (activate && focused == SelectControl) { expanded = !expanded; }
            else { return false; }
            input.Use();
            return true;
        }

        /// <summary>Draws a bounded, scrollable direct picker with explicit current name, index and focus feedback.</summary>
        public void Draw(Rect panel)
        {
            GUI.Label(new Rect(panel.x, panel.y, 232f, 22f), selectedIndex < 0 ? CurrentName + " (" + Count + ")" : CurrentName + " (" + (selectedIndex + 1) + " / " + Count + ")");
            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && Count > 0;
            if (Button(new Rect(panel.x, panel.y + 24f, 112f, 28f), PreviousControl, "Previous")) { Previous(); }
            if (Button(new Rect(panel.x + 120f, panel.y + 24f, 112f, 28f), NextControl, "Next")) { Next(); }
            if (Button(new Rect(panel.x, panel.y + 58f, 112f, 28f), SelectControl, "Item")) { expanded = !expanded; }
            if (Button(new Rect(panel.x + 120f, panel.y + 58f, 112f, 28f), AllControl, "All items")) { ShowAll(); }
            if (expanded)
            {
                Rect viewport = new Rect(panel.x, panel.y + 92f, 232f, 92f);
                scroll = GUI.BeginScrollView(viewport, scroll, new Rect(0f, 0f, 208f, Count * 28f));
                for (int i = 0; i < Count; i++)
                {
                    if (GUI.Button(new Rect(0f, i * 28f, 208f, 26f), items[i].name)) { Select(i); expanded = false; }
                }
                GUI.EndScrollView();
            }
            GUI.enabled = previousEnabled;
        }

        private static bool Button(Rect rect, string control, string label)
        {
            GUI.SetNextControlName(control);
            bool clicked = GUI.Button(rect, new GUIContent(label, "Select preview item; Tab to focus, Enter or Space to activate. Item: arrows, Home, End."));
            if (GUI.GetNameOfFocusedControl() == control)
            {
                Color before = GUI.color;
                GUI.color = new Color(0.35f, 0.75f, 1f, 1f);
                GUI.DrawTexture(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), Texture2D.whiteTexture);
                GUI.color = before;
            }
            return clicked;
        }
    }
}
