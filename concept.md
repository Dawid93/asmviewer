# Assembly Architect — Dokument koncepcyjny

## 1. Czym jest ta wtyczka?

**Assembly Architect** to narzędzie edytorskie dla Unity, które wizualizuje zależności
między plikami `.asmdef` (Assembly Definition) w formie interaktywnego grafu węzłów.
Pozwala programiście zobaczyć na jednym ekranie całą architekturę projektu — które
assembly zależy od którego — oraz edytować te zależności bezpośrednio z poziomu grafu,
bez potrzeby ręcznego modyfikowania plików JSON.

Wtyczka jest dystrybuowana jako **paczka UPM** (Unity Package Manager), co oznacza, że
można ją zainstalować w dowolnym projekcie Unity jedną linią w `manifest.json` lub przez
interfejs Package Manager.

---

## 2. Problem, który rozwiązuje

W większych projektach Unity liczba plików `.asmdef` szybko rośnie. Typowy projekt
produkcyjny może mieć ich kilkanaście lub kilkadziesiąt. Zarządzanie zależnościami między
nimi staje się trudne, bo:

- Każdy `.asmdef` to plik JSON — nieczytelny bez kontekstu.
- Unity Inspector pokazuje tylko referencje jednego assembly na raz.
- Nie ma wbudowanego narzędzia do wykrywania cykli w zależnościach.
- Przeniesienie skryptu między assembly wymaga ręcznej edycji kilku plików.
- Nowi członkowie zespołu nie mają szybkiego sposobu na zrozumienie struktury projektu.

**Assembly Architect** rozwiązuje te problemy przez graficzną reprezentację całej siatki
zależności z możliwością edycji.

---

## 3. Główne funkcje

### 3.1 Widok grafu
Okno edytora otwiera się przez **Window ▸ Analysis ▸ Assembly Architect**. Centralną
część okna zajmuje interaktywny graf, w którym każdy węzeł reprezentuje jeden plik
`.asmdef`, a każda krawędź reprezentuje referencję między assembly.

Graf obsługuje:
- przesuwanie i skalowanie (pan + zoom),
- zaznaczanie pojedynczych węzłów i krawędzi,
- zaznaczanie prostokątem (rectangle select),
- mini-mapę w rogu ekranu ułatwiającą nawigację w dużych projektach.

### 3.2 Dodawanie i usuwanie referencji
Użytkownik może przeciągnąć krawędź między dwoma węzłami, aby dodać nową referencję.
Wybranie krawędzi i naciśnięcie Delete usuwa referencję. Obie operacje zapisują
zmiany bezpośrednio do pliku `.asmdef` na dysku i obsługują **Undo/Redo** poprzez
wbudowany system Unity.

### 3.3 Wykrywanie cykli
Cykl w grafie zależności (np. A → B → A) powoduje błąd kompilacji w Unity. Wtyczka
wykrywa takie sytuacje automatycznie po każdej zmianie, zaznacza węzły i krawędzi
uczestniczące w cyklu pomarańczowym kolorem i wyświetla baner ostrzegawczy z przyciskiem
"Focus", który przenosi widok na dany cykl.

Przed dodaniem referencji, która stworzyłaby cykl, narzędzie wyświetla ostrzeżenie z
opcją anulowania lub wymuszenia operacji.

### 3.4 Panel inspektora
Po kliknięciu w węzeł, prawy panel wyświetla wszystkie właściwości danego `.asmdef`:
- platformy (include/exclude),
- ograniczenia define'ów,
- version defines,
- flagi (AutoReferenced, AllowUnsafeCode, itd.).

Każde pole można edytować bezpośrednio — zmiany są zapisywane z obsługą Undo.

### 3.5 Tworzenie nowych assembly
Kliknięcie prawym przyciskiem myszy na pustej przestrzeni grafu otwiera okno dialogowe
do stworzenia nowego pliku `.asmdef` — z wyborem folderu i nazwy. Nowy węzeł pojawia
się w miejscu kliknięcia.

