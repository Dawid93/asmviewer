# Podsumowanie Etapu 5 — Polskie Notatki

## Cel etapu

Etap 5 skupił się na pięciu zadaniach wizualizacji i infrastruktury, które nadały narzędziu Assembly Architect charakter produkcyjny: podświetlanie cykli, filtrowanie grafu, mini-mapa, persystencja układu oraz ustawienia projektu.

---

## Zadanie 5.1 — Wizualizacja cykli (`CycleBanner`, `ApplyCycleHighlight`)

**Co zrobiono:**

- **`AsmDefNode`** — dodano właściwości `Model` (referencja do `AsmDefNodeModel`) i `CurrentState` (eksponuje prywatne `_currentState`). Dzięki `CurrentState` metody cykli i filtrowania mogą odczytywać obecny stan i przełączać wyłącznie jeden bit flagi bez kasowania pozostałych.
- **`AsmDefEdge`** — dodano pole `_currentState` i właściwość `CurrentState`; zaktualizowano `ApplyState`, żeby zapisywał stan przed zastosowaniem klas CSS.
- **`AsmDefGraphView.ApplyCycleHighlight`** — buduje dwa zbiory: identyfikatory węzłów będących w cyklu (SCC ≥ 2) oraz pary `(src, tgt)` krawędzi wewnątrz tych SCC. Iteruje węzły i krawędzie, przełączając bit `InCycle` z zachowaniem pozostałych flag.
- **`AsmDefGraphView.GetNodeById`** — pomocnik zwracający węzeł po StableId, używany przez baner do zaznaczania cyklu.
- **`CycleBanner`** — nowy `VisualElement` wstawiany między pasek narzędzi a `body`. Wyświetla ostrzeżenie `"⚠ N dependency cycle(s) detected — Focus next"` i przycisk **Focus**, który cyklicznie przechodzi przez SCC, zaznacza ich węzły i wywołuje `FrameSelection()`.
- **`AssemblyArchitectWindow.Rebuild`** — po każdym przebudowaniu grafu wywołuje `ApplyCycleHighlight` i `_cycleBanner.Update`.
- **USS** — krawędzie cyklu (`aa-edge-cycle`) mają `--edge-width: 3` i kolor `#ff8c42`; baner ma pomarańczowe tło i label.

---

## Zadanie 5.2 — Filtrowanie i wyszukiwanie (`GraphFilter`, `ApplyFilter`)

**Co zrobiono:**

- **`GraphFilter`** — nowy `readonly struct` z polami `SearchQuery` (małe litery), `ShowPackages`, `ShowBuiltIns`.
- **`AsmDefGraphView.ApplyFilter`** — w jednym przebiegu wyznacza dwa zbiory: `hiddenIds` (węzły filtrowane wg pochodzenia: pakiety / wbudowane) i `searchDimIds` (węzły nieodpowiadające zapytaniu). Następnie:
  - węzły z `hiddenIds` → `display: None`,
  - pozostałe → `display: Flex`; jeśli w `searchDimIds` → bit `Filtered` (opacity 0.25),
  - krawędzie → `display: None` gdy endpoint jest ukryty; `Filtered` gdy endpoint jest przyciemniony.
  - Zaznaczone węzły, które stały się ukryte, są odznaczane.
- **`AssemblyArchitectWindow`** — dodano pole `_filter`; nowa metoda `UpdateFilter` (przechowuje filtr i wywołuje `ApplyFilter` bez pełnego przebudowania); eventy toolbar `SearchChanged`, `ShowPackagesChanged`, `ShowBuiltInsChanged` wywołują `UpdateFilter` zamiast `Rebuild`.
- Filtr jest stosowany po `ApplyCycleHighlight` w każdym `Rebuild`, więc stan cykli nie jest kasowany przez filtrowanie.

---

## Zadanie 5.3 — Mini-mapa (`MiniMap`)

**Co zrobiono:**

- **`AsmDefGraphView`** — w konstruktorze, po siatce, tworzony jest `MiniMap { anchored = true }` z pozycją `(15, 15, 200, 160)`. Dodawany przez `Add()` (nie `AddElement()`), więc nie jest usuwany przez `Clear()`.
- **`SetMiniMapVisible(bool)`** — publiczna metoda przełączająca `display` mini-mapy.
- **`AssemblyArchitectWindow`** — toolbar event `MiniMapToggled` wywołuje `SetMiniMapVisible`; po `CreateGUI` stosowana jest persystowana wartość `showMiniMap`.
- **USS** — klasa `aa-minimap` definiuje semi-transparentne tło, obramowanie i zaokrąglone rogi.

