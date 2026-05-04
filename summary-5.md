# Podsumowanie etapu 5

## Zadanie 5.1 - Cycle visualization

Dodano wizualizację cykli zależności bezpośrednio w grafie. `AssemblyArchitectWindow` po przebudowie modelu uruchamia `CycleDetector.FindCycles`, a `AsmDefGraphView.ApplyCycleHighlight` podświetla węzły i krawędzie należące do cykli.

Dodano `CycleBanner`, widoczny pod toolbarem tylko wtedy, gdy istnieje co najmniej jeden cykl. Banner pokazuje liczbę cykli i pozwala przechodzić po nich przyciskiem `Focus`, który zaznacza właściwe węzły i wywołuje `FrameSelection()`.

Proponowany commit message:

```text
feat(graph): visualize dependency cycles
```

## Zadanie 5.2 - Filter and search

Dodano `GraphFilter` oraz obsługę pola wyszukiwania i toggle’i originu z toolbara. Węzły projektowe są zawsze widoczne, a package/built-in są pokazywane zależnie od ustawień `Show Packages` i `Show Built-ins`.

Filtr originu ukrywa węzły i powiązane krawędzie, natomiast filtr tekstowy zostawia layout stabilny i przyciemnia niedopasowane węzły oraz krawędzie. Search używa zapamiętanego lowercase name w `AsmDefNode`, żeby unikać zbędnego `ToLowerInvariant()` przy każdym przejściu po grafie.

Proponowany commit message:

```text
feat(graph): add filtering search and minimap
```

## Zadanie 5.3 - Mini-map

Dodano wbudowaną mini-mapę GraphView (`MiniMap`) do `AsmDefGraphView`. Mini-mapa jest zakotwiczona w lewym górnym rogu grafu, ma podstawowe style USS dopasowane do reszty narzędzia i reaguje na toggle `Mini-map` w toolbarze.

Widoczność mini-mapy jest zapisywana w serializowanym stanie okna, więc przetrwa domain reload tak jak pozostałe ustawienia toolbara.

Proponowany commit message:

```text
feat(graph): add filtering search and minimap
```

## Zadanie 5.4 - Layout cache persistence

Dodano `LayoutCache`, który zapisuje pozycje węzłów oraz viewport do pliku JSON. Domyślnie cache trafia do `ProjectSettings/Packages/com.ddev.assembly-architect/Layout.json`, a po wyłączeniu trybu team-shared może trafić do `UserSettings`.

Okno ładuje cache przy `OnEnable`, przywraca pozycje i viewport po pierwszym rebuildzie, automatycznie zapisuje zmiany z debouncingiem oraz obsługuje akcje `Save Layout` i `Reset Layout` z toolbara/overflow menu. Pozycje są sortowane po id, żeby plik miał deterministyczny diff.

Proponowany commit message:

```text
feat(editor): persist graph layout cache
```

## Zadanie 5.5 - Project Settings provider

Dodano `AssemblyArchitectSettings` jako `ScriptableSingleton` zapisywany w `ProjectSettings/Packages/com.ddev.assembly-architect/Settings.asset` oraz `AssemblyArchitectSettingsProvider` pod `Project/Assembly Architect`.

Strona ustawień zawiera domyślny layout, liczbę iteracji force layoutu, kolory wizualizacji, toggle fail-build-on-cycles, toggle team-shared layout oraz przycisk resetu do wartości domyślnych. Zmiana kolorów wywołuje event `Changed`, a otwarty graf odświeża kolory node’ów i edge’y bez przebudowy modelu.

Proponowany commit message:

```text
feat(settings): add assembly architect project settings
```

## Code review

Nie znalazłem blokujących problemów po kompilacji. Najważniejsza poprawa jakościowa to rozdzielenie stanów wizualnych grafu: cycle, broken i filtered są teraz składane w jednym miejscu (`ApplyVisualStates`), dzięki czemu filtrowanie nie usuwa podświetlenia cykli.

Ryzyka do sprawdzenia w Unity Editor: cache layoutu zapisuje się automatycznie po rebuildzie i zmianie viewportu, więc warto upewnić się, że nie powoduje niechcianych zmian w pliku przy samym otwarciu okna. Project Settings są zbudowane programowo mimo obecności UXML, więc wygląd jest prosty, ale funkcjonalny.

Weryfikacja: kod edytora skompilowano lokalnym kompilatorem Roslyn dostarczanym z Unity, z referencjami z projektu `AssemblyArchitect.Editor.csproj`, bez błędów i ostrżeń.
