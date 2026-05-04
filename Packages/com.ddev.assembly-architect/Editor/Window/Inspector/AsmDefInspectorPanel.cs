using System;
using System.Collections.Generic;
using System.Linq;
using AssemblyArchitect.Editor.Commands;
using AssemblyArchitect.Editor.Core;
using AssemblyArchitect.Editor.Graph;
using AssemblyArchitect.Editor.Infrastructure;
using UnityEditor;
using UnityEngine.UIElements;

namespace AssemblyArchitect.Editor.Window.Inspector
{
    internal sealed class AsmDefInspectorPanel : VisualElement
    {
        private const string UxmlPath = "Packages/com.ddev.assembly-architect/Editor/UI/AsmDefInspectorPanel.uxml";
        private const string UssPath = "Packages/com.ddev.assembly-architect/Editor/UI/AsmDefInspectorPanel.uss";
        private static readonly string[] PlatformOptions =
        {
            "Editor",
            "WindowsStandalone64",
            "Android",
            "iOS",
            "LinuxStandalone64",
            "macOSStandalone",
            "WebGL",
            "WSA",
            "tvOS",
            "PS4",
            "PS5",
            "XboxOne",
            "Switch",
        };

        private readonly AsmDefRepository repo;
        private readonly AsmDefWriter writer;
        private readonly Debouncer textDebouncer;

        private string currentId = string.Empty;
        private AsmDefData currentData;
        private bool binding;
        private Action pendingTextEdit;

        private Label emptyLabel;
        private VisualElement content;
        private Label nameLabel;
        private Label originBadge;
        private Label readOnlyNote;
        private TextField rootNamespaceField;
        private Toggle allowUnsafeToggle;
        private Toggle autoReferencedToggle;
        private Toggle overrideReferencesToggle;
        private Toggle noEngineReferencesToggle;
        private VisualElement includeList;
        private VisualElement excludeList;
        private VisualElement defineList;
        private VisualElement versionDefineList;
        private VisualElement precompiledList;
        private VisualElement referencesList;

        public AsmDefInspectorPanel(AsmDefRepository repo, AsmDefWriter writer)
        {
            this.repo = repo ?? throw new ArgumentNullException(nameof(repo));
            this.writer = writer ?? throw new ArgumentNullException(nameof(writer));
            textDebouncer = new Debouncer(300, FlushTextEdit);

            AddToClassList("aa-inspector-panel");
            CloneUxml();
            ApplyStyles();
            CacheElements();
            BuildStaticSections();

            this.repo.Changed += OnRepositoryChanged;
            Clear();
        }

        public void ShowFor(string asmDefId)
        {
            if (string.IsNullOrEmpty(asmDefId))
            {
                Clear();
                return;
            }

            currentId = asmDefId;
            Refresh();
        }

        public new void Clear()
        {
            currentId = string.Empty;
            currentData = null;
            emptyLabel.style.display = DisplayStyle.Flex;
            content.style.display = DisplayStyle.None;
        }

        private void CloneUxml()
        {
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            if (visualTree != null)
                visualTree.CloneTree(this);
        }

        private void ApplyStyles()
        {
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
            if (styleSheet != null)
                styleSheets.Add(styleSheet);
        }

        private void CacheElements()
        {
            emptyLabel = this.Q<Label>("empty-label") ?? new Label("No assembly selected");
            content = this.Q<VisualElement>("content") ?? new ScrollView();
            nameLabel = this.Q<Label>("name-label") ?? new Label();
            originBadge = this.Q<Label>("origin-badge") ?? new Label();
            readOnlyNote = this.Q<Label>("read-only-note") ?? new Label();
        }