---

## Zadanie 5.4 — Persystencja układu (`LayoutCache`)

**Co zrobiono:**

- **`LayoutCache`** — nowa klasa w `Editor/Infrastructure/`; zapisuje JSON do `ProjectSettings/Packages/com.ddev.assembly-architect/Layout.json`. Klucze posortowane alfabetycznie dla deterministycznych diffów VCS. Używa `IFileSystem` jako warstwy abstrakcji (przygotowane pod testy).
- **`LayoutCacheData` / `PositionEntry`** — typy `[Serializable]` dla `JsonUtility`.
- **`AssemblyArchitectWindow`**:
  - `OnEnable` — tworzy `LayoutCache`, ładuje dane i wstępnie wypełnia `_positions` z pliku JSON.
  - Pierwsze `Rebuild` — przywraca viewport z pliku cache; kolejne rebuildy — zachowują bieżący viewport.
  - `NodePositionChanged` + `viewTransformChanged` → debounced auto-save (500 ms).
  - `SaveLayoutRequested` → natychmiastowy zapis + komunikat `"Layout saved."` w pasku statusu przez 2 sekundy (`schedule.Execute`).
  - `ResetLayoutRequested` → usuwa plik JSON, czyści `_positions`, przebudowuje graf od nowa.

---

## Zadanie 5.5 — Dostawca ustawień projektu (`AssemblyArchitectSettings`)

**Co zrobiono:**

- **`AssemblyArchitectSettings`** — `ScriptableSingleton<T>` z atrybutem `[FilePath]` zapisujący do `ProjectSettings/Packages/…/Settings.asset`. Pola: `DefaultLayout`, `FailBuildsOnCycles`, `LayoutCacheIsTeamShared`, 5 kolorów, `ForceLayoutIterations`. Publiczna metoda `Save()` chowa bazową (protected) i po zapisie emituje statyczne zdarzenie `Changed`.
- **`AssemblyArchitectSettingsProvider`** — `[SettingsProvider]` rejestrujący stronę `"Project/Assembly Architect"` z 4 grupami pól (Layout, Visualization, Build, Storage) i przyciskiem **Reset to Defaults**. Generyczny pomocnik `Bind<TField, TValue>` upraszcza binding; `EnumField` jest bindowany bezpośrednio (obejście ograniczeń `BaseField<Enum>` vs `BaseField<LayoutKind>`).
- **`AssemblyArchitectSettings.uxml`** — cztery grupy z polami `EnumField`, `IntegerField`, `ColorField`, `Toggle`, `HelpBox` oraz przyciskiem reset.
- **`AsmDefNode`** — przy tworzeniu węzła odczytuje kolory z `AssemblyArchitectSettings.instance` i stosuje je jako inline style (obramowanie i kropka-origin), z bezpiecznym fallback do USS.
- **`AssemblyArchitectWindow`** — subskrybuje `AssemblyArchitectSettings.Changed → ScheduleRebuild`, dzięki czemu zmiana koloru w ustawieniach natychmiast przebudowuje węzły.

---

## Poprawki z samoprzeglądu

- **`AssemblyArchitectSettingsProvider`** — naprawiono błąd kompilacji: `EnumField : BaseField<Enum>` nie spełnia ograniczenia `BaseField<LayoutKind>`, więc biding EnumField wydzielono poza generyczny helper.
- **`AsmDefGraphView.Clear()`** — dodano wyjaśniający komentarz: `graphElements` iteruje po content pane (węzły, krawędzie), a nie po bezpośrednich dzieciach (MiniMap, siatka), więc `_miniMap` jest bezpieczny.

---

## Commits etapu 5

| Hash | Opis |
|------|------|
| `e14857f` | feat(cycle): CycleBanner, ApplyCycleHighlight, cycle edge styling (5.1) |
| `c6e783d` | feat(filter): GraphFilter, ApplyFilter, toolbar wiring (5.2) |
| `9b07733` | feat(minimap): MiniMap overlay, toggle, persistence (5.3) |
| `5e5c3f4` | feat(layout-cache): persist positions and viewport (5.4) |
| `0bd1832` | feat(settings): Project Settings provider (5.5) |
| `dec30ec` | fix(settings): EnumField binding, Clear() comment (self-review) |
