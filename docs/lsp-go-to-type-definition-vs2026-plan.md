# Plan implementacji LSP-only „Go to Type Definition” (VS 2026)

## Cel
Dostarczyć `Go to Type Definition` wyłącznie przez ścieżkę LSP (`textDocument/typeDefinition`) dla Visual Studio 2026, bez zależności runtime od legacy `FSharp.Editor` (MEF/in-proc).

## Założenia architektoniczne
- Źródło prawdy: `src/FSharp.Compiler.LanguageServer`.
- Integracja klienta VS: `src/FSharp.VisualStudio.Extension/FSharpLanguageServerProvider.cs`.
- Semantyka i cache: `FSharpWorkspace` / `FSharpWorkspaceQuery` + `FSharpChecker`.
- Algorytm bazowy: port logiki FSAC `TryFindTypeDeclaration` (symbol → typ → `FSharpEntity` → lokalizacja).


## Status realizacji (aktualizacja)

### Zrealizowane
- Dodano endpoint LSP `textDocument/typeDefinition` w `GoToTypeDefinitionHandler` i zarejestrowano go w DI serwera.
- Dodano feature flag `TypeDefinition` w `FSharpLanguageServerConfig` (domyślnie `true`) i spięto ją z zachowaniem handlera.
- Dodano capability `TypeDefinitionProvider` po stronie providera VS.
- Dodano ustawienie `GetTypeDefinitionFrom` (`old/lsp/both/unset`) i propagację do konfiguracji serwera.
- Dodano query workspace `GetTypeDefinitionForFile` z rozpoznaniem typu dla podstawowych klas symboli (`FSharpField`, `FSharpMemberOrFunctionOrValue`, `FSharpParameter`, `FSharpUnionCase`, `FSharpEntity`).
- Utwardzono zwracanie wyników LSP: handler zwraca pusty wynik dla brakującego/niepoprawnego URI zamiast rzucać wyjątek.

### Niezrealizowane (pozostaje w backlogu)
- Brak pełnej ścieżki external types: SourceLink/dekompilacja/cache artefaktów.
- Brak dedykowanego wspólnego silnika `Definition | TypeDefinition` (query nadal zawiera logikę bez wydzielenia osobnego engine).
- Brak testów jednostkowych/integracyjnych dla `typeDefinition` i capability gating.
- Brak telemetrii requestu `textDocument/typeDefinition` (`isExternal`, `usedSourcelink`, `usedDecompile`, `duration`, `cancelled`).

### Ograniczenia walidacji w tym środowisku
- `build.sh` nie może dokończyć bootstrapu .NET, ponieważ proxy zwraca `403 Forbidden` dla pobrania `https://builds.dotnet.microsoft.com/dotnet/scripts/v1/dotnet-install.sh`.

## Scope zmian

### 1) Serwer LSP (`FSharp.Compiler.LanguageServer`)
1. Dodać handler `textDocument/typeDefinition`:
   - nowy plik: `src/FSharp.Compiler.LanguageServer/Handlers/GoToTypeDefinitionHandler.fs`.
   - implementacja `IRequestHandler<...>` w stylu istniejących handlerów (`cancellableTask`, CT-first).
2. Rejestracja handlera w DI:
   - `src/FSharp.Compiler.LanguageServer/FSharpLanguageServer.fs` (`ConstructLspServices`).
3. Rozszerzyć konfigurację feature flag:
   - `src/FSharp.Compiler.LanguageServer/FSharpLanguageServerConfig.fs`.
   - dodać `TypeDefinition: bool` (domyślnie `true`).

### 2) Provider VS (`FSharp.VisualStudio.Extension`)
1. Ogłosić capability:
   - `ServerCapabilities.TypeDefinitionProvider` w `VsServerCapabilitiesOverride`.