        private void BuildStaticSections()
        {
            content.Clear();

            var header = new VisualElement();
            header.AddToClassList("aa-inspector-header");
            nameLabel.AddToClassList("aa-inspector-title");
            originBadge.AddToClassList("aa-origin-badge");
            readOnlyNote.AddToClassList("aa-readonly-note");
            var pingButton = new Button(PingCurrent) { text = "Ping" };
            var openButton = new Button(OpenCurrent) { text = "Open" };
            header.Add(nameLabel);
            header.Add(originBadge);
            header.Add(readOnlyNote);
            header.Add(pingButton);
            header.Add(openButton);
            content.Add(header);

            var general = new Foldout { text = "General", value = true };
            rootNamespaceField = new TextField("Root namespace");
            RegisterDebouncedText(rootNamespaceField, value => ApplyEdit(data => data.RootNamespace = value, "Edit Root Namespace"));
            allowUnsafeToggle = CreateToggle("Allow unsafe code", data => data.AllowUnsafeCode, (data, value) => data.AllowUnsafeCode = value, "Toggle Unsafe Code");
            autoReferencedToggle = CreateToggle("Auto referenced", data => data.AutoReferenced, (data, value) => data.AutoReferenced = value, "Toggle Auto Referenced");
            overrideReferencesToggle = CreateToggle("Override references", data => data.OverrideReferences, (data, value) => data.OverrideReferences = value, "Toggle Override References");
            overrideReferencesToggle.RegisterValueChangedCallback(_ => precompiledList?.SetEnabled(IsEditable() && overrideReferencesToggle.value));
            noEngineReferencesToggle = CreateToggle("No engine references", data => data.NoEngineReferences, (data, value) => data.NoEngineReferences = value, "Toggle No Engine References");
            general.Add(rootNamespaceField);
            general.Add(allowUnsafeToggle);
            general.Add(autoReferencedToggle);
            general.Add(overrideReferencesToggle);
            general.Add(noEngineReferencesToggle);
            content.Add(general);

            var platforms = new Foldout { text = "Platforms" };
            includeList = AddListSection(platforms, "Include platforms");
            excludeList = AddListSection(platforms, "Exclude platforms");
            content.Add(platforms);

            var defines = new Foldout { text = "Define Constraints" };
            defineList = AddListSection(defines, "Defines");
            content.Add(defines);

            var versionDefines = new Foldout { text = "Version Defines" };
            versionDefineList = new VisualElement();
            versionDefines.Add(versionDefineList);
            content.Add(versionDefines);

            var precompiled = new Foldout { text = "Precompiled References" };
            precompiledList = AddListSection(precompiled, "References");
            content.Add(precompiled);

            var references = new Foldout { text = "References" };
            referencesList = new VisualElement();
            references.Add(referencesList);
            content.Add(references);
        }

        private Toggle CreateToggle(
            string label,
            Func<AsmDefData, bool> get,
            Action<AsmDefData, bool> set,
            string undoLabel)
        {
            var toggle = new Toggle(label);
            toggle.RegisterValueChangedCallback(evt =>
            {
                if (binding) return;
                ApplyEdit(data => set(data, evt.newValue), undoLabel);
            });
            return toggle;
        }

        private VisualElement AddListSection(VisualElement parent, string label)
        {
            var title = new Label(label);
            title.AddToClassList("aa-list-title");
            var list = new VisualElement();
            parent.Add(title);
            parent.Add(list);
            return list;
        }