### 3.6 Układy grafu
Dostępne są dwa algorytmy automatycznego rozmieszczenia węzłów:
- **Hierarchiczny** (Sugiyama) — dobry dla projektów z wyraźną hierarchią warstw
  (Core → Gameplay → UI).
- **Force-directed** (Fruchterman–Reingold) — lepszy dla bardziej płaskich lub
  siateczkowych struktur.

Użytkownik może ręcznie przestawić dowolny węzeł; pozycje są zapamiętywane w pliku
`ProjectSettings/...` i mogą być commitowane do repozytorium, dzięki czemu cały zespół
widzi ten sam układ grafu.

### 3.7 Filtrowanie i wyszukiwanie
Pasek narzędziowy zawiera:
- pole wyszukiwania (filtruje węzły po nazwie),
- przełącznik "Show Packages" (ukrywa/pokazuje assembly z paczek),
- przełącznik "Show Built-ins" (ukrywa/pokazuje moduły wbudowane Unity).

Węzły niespełniające kryterium wyszukiwania są przyciemniane (nie usuwane z grafu), dzięki
czemu kontekst struktury pozostaje widoczny.

---

## 4. Architektura techniczna

Wtyczka jest podzielona na cztery warstwy, z których każda może być testowana osobno:

```
┌─────────────────────────────────────────────────────┐
│  Warstwa prezentacji                                │
│  EditorWindow · GraphView · UXML/USS · Inspector    │
├─────────────────────────────────────────────────────┤
│  Warstwa aplikacji (komendy)                        │
│  AddReferenceCommand · RemoveReferenceCommand ·     │
│  CreateAsmDefCommand                                │
├─────────────────────────────────────────────────────┤
│  Warstwa domeny (czysty C#, bez Unity API)          │
│  AsmDefData · DependencyGraphModel ·                │
│  CycleDetector · LayoutEngines                      │
├─────────────────────────────────────────────────────┤
│  Warstwa infrastruktury                             │
│  AsmDefRepository · AsmDefWriter · LayoutCache      │
└─────────────────────────────────────────────────────┘
```

Logika domenowa (wykrywanie cykli, budowanie grafu, algorytmy układu) jest całkowicie
niezależna od Unity API i może być testowana zwykłymi testami edit-mode bez uruchamiania
Edytora.

### Klucz technologiczny
- **UI Toolkit** (UXML + USS) — cały interfejs użytkownika.
- **GraphView** (`UnityEditor.Experimental.GraphView`) — interaktywny graf.
- **ScriptableSingleton** — ustawienia projektu w Project Settings.
- **AssetPostprocessor** — automatyczne wykrywanie zmian w plikach `.asmdef`.
- **Undo API** — integracja z systemem cofania Unity.

---

## 5. Dystrybucja

Wtyczka jest projektowana z myślą o trzech kanałach dystrybucji:

| Kanał | Sposób instalacji |
|-------|-------------------|
| Git URL | `"com.<vendor>.assembly-architect": "https://github.com/..."` w `manifest.json` |
| Tarball | Plik `.tgz` dodawany przez Package Manager UI |
| Asset Store | Paczka w kategorii *Tools / Utilities* |

Paczka zawiera:
- kod edytora (Editor-only, nie trafia do builda gry),
- dwa sample projekty (czysty DAG i demonstracja cyklu),
- dokumentację w `Documentation~/`.

---

## 6. Plan realizacji

Implementacja jest podzielona na osiem faz. Każda faza kończy się działającym,
kompilowalnym stanem projektu.

### Faza 0 — Bootstrap
Stworzenie szkieletu paczki UPM: `package.json`, struktury katalogów, pliku `.asmdef`
dla kodu edytora i dla testów, konfiguracji repozytorium (`.editorconfig`,
`.gitattributes`).

