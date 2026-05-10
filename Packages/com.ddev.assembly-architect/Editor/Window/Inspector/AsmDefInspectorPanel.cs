using System;
using System.Collections.Generic;
using AssemblyArchitect.Editor.Core;
using AssemblyArchitect.Editor.Graph;
using AssemblyArchitect.Editor.Infrastructure;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace AssemblyArchitect.Editor.Window.Inspector
{
    /// <summary>
    /// Right-hand inspector panel that displays and edits all fields of a selected
    /// <c>.asmdef</c> assembly definition.
    /// </summary>
    internal sealed class AsmDefInspectorPanel : VisualElement
    {
        private const string UxmlPath = "Packages/com.ddev.assembly-architect/Editor/UI/AsmDefInspectorPanel.uxml";
        private const string UssPath  = "Packages/com.ddev.assembly-architect/Editor/UI/AsmDefInspectorPanel.uss";

        // ── Known platform names ──────────────────────────────────────────────

        private static readonly string[] KnownPlatforms =
        {
            "Editor",
            "WindowsStandalone64",
            "Android",
            "iOS",
            "LinuxStandalone64",
            "macOSStandalone",
            "WebGL",
            "tvOS",
            "Switch",
            "PS4",
            "PS5",
            "XboxOne",
            "GameCoreXboxSeries",
            "Universal Windows Platform",
            "EmbeddedLinux",
            "QNX",
        };

        // ── Dependencies ──────────────────────────────────────────────────────

        private readonly AsmDefRepository _repo;
        private readonly AsmDefWriter     _writer;

        // ── State ─────────────────────────────────────────────────────────────

        private string     _currentId;
        private AsmDefData _current;
        private bool       _isReadOnly;

        // ── Text debounce ─────────────────────────────────────────────────────

        private readonly Debouncer         _textDebouncer;
        private Action<AsmDefData>         _pendingMutate;
        private string                     _pendingUndoLabel;

        // ── Content container ─────────────────────────────────────────────────

        private VisualElement _content;

        // ── Platform list handles (needed for cross-list validation) ──────────
        private List<string> _includePlatforms;
        private List<string> _excludePlatforms;
        private ListView     _includePlatformsView;
        private ListView     _excludePlatformsView;

        // ── Constructor ───────────────────────────────────────────────────────

        public AsmDefInspectorPanel(AsmDefRepository repo, AsmDefWriter writer)
        {
            _repo   = repo;
            _writer = writer;

            style.flexGrow = 1;

            // Stylesheet
            var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
            if (uss != null) styleSheets.Add(uss);

            // UXML skeleton
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            if (uxml != null)
                uxml.CloneTree(this);

            _content = this.Q<VisualElement>("inspector-content");
            if (_content == null)
            {
                _content = new VisualElement { name = "inspector-content" };
                _content.AddToClassList("aa-inspector-content");
                Add(_content);
            }

            _textDebouncer = new Debouncer(300, FlushTextEdit);

            // Cleanup on panel detach
            RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                _textDebouncer?.Dispose();
                if (_repo != null) _repo.Changed -= OnRepoChanged;
            });

            if (_repo != null)
                _repo.Changed += OnRepoChanged;

            ShowEmpty();
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Populates the panel for the assembly with the given <see cref="AsmDefData.StableId"/>.</summary>
        public void ShowFor(string asmDefId)
        {
            if (string.IsNullOrEmpty(asmDefId)) { Clear(); return; }
            _currentId = asmDefId;
            Refresh();
        }

        /// <summary>Clears the panel to the empty state.</summary>
        public new void Clear()
        {
            _currentId = null;
            _current   = null;
            ShowEmpty();
        }

        // ── Repo changes ──────────────────────────────────────────────────────

        private void OnRepoChanged()
        {
            if (string.IsNullOrEmpty(_currentId)) return;

            AsmDefData found = null;
            foreach (var d in _repo.LoadAll())
                if (d.StableId == _currentId) { found = d; break; }

            if (found == null) Clear();
            else               Refresh();
        }

        // ── Refresh ───────────────────────────────────────────────────────────

        private void Refresh()
        {
            if (_repo == null || string.IsNullOrEmpty(_currentId)) { ShowEmpty(); return; }

            _current = null;
            foreach (var d in _repo.LoadAll())
                if (d.StableId == _currentId) { _current = d; break; }

            if (_current == null) { ShowEmpty(); return; }

            _isReadOnly = _current.Origin == AsmDefOrigin.RegistryPackage
                          || _current.Origin == AsmDefOrigin.BuiltIn;
            RebuildContent();
        }

        // ── Empty state ───────────────────────────────────────────────────────

        private void ShowEmpty()
        {
            _content.Clear();
            var lbl = new Label("Select an assembly node to inspect.")
            {
                style =
                {
                    unityTextAlign = TextAnchor.MiddleCenter,
                    color          = new StyleColor(new Color(0.5f, 0.5f, 0.5f)),
                    marginTop      = 20,
                    whiteSpace     = WhiteSpace.Normal,
                }
            };
            _content.Add(lbl);
        }

        // ── Content builder ───────────────────────────────────────────────────

        private void RebuildContent()
        {
            _content.Clear();
            _includePlatforms     = null;
            _excludePlatforms     = null;
            _includePlatformsView = null;
            _excludePlatformsView = null;

            BuildHeader();

            if (_isReadOnly)
                _content.Add(MakeNote("Read-only (registry or built-in package)"));

            BuildGeneralSection();
            BuildPlatformsSection();

            BuildStringListSection(
                "Define Constraints",
                _current.DefineConstraints,
                "SYMBOL",
                "Edit Define Constraints",
                list => d => d.DefineConstraints = list.ToArray());

            BuildVersionDefinesSection();

            BuildPrecompiledSection();
            BuildReferencesSection();
        }

        // ── Header ────────────────────────────────────────────────────────────

        private void BuildHeader()
        {
            var header = new VisualElement();
            header.AddToClassList("aa-inspector-header");

            var nameLabel = new Label(_current.Name);
            nameLabel.AddToClassList("aa-inspector-name");
            header.Add(nameLabel);

            var badge = new Label(OriginLabel(_current.Origin));
            badge.AddToClassList("aa-origin-badge");
            badge.AddToClassList(OriginBadgeClass(_current.Origin));
            header.Add(badge);

            var pingBtn = new Button(() => PingAsset(_current.AssetPath))
                { text = "⊙", tooltip = "Ping in Project window" };
            pingBtn.AddToClassList("aa-inspector-btn");
            header.Add(pingBtn);

            var openBtn = new Button(() => OpenAsset(_current.AssetPath))
                { text = "✎", tooltip = "Open in external editor" };
            openBtn.AddToClassList("aa-inspector-btn");
            header.Add(openBtn);

            _content.Add(header);
        }

        // ── General ───────────────────────────────────────────────────────────

        private void BuildGeneralSection()
        {
            var foldout = MakeFoldout("General", true);

            foldout.Add(MakeTextField("Root Namespace", _current.RootNamespace,
                v => ScheduleTextEdit(d => d.RootNamespace = v, "Edit Root Namespace")));

            foldout.Add(MakeToggle("Allow Unsafe Code", _current.AllowUnsafeCode,
                v => ApplyEdit(d => d.AllowUnsafeCode = v, "Toggle Allow Unsafe Code")));

            foldout.Add(MakeToggle("Auto Referenced", _current.AutoReferenced,
                v => ApplyEdit(d => d.AutoReferenced = v, "Toggle Auto Referenced")));

            foldout.Add(MakeToggle("Override References", _current.OverrideReferences,
                v => ApplyEdit(d => d.OverrideReferences = v, "Toggle Override References")));

            foldout.Add(MakeToggle("No Engine References", _current.NoEngineReferences,
                v => ApplyEdit(d => d.NoEngineReferences = v, "Toggle No Engine References")));

            _content.Add(foldout);
        }

        // ── Platforms ─────────────────────────────────────────────────────────

        private void BuildPlatformsSection()
        {
            var foldout   = MakeFoldout("Platforms", false);
            var container = new VisualElement();
            container.AddToClassList("aa-platforms-container");

            _includePlatforms = new List<string>(_current.IncludePlatforms ?? Array.Empty<string>());
            _excludePlatforms = new List<string>(_current.ExcludePlatforms ?? Array.Empty<string>());

            container.Add(BuildPlatformSide("Include", _includePlatforms, isInclude: true, out _includePlatformsView));
            container.Add(BuildPlatformSide("Exclude", _excludePlatforms, isInclude: false, out _excludePlatformsView));
            foldout.Add(container);

            foldout.Add(new Label("Include and Exclude cannot both be non-empty.")
                { style = { fontSize = 9, color = new StyleColor(new Color(0.55f, 0.55f, 0.55f)), marginTop = 2 } });

            _content.Add(foldout);
        }

        private VisualElement BuildPlatformSide(string title, List<string> list, bool isInclude, out ListView listView)
        {
            var side = new VisualElement();
            side.AddToClassList("aa-platforms-side");

            var lbl = new Label(title);
            lbl.AddToClassList("aa-platforms-label");
            side.Add(lbl);

            // ── ListView uses userData trick to avoid stale index captures ────
            var lv = new ListView
            {
                itemsSource  = list,
                fixedItemHeight = 20,
                reorderable  = !_isReadOnly,
                showBorder   = true,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly,
                style        = { minHeight = 60, maxHeight = 100 },
                makeItem     = () => new Label { style = { fontSize = 11, paddingLeft = 4 } },
                bindItem     = (el, i) => ((Label)el).text = list[i],
            };

            lv.itemIndexChanged += (from, to) =>
            {
                if (_isReadOnly) return;
                var item = list[from]; list.RemoveAt(from); list.Insert(to, item);
                SavePlatforms();
            };

            side.Add(lv);
            listView = lv;

            if (!_isReadOnly)
            {
                var toolbar = new VisualElement();
                toolbar.AddToClassList("aa-list-toolbar");

                var addBtn = new Button(() => ShowPlatformPicker(list, isInclude))
                    { text = "+", tooltip = "Add platform" };
                addBtn.AddToClassList("aa-list-add-btn");

                var removeBtn = new Button(() =>
                {
                    if (lv.selectedIndex < 0 || lv.selectedIndex >= list.Count) return;
                    list.RemoveAt(lv.selectedIndex);
                    lv.Rebuild();
                    SavePlatforms();
                }) { text = "−", tooltip = "Remove selected" };
                removeBtn.AddToClassList("aa-list-remove-btn");

                toolbar.Add(addBtn);
                toolbar.Add(removeBtn);
                side.Add(toolbar);
            }

            return side;
        }

        private void ShowPlatformPicker(List<string> target, bool isInclude)
        {
            var menu = new GenericMenu();
            foreach (var p in KnownPlatforms)
            {
                if (target.Contains(p)) continue;
                var captured = p;
                menu.AddItem(new GUIContent(captured), false, () =>
                {
                    var other     = isInclude ? _excludePlatforms : _includePlatforms;
                    var otherView = isInclude ? _excludePlatformsView : _includePlatformsView;
                    if (other != null && other.Count > 0 && target.Count == 0)
                    {
                        if (!EditorUtility.DisplayDialog("Platform Constraint",
                                "Cannot have both Include and Exclude platforms. Clear the other list?",
                                "Clear other list", "Cancel"))
                            return;
                        other.Clear();
                        otherView?.Rebuild();
                    }
                    target.Add(captured);
                    var myView = isInclude ? _includePlatformsView : _excludePlatformsView;
                    myView?.Rebuild();
                    SavePlatforms();
                });
            }
            menu.ShowAsContext();
        }

        private void SavePlatforms()
        {
            var inc = _includePlatforms?.ToArray() ?? Array.Empty<string>();
            var exc = _excludePlatforms?.ToArray() ?? Array.Empty<string>();
            ApplyEdit(d => { d.IncludePlatforms = inc; d.ExcludePlatforms = exc; }, "Edit Platforms");
        }

        // ── Generic string list ───────────────────────────────────────────────

        /// <param name="mutateFactory">
        ///   Given the current list, returns an <c>Action&lt;AsmDefData&gt;</c> that writes the list back
        ///   to the correct field of the data. Example: <c>list => d => d.DefineConstraints = list.ToArray()</c>.
        /// </param>
        private void BuildStringListSection(
            string title,
            string[] initial,
            string newItemDefault,
            string undoLabel,
            Func<List<string>, Action<AsmDefData>> mutateFactory)
        {
            var foldout = MakeFoldout(title, false);
            var list    = new List<string>(initial ?? Array.Empty<string>());

            var lv = new ListView
            {
                itemsSource  = list,
                fixedItemHeight = 20,
                reorderable  = !_isReadOnly,
                showBorder   = true,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly,
                style        = { minHeight = 40, maxHeight = 120 },
                makeItem     = () =>
                {
                    var tf = new TextField { style = { flexGrow = 1 } };
                    // Callback reads index from userData — avoids stale capture
                    tf.RegisterValueChangedCallback(evt =>
                    {
                        if (tf.userData is int idx && idx < list.Count)
                        {
                            list[idx] = evt.newValue;
                            ScheduleTextEdit(mutateFactory(list), undoLabel);
                        }
                    });
                    return tf;
                },
                bindItem = (el, i) =>
                {
                    var tf = (TextField)el;
                    tf.userData = i;
                    tf.SetValueWithoutNotify(list[i]);
                    tf.isReadOnly = _isReadOnly;
                },
            };

            lv.itemIndexChanged += (from, to) =>
            {
                if (_isReadOnly) return;
                var item = list[from]; list.RemoveAt(from); list.Insert(to, item);
                ApplyEdit(mutateFactory(list), undoLabel);
            };

            foldout.Add(lv);

            if (!_isReadOnly)
            {
                var toolbar = new VisualElement();
                toolbar.AddToClassList("aa-list-toolbar");

                var addBtn = new Button(() =>
                {
                    list.Add(newItemDefault);
                    lv.Rebuild();
                    lv.selectedIndex = list.Count - 1;
                    ApplyEdit(mutateFactory(list), undoLabel);
                }) { text = "+", tooltip = "Add entry" };
                addBtn.AddToClassList("aa-list-add-btn");

                var removeBtn = new Button(() =>
                {
                    if (lv.selectedIndex < 0 || lv.selectedIndex >= list.Count) return;
                    list.RemoveAt(lv.selectedIndex);
                    lv.Rebuild();
                    ApplyEdit(mutateFactory(list), undoLabel);
                }) { text = "−", tooltip = "Remove selected" };
                removeBtn.AddToClassList("aa-list-remove-btn");

                toolbar.Add(addBtn);
                toolbar.Add(removeBtn);
                foldout.Add(toolbar);
            }

            _content.Add(foldout);
        }

        // ── Version Defines ───────────────────────────────────────────────────

        private void BuildVersionDefinesSection()
        {
            var foldout = MakeFoldout("Version Defines", false);
            var list    = new List<VersionDefine>(_current.VersionDefines ?? Array.Empty<VersionDefine>());

            // Column headers
            var header = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            header.Add(MakeColHeader("Name",       1f));
            header.Add(MakeColHeader("Expression", 1f));
            header.Add(MakeColHeader("Define",     1f));
            foldout.Add(header);

            // makeItem creates three TextFields; userData stores the index
            Func<VisualElement> makeItem = () =>
            {
                var row  = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                var nameF = new TextField { style = { flexGrow = 1 }, name = "vd-name" };
                var exprF = new TextField { style = { flexGrow = 1 }, name = "vd-expr" };
                var defF  = new TextField { style = { flexGrow = 1 }, name = "vd-def"  };

                void OnChange()
                {
                    if (row.userData is int idx && idx < list.Count)
                    {
                        list[idx] = new VersionDefine
                        {
                            Name       = nameF.value,
                            Expression = exprF.value,
                            Define     = defF.value,
                        };
                        var snapshot = list.ToArray();
                        ScheduleTextEdit(d => d.VersionDefines = snapshot, "Edit Version Defines");
                    }
                }

                nameF.RegisterValueChangedCallback(_ => OnChange());
                exprF.RegisterValueChangedCallback(_ => OnChange());
                defF.RegisterValueChangedCallback(_ => OnChange());
                row.Add(nameF); row.Add(exprF); row.Add(defF);
                return row;
            };

            Action<VisualElement, int> bindItem = (el, i) =>
            {
                el.userData = i;
                var nameF = el.Q<TextField>("vd-name");
                var exprF = el.Q<TextField>("vd-expr");
                var defF  = el.Q<TextField>("vd-def");
                if (nameF == null || exprF == null || defF == null) return;
                nameF.SetValueWithoutNotify(list[i].Name ?? string.Empty);
                exprF.SetValueWithoutNotify(list[i].Expression ?? string.Empty);
                defF.SetValueWithoutNotify(list[i].Define ?? string.Empty);
                nameF.isReadOnly = _isReadOnly;
                exprF.isReadOnly = _isReadOnly;
                defF.isReadOnly  = _isReadOnly;
            };

            var lv = new ListView
            {
                itemsSource  = list,
                fixedItemHeight = 22,
                reorderable  = !_isReadOnly,
                showBorder   = true,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly,
                style        = { minHeight = 44, maxHeight = 140 },
                makeItem     = makeItem,
                bindItem     = bindItem,
            };

            lv.itemIndexChanged += (from, to) =>
            {
                if (_isReadOnly) return;
                var item = list[from]; list.RemoveAt(from); list.Insert(to, item);
                var snapshot = list.ToArray();
                ApplyEdit(d => d.VersionDefines = snapshot, "Reorder Version Defines");
            };

            foldout.Add(lv);

            if (!_isReadOnly)
            {
                var toolbar = new VisualElement();
                toolbar.AddToClassList("aa-list-toolbar");

                var addBtn = new Button(() =>
                {
                    list.Add(new VersionDefine());
                    lv.Rebuild();
                    lv.selectedIndex = list.Count - 1;
                    var snapshot = list.ToArray();
                    ApplyEdit(d => d.VersionDefines = snapshot, "Add Version Define");
                }) { text = "+", tooltip = "Add version define" };
                addBtn.AddToClassList("aa-list-add-btn");

                var removeBtn = new Button(() =>
                {
                    if (lv.selectedIndex < 0 || lv.selectedIndex >= list.Count) return;
                    list.RemoveAt(lv.selectedIndex);
                    lv.Rebuild();
                    var snapshot = list.ToArray();
                    ApplyEdit(d => d.VersionDefines = snapshot, "Remove Version Define");
                }) { text = "−", tooltip = "Remove selected" };
                removeBtn.AddToClassList("aa-list-remove-btn");

                toolbar.Add(addBtn);
                toolbar.Add(removeBtn);
                foldout.Add(toolbar);
            }

            _content.Add(foldout);
        }

        // ── Precompiled References ────────────────────────────────────────────

        private void BuildPrecompiledSection()
        {
            bool enabled = !_isReadOnly && _current.OverrideReferences;
            var foldout  = MakeFoldout("Precompiled References", false);

            if (!enabled && !_isReadOnly)
                foldout.Add(new Label("Enable 'Override References' to edit.")
                    { style = { fontSize = 9, color = new StyleColor(new Color(0.55f, 0.55f, 0.55f)), unityFontStyleAndWeight = FontStyle.Italic } });

            var list = new List<string>(_current.PrecompiledReferences ?? Array.Empty<string>());

            var lv = new ListView
            {
                itemsSource  = list,
                fixedItemHeight = 20,
                reorderable  = enabled,
                showBorder   = true,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly,
                style        = { minHeight = 40, maxHeight = 100 },
                makeItem     = () =>
                {
                    var tf = new TextField { style = { flexGrow = 1 } };
                    tf.RegisterValueChangedCallback(evt =>
                    {
                        if (!enabled) return;
                        if (tf.userData is int idx && idx < list.Count)
                        {
                            list[idx] = evt.newValue;
                            var snapshot = list.ToArray();
                            ScheduleTextEdit(d => d.PrecompiledReferences = snapshot, "Edit Precompiled References");
                        }
                    });
                    return tf;
                },
                bindItem     = (el, i) =>
                {
                    var tf = (TextField)el;
                    tf.userData = i;
                    tf.SetValueWithoutNotify(list[i]);
                    tf.isReadOnly = !enabled;
                },
            };

            lv.itemIndexChanged += (from, to) =>
            {
                if (!enabled) return;
                var item = list[from]; list.RemoveAt(from); list.Insert(to, item);
                var snapshot = list.ToArray();
                ApplyEdit(d => d.PrecompiledReferences = snapshot, "Reorder Precompiled References");
            };

            foldout.Add(lv);

            if (enabled)
            {
                var toolbar = new VisualElement();
                toolbar.AddToClassList("aa-list-toolbar");

                var addBtn = new Button(() =>
                {
                    list.Add("Assembly.dll");
                    lv.Rebuild();
                    lv.selectedIndex = list.Count - 1;
                    var snapshot = list.ToArray();
                    ApplyEdit(d => d.PrecompiledReferences = snapshot, "Add Precompiled Reference");
                }) { text = "+", tooltip = "Add precompiled reference" };
                addBtn.AddToClassList("aa-list-add-btn");

                var removeBtn = new Button(() =>
                {
                    if (lv.selectedIndex < 0 || lv.selectedIndex >= list.Count) return;
                    list.RemoveAt(lv.selectedIndex);
                    lv.Rebuild();
                    var snapshot = list.ToArray();
                    ApplyEdit(d => d.PrecompiledReferences = snapshot, "Remove Precompiled Reference");
                }) { text = "−", tooltip = "Remove selected" };
                removeBtn.AddToClassList("aa-list-remove-btn");

                toolbar.Add(addBtn);
                toolbar.Add(removeBtn);
                foldout.Add(toolbar);
            }

            _content.Add(foldout);
        }

        // ── References (read-only) ────────────────────────────────────────────

        private void BuildReferencesSection()
        {
            var foldout = MakeFoldout("References (edit via graph)", false);
            var refs    = _current.References ?? Array.Empty<string>();

            foreach (var r in refs)
                foldout.Add(new Label(r) { style = { fontSize = 11, marginLeft = 4, marginBottom = 1 } });

            if (refs.Length == 0)
                foldout.Add(new Label("(no references)")
                    { style = { fontSize = 10, color = new StyleColor(new Color(0.5f, 0.5f, 0.5f)), marginLeft = 4 } });

            _content.Add(foldout);
        }

        // ── Edit pipeline ─────────────────────────────────────────────────────

        private void ApplyEdit(Action<AsmDefData> mutate, string undoLabel)
        {
            if (_isReadOnly || _current == null) return;
            var clone = _current.Clone();
            mutate(clone);
            try
            {
                _writer.Save(clone, undoLabel);
                _current = clone;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AssemblyArchitect] Inspector save failed: {ex.Message}");
            }
        }

        private void ScheduleTextEdit(Action<AsmDefData> mutate, string undoLabel)
        {
            if (_isReadOnly) return;
            _pendingMutate    = mutate;
            _pendingUndoLabel = undoLabel;
            _textDebouncer.Bump();
        }

        private void FlushTextEdit()
        {
            if (_pendingMutate == null) return;
            ApplyEdit(_pendingMutate, _pendingUndoLabel ?? "Edit Assembly Definition");
            _pendingMutate    = null;
            _pendingUndoLabel = null;
        }

        // ── UI helpers ────────────────────────────────────────────────────────

        private Foldout MakeFoldout(string title, bool expanded)
        {
            var f = new Foldout { text = title, value = expanded };
            f.AddToClassList("aa-section");
            return f;
        }

        private TextField MakeTextField(string label, string value, Action<string> onChange)
        {
            var tf = new TextField(label) { isReadOnly = _isReadOnly };
            tf.SetValueWithoutNotify(value ?? string.Empty);
            tf.RegisterValueChangedCallback(evt => onChange(evt.newValue));
            return tf;
        }

        private Toggle MakeToggle(string label, bool value, Action<bool> onChange)
        {
            var t = new Toggle(label);
            t.SetValueWithoutNotify(value);
            t.SetEnabled(!_isReadOnly);
            t.RegisterValueChangedCallback(evt => onChange(evt.newValue));
            return t;
        }

        private static VisualElement MakeColHeader(string text, float flex) =>
            new Label(text)
            {
                style =
                {
                    flexGrow = flex,
                    fontSize = 10,
                    color    = new StyleColor(new Color(0.55f, 0.55f, 0.55f)),
                    unityFontStyleAndWeight = FontStyle.Bold,
                    paddingLeft = 4,
                }
            };

        private static Label MakeNote(string text) =>
            new Label(text)
            {
                style =
                {
                    fontSize                = 10,
                    color                   = new StyleColor(new Color(0.9f, 0.65f, 0.2f)),
                    unityFontStyleAndWeight = FontStyle.Italic,
                    marginBottom            = 4,
                    marginLeft              = 4,
                    whiteSpace              = WhiteSpace.Normal,
                }
            };

        // ── Asset helpers ─────────────────────────────────────────────────────

        private static void PingAsset(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return;
            var asset = AssetDatabase.LoadAssetAtPath<UnityEditorInternal.AssemblyDefinitionAsset>(assetPath);
            if (asset != null) EditorGUIUtility.PingObject(asset);
        }

        private static void OpenAsset(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return;
            var asset = AssetDatabase.LoadAssetAtPath<UnityEditorInternal.AssemblyDefinitionAsset>(assetPath);
            if (asset != null) AssetDatabase.OpenAsset(asset);
        }

        // ── Origin helpers ────────────────────────────────────────────────────

        private static string OriginLabel(AsmDefOrigin origin) => origin switch
        {
            AsmDefOrigin.ProjectAssets    => "Project",
            AsmDefOrigin.EmbeddedPackage  => "Embedded",
            AsmDefOrigin.RegistryPackage  => "Registry",
            AsmDefOrigin.BuiltIn          => "Built-in",
            _                             => "Unknown",
        };

        private static string OriginBadgeClass(AsmDefOrigin origin) => origin switch
        {
            AsmDefOrigin.ProjectAssets    => "aa-origin-badge--project",
            AsmDefOrigin.EmbeddedPackage  => "aa-origin-badge--embedded",
            AsmDefOrigin.RegistryPackage  => "aa-origin-badge--registry",
            AsmDefOrigin.BuiltIn          => "aa-origin-badge--builtin",
            _                             => "aa-origin-badge--project",
        };
    }
}