        private void Refresh()
        {
            currentData = repo.LoadAll().FirstOrDefault(item => string.Equals(item.StableId, currentId, StringComparison.Ordinal));
            if (currentData == null)
            {
                Clear();
                return;
            }

            binding = true;
            emptyLabel.style.display = DisplayStyle.None;
            content.style.display = DisplayStyle.Flex;

            nameLabel.text = currentData.Name;
            originBadge.text = GetOriginLabel(currentData.Origin);
            readOnlyNote.text = IsEditable() ? string.Empty : "Read-only (registry package)";
            readOnlyNote.style.display = IsEditable() ? DisplayStyle.None : DisplayStyle.Flex;

            rootNamespaceField.SetValueWithoutNotify(currentData.RootNamespace ?? string.Empty);
            allowUnsafeToggle.SetValueWithoutNotify(currentData.AllowUnsafeCode);
            autoReferencedToggle.SetValueWithoutNotify(currentData.AutoReferenced);
            overrideReferencesToggle.SetValueWithoutNotify(currentData.OverrideReferences);
            noEngineReferencesToggle.SetValueWithoutNotify(currentData.NoEngineReferences);

            RebuildPlatformList(includeList, currentData.IncludePlatforms, true);
            RebuildPlatformList(excludeList, currentData.ExcludePlatforms, false);
            RebuildStringList(defineList, currentData.DefineConstraints, value => ApplyEdit(data => data.DefineConstraints = value, "Edit Define Constraints"));
            RebuildStringList(precompiledList, currentData.PrecompiledReferences, value => ApplyEdit(data => data.PrecompiledReferences = value, "Edit Precompiled References"));
            precompiledList.SetEnabled(IsEditable() && currentData.OverrideReferences);
            RebuildVersionDefines();
            RebuildReferences();

            SetEditControlsEnabled(IsEditable());
            binding = false;
        }

        private void SetEditControlsEnabled(bool enabled)
        {
            rootNamespaceField.SetEnabled(enabled);
            allowUnsafeToggle.SetEnabled(enabled);
            autoReferencedToggle.SetEnabled(enabled);
            overrideReferencesToggle.SetEnabled(enabled);
            noEngineReferencesToggle.SetEnabled(enabled);
            includeList.SetEnabled(enabled);
            excludeList.SetEnabled(enabled);
            defineList.SetEnabled(enabled);
            versionDefineList.SetEnabled(enabled);
            precompiledList.SetEnabled(enabled && currentData.OverrideReferences);
        }

        private void RebuildPlatformList(VisualElement list, string[] values, bool include)
        {
            list.Clear();
            var current = new List<string>(values ?? Array.Empty<string>());
            for (var i = 0; i < current.Count; i++)
            {
                var index = i;
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                var popup = new PopupField<string>(PlatformOptions.ToList(), current[index]);
                popup.RegisterValueChangedCallback(evt =>
                {
                    var copy = current.ToArray();
                    copy[index] = evt.newValue;
                    ApplyPlatformEdit(copy, include);
                });
                var remove = new Button(() =>
                {
                    var copy = current.ToList();
                    copy.RemoveAt(index);
                    ApplyPlatformEdit(copy.ToArray(), include);
                }) { text = "-" };
                row.Add(popup);
                row.Add(remove);
                list.Add(row);
            }

            list.Add(new Button(() =>
            {
                var copy = current.ToList();
                copy.Add("Editor");
                ApplyPlatformEdit(copy.Distinct().ToArray(), include);
            }) { text = include ? "Add include platform" : "Add exclude platform" });
        }

        private void ApplyPlatformEdit(string[] values, bool include)
        {
            ApplyEdit(data =>
            {
                if (include)
                {
                    data.IncludePlatforms = values;
                    if (values.Length > 0)
                        data.ExcludePlatforms = Array.Empty<string>();
                }
                else
                {
                    data.ExcludePlatforms = values;
                    if (values.Length > 0)
                        data.IncludePlatforms = Array.Empty<string>();
                }
            }, "Edit Platforms");
        }

        private void RebuildStringList(VisualElement list, string[] values, Action<string[]> apply)
        {
            list.Clear();
            var current = new List<string>(values ?? Array.Empty<string>());
            for (var i = 0; i < current.Count; i++)
            {
                var index = i;
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                var field = new TextField { value = current[index] };
                RegisterDebouncedText(field, value =>
                {
                    var copy = current.ToArray();
                    copy[index] = value;
                    apply(copy);
                });
                var remove = new Button(() =>
                {
                    var copy = current.ToList();
                    copy.RemoveAt(index);
                    apply(copy.ToArray());
                }) { text = "-" };
                row.Add(field);
                row.Add(remove);
                list.Add(row);
            }

            list.Add(new Button(() =>
            {
                var copy = current.ToList();
                copy.Add(string.Empty);
                apply(copy.ToArray());
            }) { text = "Add" });
        }

