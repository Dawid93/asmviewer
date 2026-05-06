using System;
using System.Text.RegularExpressions;
using AssemblyArchitect.Editor.Commands;
using AssemblyArchitect.Editor.Infrastructure;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace AssemblyArchitect.Editor.Window.Dialogs
{
    /// <summary>
    /// Small drop-down popup for creating a new <c>.asmdef</c> file.
    /// Open via <see cref="Show"/>.
    /// </summary>
    internal sealed class CreateAsmDefPopup : EditorWindow
    {
        private static readonly Regex NameRegex =
            new Regex(@"^[A-Za-z_][A-Za-z0-9_.]*$", RegexOptions.Compiled);

        // ── Init state ────────────────────────────────────────────────────────

        private CreateAsmDefCommand             _command;
        private AsmDefRepository                _repo;
        private Vector2                         _graphPosition;
        private string                          _selectedSourceId;
        private Action<string, Vector2>         _onPositionReady;

        // ── UI elements ───────────────────────────────────────────────────────

        private TextField  _nameField;
        private ObjectField _folderField;
        private Toggle     _autoRefToggle;
        private Button     _createButton;
        private Label      _errorLabel;

        // ── Factory ───────────────────────────────────────────────────────────

        /// <summary>Shows the popup as a drop-down anchored at <paramref name="dropdownRect"/>.</summary>
        public static void Show(
            Rect                    dropdownRect,
            CreateAsmDefCommand     command,
            AsmDefRepository        repo,
            Vector2                 graphPosition,
            string                  selectedSourceId,
            Action<string, Vector2> onPositionReady)
        {
            var popup               = CreateInstance<CreateAsmDefPopup>();
            popup._command          = command;
            popup._repo             = repo;
            popup._graphPosition    = graphPosition;
            popup._selectedSourceId = selectedSourceId;
            popup._onPositionReady  = onPositionReady;
            popup.ShowAsDropDown(dropdownRect, new Vector2(360f, 170f));
        }

        // ── GUI ───────────────────────────────────────────────────────────────

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingTop    = 8;
            root.style.paddingBottom = 8;
            root.style.paddingLeft   = 8;
            root.style.paddingRight  = 8;

            // Folder picker
            _folderField = new ObjectField("Folder") { objectType = typeof(DefaultAsset) };
            _folderField.value = GetDefaultFolderAsset();
            root.Add(_folderField);

            // Spacer
            root.Add(new VisualElement { style = { height = 4 } });

            // Name field
            _nameField = new TextField("Name") { value = "MyCompany.MyAssembly" };
            _nameField.RegisterValueChangedCallback(_ => Validate());
            root.Add(_nameField);

            // Error label
            _errorLabel = new Label
            {
                style =
                {
                    color       = new StyleColor(new Color(1f, 0.4f, 0.4f)),
                    marginLeft  = 2,
                    fontSize    = 10,
                    minHeight   = 14,
                    whiteSpace  = WhiteSpace.Normal,
                }
            };
            root.Add(_errorLabel);

            // Auto-reference toggle
            _autoRefToggle = new Toggle("Auto-reference selected node")
            {
                tooltip = "When checked, adds a reference from the currently-selected node to the new assembly.",
                value   = false,
            };
            _autoRefToggle.SetEnabled(!string.IsNullOrEmpty(_selectedSourceId));
            root.Add(_autoRefToggle);

            // Buttons row
            var buttons = new VisualElement
            {
                style =
                {
                    flexDirection  = FlexDirection.Row,
                    justifyContent = Justify.FlexEnd,
                    marginTop      = 8,
                }
            };

            var cancelBtn = new Button(Close) { text = "Cancel", style = { marginRight = 4 } };
            _createButton = new Button(OnCreate) { text = "Create" };

            buttons.Add(cancelBtn);
            buttons.Add(_createButton);
            root.Add(buttons);

            Validate();
        }

        // ── Actions ───────────────────────────────────────────────────────────

        private void OnCreate()
        {
            var args = new CreateAsmDefCommand.Args
            {
                Folder                    = GetFolderPath(),
                Name                      = _nameField.value.Trim(),
                GraphPosition             = _graphPosition,
                AutoReferenceFromSourceId = _autoRefToggle.value ? _selectedSourceId : null,
            };
            _command.Execute(args, _onPositionReady);
            Close();
        }

        // ── Validation ────────────────────────────────────────────────────────

        private void Validate()
        {
            var name = _nameField?.value?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(name))
            {
                SetError("Name is required.");
                return;
            }

            if (!NameRegex.IsMatch(name))
            {
                SetError("Name must match ^[A-Za-z_][A-Za-z0-9_.]*$");
                return;
            }

            if (_repo?.FindByName(name) != null)
            {
                SetError($"An assembly named '{name}' already exists.");
                return;
            }

            SetError(null);
        }

        private void SetError(string msg)
        {
            if (_errorLabel != null) _errorLabel.text = msg ?? string.Empty;
            _createButton?.SetEnabled(string.IsNullOrEmpty(msg));
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private string GetFolderPath()
        {
            var asset = _folderField?.value as DefaultAsset;
            if (asset != null)
            {
                var path = AssetDatabase.GetAssetPath(asset);
                if (!string.IsNullOrEmpty(path) && path.StartsWith("Assets", StringComparison.Ordinal))
                    return path;
            }

            // Fallback: selected folder in Project window
            var selected = Selection.activeObject;
            if (selected != null)
            {
                var selPath = AssetDatabase.GetAssetPath(selected);
                if (!string.IsNullOrEmpty(selPath))
                {
                    if (System.IO.Directory.Exists(selPath))
                        return selPath;
                    var dir = System.IO.Path.GetDirectoryName(selPath);
                    if (!string.IsNullOrEmpty(dir))
                        return dir.Replace('\\', '/');
                }
            }

            return "Assets";
        }

        private static DefaultAsset GetDefaultFolderAsset()
        {
            var selected = Selection.activeObject;
            if (selected != null)
            {
                var selPath = AssetDatabase.GetAssetPath(selected);
                if (!string.IsNullOrEmpty(selPath) && System.IO.Directory.Exists(selPath))
                    return selected as DefaultAsset;
            }
            return AssetDatabase.LoadAssetAtPath<DefaultAsset>("Assets");
        }
    }
}