### Faza 1 — Domena i infrastruktura
Implementacja czystego modelu danych bez zależności od Unity API:
- `AsmDefData` — odzwierciedlenie schematu JSON pliku `.asmdef`,
- `AsmDefRepository` — skanowanie projektu i śledzenie zmian,
- `AsmDefWriter` — zapis z integracją Undo,
- `DependencyGraphModel` — immutable graf węzłów i krawędzi,
- `CycleDetector` — algorytm Tarjana dla SCC,
- `HierarchicalLayout` i `ForceDirectedLayout` — dwa algorytmy układu.

### Faza 2 — Okno edytora
Stworzenie szkieletu `EditorWindow` z paskiem narzędzi, hostem grafu, panelem
inspektora i paskiem statusu. Wszystkie elementy UI bez jeszcze podpiętej logiki.

### Faza 3 — Widok grafu
Integracja `GraphView` z warstwą domeny:
- `AsmDefNode` — niestandardowy węzeł z kolorowaniem po Origin,
- `AsmDefEdge` — niestandardowa krawędź z obsługą cykli,
- podpięcie zdarzeń (add edge, remove edge, move node),
- pierwsze rzeczywiste dane z projektu w oknie.

### Faza 4 — Komendy i Undo
Implementacja pełnych operacji zapisu:
- `AddReferenceCommand` — z walidacją cykli i duplikatów,
- `RemoveReferenceCommand` — z potwierdzeniem,
- `CreateAsmDefCommand` — z oknem dialogowym,
- `AsmDefInspectorPanel` — edycja wszystkich pól `.asmdef`.

### Faza 5 — Analiza i polerowanie
- Wizualizacja cykli (highlight + baner + Focus),
- filtrowanie i wyszukiwanie węzłów,
- mini-mapa,
- zapis i odczyt pozycji węzłów na dysk,
- strona w Project Settings z wszystkimi opcjami.

### Faza 6 — Testy i jakość
- Testy jednostkowe domeny (>80% pokrycia kodu),
- testy integracyjne z prawdziwą bazą danych Unity,
- przebieg skill `simplify` — usunięcie martwego kodu, uproszczenie API.

### Faza 7 — Dystrybucja
- Sample projekty (czysty DAG + cykl demo),
- pełna dokumentacja,
- opcjonalne przerywanie builda przy wykryciu cyklu,
- bump wersji do 1.0.0 i przygotowanie do publikacji.

---

## 7. Co NIE wchodzi w zakres MVP

Poniższe funkcje są świadomie wykluczone z pierwszego wydania:

- **Przenoszenie skryptów między assembly** — operacja ryzykowna bez pełnego analizatora
  Roslyn; możliwy temat dla wersji 2.0.
- **Analiza nieużywanych referencji** — wymaga informacji z kompilatora, trudne bez
  dostępu do danych Roslyn.
- **Eksport grafu** (PNG, DOT, Mermaid) — przydatny, ale nie krytyczny.
- **Obsługa assembly runtime** — tylko Editor-only assembly są w pełni kontrolowane
  przez użytkownika; runtime assembly często pochodzą z paczek.
- **Multi-projekt / Unity Cloud Build** — zbyt złożone bez realnego środowiska testowego.

---

## 8. Definicja ukończenia

Wtyczka jest uznana za gotową do wydania, gdy:

1. Otwiera się bez błędów w czystym projekcie Unity 6.
2. Prawidłowo wykrywa i wyświetla wszystkie pliki `.asmdef` z projektu i paczek.
3. Dodanie i usunięcie referencji przez graf zapisuje zmiany na dysk i jest odwracalne
   przez Undo.
4. Cykle są wykrywane i wizualizowane w czasie rzeczywistym.
5. Pozycje węzłów przeżywają restart Unity.
6. Wszystkie testy przechodzą (Test Runner: zero błędów).
7. Console Unity jest czysta po pełnym imporcie paczki i otwarciu okna.
8. Sample projekty działają po imporcie przez Package Manager.