        private void RebuildVersionDefines()
        {
            versionDefineList.Clear();
            var current = new List<VersionDefine>(currentData.VersionDefines ?? Array.Empty<VersionDefine>());
            for (var i = 0; i < current.Count; i++)
            {
                var index = i;
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                AddVersionField(row, current, index, "Name", (define, value) => define.Name = value);
                AddVersionField(row, current, index, "Expression", (define, value) => define.Expression = value);
                AddVersionField(row, current, index, "Define", (define, value) => define.Define = value);
                row.Add(new Button(() =>
                {
                    var copy = current.ToList();
                    copy.RemoveAt(index);
                    ApplyEdit(data => data.VersionDefines = copy.ToArray(), "Edit Version Defines");
                }) { text = "-" });
                versionDefineList.Add(row);
            }

            versionDefineList.Add(new Button(() =>
            {
                var copy = current.ToList();
                copy.Add(new VersionDefine());
                ApplyEdit(data => data.VersionDefines = copy.ToArray(), "Edit Version Defines");
            }) { text = "Add version define" });
        }

        private void AddVersionField(
            VisualElement row,
            List<VersionDefine> current,
            int index,
            string label,
            Action<VersionDefine, string> set)
        {
            var value = current[index];
            var field = new TextField(label)
            {
                value = label == "Name" ? value.Name : label == "Expression" ? value.Expression : value.Define,
            };
            RegisterDebouncedText(field, text =>
            {
                var copy = current.ToArray();
                var item = copy[index];
                set(item, text);
                copy[index] = item;
                ApplyEdit(data => data.VersionDefines = copy, "Edit Version Defines");
            });
            row.Add(field);
        }

        private void RebuildReferences()
        {
            referencesList.Clear();
            foreach (var reference in currentData.References ?? Array.Empty<string>())
                referencesList.Add(new Label(reference));
        }

        private void RegisterDebouncedText(TextField field, Action<string> apply)
        {
            field.RegisterValueChangedCallback(evt =>
            {
                if (binding) return;
                var value = evt.newValue;
                pendingTextEdit = () => apply(value);
                textDebouncer.Bump();
            });
        }

        private void FlushTextEdit()
        {
            pendingTextEdit?.Invoke();
            pendingTextEdit = null;
        }

        private void ApplyEdit(Action<AsmDefData> mutate, string undoLabel)
        {
            if (currentData == null || !IsEditable())
                return;

            var clone = currentData.Clone();
            mutate(clone);
            writer.Save(clone, undoLabel);
            repo.NotifyChanged();
        }

        private bool IsEditable()
        {
            return currentData != null && !AddReferenceCommand.IsReadOnly(currentData);
        }

        private void OnRepositoryChanged()
        {
            if (!string.IsNullOrEmpty(currentId))
                Refresh();
        }

        private void PingCurrent()
        {
            var asset = currentData != null ? AssetDatabase.LoadMainAssetAtPath(currentData.AssetPath) : null;
            if (asset != null)
                EditorGUIUtility.PingObject(asset);
        }

        private void OpenCurrent()
        {
            var asset = currentData != null ? AssetDatabase.LoadMainAssetAtPath(currentData.AssetPath) : null;
            if (asset != null)
                AssetDatabase.OpenAsset(asset);
        }

        private static string GetOriginLabel(AsmDefOrigin origin)
        {
            switch (origin)
            {
                case AsmDefOrigin.ProjectAssets:
                    return "Project";
                case AsmDefOrigin.EmbeddedPackage:
                    return "Embedded";
                case AsmDefOrigin.RegistryPackage:
                    return "Registry";
                case AsmDefOrigin.BuiltIn:
                    return "Built-in";
                default:
                    return "Unknown";
            }
        }
    }
}