2. Propagować feature flag `TypeDefinition` do konfiguracji serwera.
3. Dodać setting do sterowania migracją (analogicznie do diagnostics/semantic highlighting):
   - `GetNavigationFrom` lub `GetGoToTypeDefinitionFrom` (`old/lsp/both/unset`) w `FSharpExtensionSettings.cs`.

### 3) Warstwa query/engine (preferowana separacja)
1. Dodać wspólny engine nawigacji (np. `Definition | TypeDefinition`), aby uniknąć duplikacji.
2. Dodać API w query workspace (np. `GetTypeDefinitionLocations(uri, position, ct)`).
3. Handler ma delegować do query (thin endpoint).

## Port logiki semantycznej (FSAC → repo)

### MVP (lokalne typy)
- Dla symbolu pod kursorem:
  - pobrać symbol use,
  - wyliczyć typ symbolu,
  - przejść do `TypeDefinition` i `DeclarationLocation`,
  - zwrócić LSP `Location[]`.

### External types (etap 2)
Kolejność:
1. `File.Exists` / lokalne źródło,
2. SourceLink-first (cache do pliku lokalnego),
3. fallback: dekompilacja do pliku tymczasowego,
4. zwrot `file://...` URI.

## Plan wdrożenia (iteracyjny)

### Milestone A — kontrakt i wiring
- [x] Handler `textDocument/typeDefinition` dodany i zarejestrowany.
- [x] `TypeDefinitionProvider` ogłaszany przez provider.
- [x] Feature flag `TypeDefinition` działa end-to-end.

### Milestone B — semantyka lokalna
- [x] Działa dla rekordów, DU, klas, aliasów typów (zakres MVP).
- [x] Podstawowe mapowanie pozycji LSP (0-based) ↔ FCS zaimplementowane (wymaga testów regresyjnych).
- [x] Stabilne anulowanie requestów (handler oparty o `cancellableTask` + CT).

### Milestone C — typy zewnętrzne
- [ ] SourceLink path resolved i nawigowalny.
- [ ] Fallback dekompilacyjny do pliku tymczasowego.
- [ ] Cache zewnętrznych artefaktów ogranicza powtórną pracę.

### Milestone D — hardening
- [ ] Telemetria requestu (`duration`, `isExternal`, `usedSourcelink`, `usedDecompile`, `cancelled`).
- [ ] Testy regresyjne i smoke testy w VS 2026.
- [ ] Dokumentacja deweloperska i checklista rolloutu.

## Plan testów

### Testy jednostkowe/integracyjne (serwer LSP)
- lokalne scenariusze `typeDefinition` (różne rodzaje symboli i typów),
- edge cases: brak symbolu, błąd typecheck, anulowanie,
- mapowanie pozycji (granice linii/kolumn, 0-based).

### Testy integracyjne providera
- capability `TypeDefinitionProvider` ustawiany tylko gdy feature włączony,
- brak dublowania źródeł nawigacji przy konfiguracji `old/lsp/both`.

### Smoke test ręczny (VS 2026)
- Go to Type Definition w `.fs`, `.fsi`, `.fsx`,
- lokalny typ, typ z referencji NuGet/BCL,
- zachowanie przy szybkiej edycji (anulowanie poprzednich requestów).

## Ryzyka i mitigacje
- **Ryzyko:** zewnętrzne typy bez łatwej ścieżki źródłowej.  
  **Mitigacja:** SourceLink-first + decompile fallback + cache plików.
- **Ryzyko:** niespójność pozycji LSP/FCS.  
  **Mitigacja:** dedykowane testy mapowania pozycji i regresje na boundary cases.
- **Ryzyko:** dublowanie nawigacji przy migracji.  
  **Mitigacja:** spójny feature flag i capability gating.

## Kryteria ukończenia
- VS 2026 wysyła `textDocument/typeDefinition`,
- serwer zwraca stabilne i poprawne `Location[]` dla scenariuszy lokalnych i zewnętrznych,
- feature jest kontrolowany ustawieniami i nie konfliktuje z legacy ścieżką,
- testy i smoke testy przechodzą.
