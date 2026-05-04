using System;
using System.Linq;
using System.Text.RegularExpressions;
using AssemblyArchitect.Editor.Commands;
using AssemblyArchitect.Editor.Infrastructure;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace AssemblyArchitect.Editor.Window.Dialogs
{
    internal sealed class CreateAsmDefPopup : EditorWindow
    {
        private static readonly Regex NameRegex = new Regex("^[A-Za-z_][A-Za-z0-9_.]*$", RegexOptions.Compiled);

        private AsmDefRepository repo;
        private CreateAsmDefCommand command;
        private Vector2 graphPosition;
        private string selectedNodeId;

        private ObjectField folderField;
        private TextField nameField;
        private Toggle autoReferenceToggle;
        private Button createButton;
        private Label validationLabel;

        public static void Show(
            Rect activatorRect,
            AsmDefRepository repo,
            CreateAsmDefCommand command,
            Vector2 graphPosition,
            string selectedNodeId)
        {
            var popup = CreateInstance<CreateAsmDefPopup>();
            popup.repo = repo;
            popup.command = command;
            popup.graphPosition = graphPosition;
            popup.selectedNodeId = selectedNodeId ?? string.Empty;
            popup.ShowAsDropDown(activatorRect, new Vector2(360f, 190f));
        }

        private void CreateGUI()
        {
            rootVisualElement.AddToClassList("aa-create-popup");
            rootVisualElement.style.paddingLeft = 10f;
            rootVisualElement.style.paddingRight = 10f;
            rootVisualElement.style.paddingTop = 10f;
            rootVisualElement.style.paddingBottom = 10f;

            folderField = new ObjectField("Folder")
            {
                objectType = typeof(DefaultAsset),
                allowSceneObjects = false,
                value = AssetDatabase.LoadAssetAtPath<DefaultAsset>(GetDefaultFolder()),
            };
            folderField.RegisterValueChangedCallback(_ => Validate());

            nameField = new TextField("Name");
            nameField.RegisterValueChangedCallback(_ => Validate());

            autoReferenceToggle = new Toggle("Auto-reference selected node")
            {
                value = false,
                tooltip = "Creates a reference from the currently selected assembly to the new one. Undo for created text assets is best-effort in Unity.",
            };
            autoReferenceToggle.SetEnabled(!string.IsNullOrEmpty(selectedNodeId));

            validationLabel = new Label();
            validationLabel.AddToClassList("aa-create-popup-validation");

            var buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.justifyContent = Justify.FlexEnd;

            createButton = new Button(Create) { text = "Create" };
            var cancelButton = new Button(Close) { text = "Cancel" };
            buttons.Add(createButton);
            buttons.Add(cancelButton);

            rootVisualElement.Add(folderField);
            rootVisualElement.Add(nameField);
            rootVisualElement.Add(autoReferenceToggle);
            rootVisualElement.Add(validationLabel);
            rootVisualElement.Add(buttons);
            Validate();
        }

        private void Create()
        {
            var folder = GetFolderPath();
            command.Execute(new CreateAsmDefCommand.Args
            {
                Folder = folder,
                Name = nameField.value,
                GraphPosition = graphPosition,
                AutoReferenceFromSourceId = autoReferenceToggle.value ? selectedNodeId : string.Empty,
            });
            Close();
        }

        private void Validate()
        {
            var message = GetValidationMessage();
            validationLabel.text = message;
            createButton?.SetEnabled(string.IsNullOrEmpty(message));
        }

        private string GetValidationMessage()
        {
            var folder = GetFolderPath();
            if (string.IsNullOrEmpty(folder) || !folder.StartsWith("Assets", StringComparison.Ordinal) || !AssetDatabase.IsValidFolder(folder))
                return "Select a folder inside Assets.";

            var name = nameField?.value ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
                return "Name is required.";
            if (!NameRegex.IsMatch(name))
                return "Name must be a valid assembly identifier.";
            if (repo.LoadAll().Any(item => string.Equals(item.Name, name, StringComparison.Ordinal)))
                return "Assembly name is already in use.";

            return string.Empty;
        }

        private string GetFolderPath()
        {
            if (folderField?.value == null)
                return "Assets";

            var path = AssetDatabase.GetAssetPath(folderField.value);
            return AssetDatabase.IsValidFolder(path) ? path : "Assets";
        }

        private static string GetDefaultFolder()
        {
            var active = Selection.activeObject != null ? AssetDatabase.GetAssetPath(Selection.activeObject) : string.Empty;
            if (string.IsNullOrEmpty(active))
                return "Assets";

            if (!AssetDatabase.IsValidFolder(active))
                active = System.IO.Path.GetDirectoryName(active)?.Replace('\\', '/') ?? "Assets";

            return active.StartsWith("Assets", StringComparison.Ordinal) && AssetDatabase.IsValidFolder(active)
                ? active
                : "Assets";
        }
    }
}
