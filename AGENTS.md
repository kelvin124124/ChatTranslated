# Copilot Instructions — ChatTranslated

## Build

```bash
dotnet build ChatTranslated.sln
```

This is a Dalamud plugin (FFXIV modding framework) using `Dalamud.NET.Sdk/15.0.0`. The build requires the Dalamud dev environment — `DalamudLibPath` must resolve (typically set by the Dalamud SDK or via environment variable). No linters are configured. The solution contains the main project (`ChatTranslated/ChatTranslated.csproj`) and a test project (`ChatTranslated.Tests/ChatTranslated.Tests.csproj`), both targeting x64 only.

Key NuGet dependencies:
- `GTranslate` (2.3.0) — Google/Bing translator wrappers
- `SearchPioneer.Lingua` (1.0.5) — ML-based language detection

The build has a custom MSBuild target (`StripLinguaLanguageModels`) that runs after `CopyFiles` and deletes all Lingua language model directories except `de;en;es;fr;ja;ko;zh`. If you add a shipped language, update the `Keep` list in the `.csproj`.

There is also a `Secrets` target that writes the `PLOGON_SECRET_cfv5` environment variable into an embedded resource at build time (CI only).

## Architecture

### High-level message flow

```
FFXIV Chat Event (Dalamud)
  │
  ▼
ChatHandler.OnChatMessage()          ← event subscription
  │  filters: enabled?, [CT] prefix?, chat type?, duty?
  │  skips own messages (prints raw to MainWindow)
  ▼
Message(sender, source, SeString)    ← model construction
  │  ExtractText(): walks SeString payloads, skips non-text
  │  Sanitize(): replaces private-use Unicode (\uE000-\uF8FF) with \uFFFD
  ▼
IsFilteredMessage()                  ← pre-translation filters
  │  rejects: <2 chars, no translatable content, pure links,
  │           sound macros (<se.N>), macro spam (<650ms same sender)
  ▼
PhraseFilter.TryFilter()             ← static phrase dictionary
  │  normalizes text (Unicode FormKC, lowercase, collapse repeats, JP corrections)
  │  if matched: returns static translation or swallows emoticons
  │  if matched + known language: prints original, skips translation
  │  outputs detectedIso if phrase language is identified
  ▼
LanguageDetector.ComputeReliabilityAsync()  ← confidence scoring
  │  reliability = clamp((confidence × lengthFactor) + (channelBoost × (1−lengthFactor) × 0.5))
  │
  │  Three-tier decision based on reliability:
  │    < 0.25 (low):  translate AND detect in parallel (Task.WhenAll), use detection result
  │    < 0.40 (mid):  consult Google Translate detection, then decide
  │    ≥ 0.40 (high): trust Lingua's result directly
  ▼
Known/target-language check           ← drops message if detected ISO is in user's KnownLanguages
  │  also drops if ISO matches the current target language (no point translating)
  │  also drops if ISO not in ValidIsoCodes (catches emoticons misclassified as rare langs)
  ▼
TranslationHandler.TranslateMessage()  ← cache check + engine dispatch
  │  cache hit → return immediately
  │  custom language active → MachineTranslate (bypasses engine switch)
  │  engine selected by config:
  │    DeepL engine  → DeeplsTranslate (web scrape)
  │    LLM engine    → LLM_Provider switch:
  │                      0 → LLMProxyTranslate
  │                      1 → OpenAITranslate
  │                      2 → OpenAICompatible → delegates to OpenAITranslate
  ▼
Engine-specific translation          ← each engine has its own fallback chain
  │  DeeplsTranslate → DeepLTranslate (API, if key set) → MachineTranslate
  │  OpenAITranslate → MachineTranslate
  │  LLMProxyTranslate → MachineTranslate
  │  MachineTranslate: Bing → Google → return original text
  ▼
ChatHandler.OutputMessage()          ← dual output
  │  compares normalized original vs translated; skips if identical
  │  formats: "original || translated" or just translated (if HideOriginal)
  │  prints to MainWindow.PrintToOutput()
  │  if ChatIntegration enabled: Plugin.OutputChatLine() into game chat
  ▼
Plugin.OutputChatLine()              ← injects into FFXIV chat
  │  uses SeStringBuilder + PlayerPayload("[CT] sender")
  │  routes to original chat type or Echo channel per config
```

### Entry points

There are four distinct entry points into the translation pipeline, each creating a `Message` with a different `MessageSource`:

| Entry point | MessageSource | Trigger |
|---|---|---|
| `ChatHandler.HandleChatMessage` | `Chat` | Dalamud `ChatMessage` event — all monitored FFXIV chat channels |
| `Plugin.TranslatePF` | `PartyFinder` | Context menu "Translate" on Party Finder listing — reads PF addon memory for description, recruiter name, category tags, and duty name |
| `MainWindow.ProcessInputAsync` | `MainWindow` | User types text in the plugin's main window input field and clicks Translate — also performs reverse translation via MachineTranslate |
| `IpcManager.IpcTranslateText` | `Ipc` | Other Dalamud plugins call `ChatTranslated.Translate` IPC gate with (text, context, targetLanguage) |

### Chat layer (`Chat/`)

**ChatHandler** (`ChatHandler.cs`):
- Constructor subscribes to `Service.chatGui.ChatMessage`; `Dispose()` unsubscribes. (`CheckMessageHandled` was tried but didn't fire reliably in practice — likely suppressed by other plugins or fires too late in the pipeline.)
- Event delegate is `OnHandleableChatMessageDelegate(IHandleableChatMessage)`. Skips when `message.IsHandled` is already true. Forwards `LogKind`, `SourceKind`, `Sender`, `Message` to `HandleChatMessage`.
- `HandleChatMessage` is `async void` (only acceptable for Dalamud event handlers). All work is wrapped in try/catch logging to `Service.pluginLog.Error`.
- Extracts `PlayerPayload` from sender `SeString` for the player name.
- Own messages are detected via `SourceKind == XivChatRelationKind.LocalPlayer` (covers both inbound self-targeted messages and `TellOutgoing`); they are printed raw to MainWindow and skipped.
- Pre-translation filters in `IsFilteredMessage()`:
  - `HasTranslatableContentRegex`: requires at least one CJK character or 2+ letter word.
  - `PureLinkRegex`: rejects messages that are only URLs.
  - `SoundMacroRegex`: rejects `<se.N>` sound macros.
  - `IsMacroMessage`: detects rapid messages from the same sender (<650ms apart). Tracks last message time per sender in a `Dictionary<string, int>` using `Environment.TickCount` (the game-supplied `IChatMessage.Timestamp` is only second-resolution, too coarse for this filter). Evicts entries older than 3 seconds when the dictionary exceeds 20 entries.
- Context extraction (`GetChatMessageContext`): reads the active chat log panel via unsafe pointer access to `AddonChatLogPanel`. Walks the last 300 SeString payloads, extracts text (replacing complex payloads with tags like `[Item]`, `[Quest]`, `[Map]`, `[Status]`). Takes the last 10 lines. Appends game state flags (in-duty, in-combat).
- `GetActiveChatLogPanel`: reads `AddonChatLog->TabIndex` to determine which chat panel is active.

**Message** (`Message.cs`):
- Primary constructor: `Message(string sender, MessageSource source, SeString originalContent, XivChatType type)`.
- Convenience constructor for plain strings: wraps in `SeString(new TextPayload(input))` with `XivChatType.Say`.
- Lazy-initialized properties:
  - `OriginalText`: `OriginalContent.TextValue` (computed once on first access).
  - `CleanedContent`: `Sanitize(ExtractText(OriginalContent))` — walks SeString payloads extracting only `TextPayload` text, skipping complex payload sequences.
- Link-payload handling in `ExtractText`: an `inLink` flag is set when entering an interactive payload (`Player`, `Item`, `Quest`, `MapLink`, `Status`, `PartyFinder`) and cleared when the closing `RawPayload` link-terminator is seen. While `inLink` is true, inner `TextPayload`s (the link's display name) are dropped, preventing duplication. `AutoTranslatePayload` is self-contained (no link terminator) and is skipped via `i += 2`.
- `Sanitize()` replaces FFXIV private-use area characters (`\uE000-\uF8FF`) with the Unicode replacement character `\uFFFD` using `ChatRegex.SpecialCharacterRegex()`.
- Mutable translation state: `TranslatedContent`, `TranslationMode`, `Context`.

**PhraseFilter** (`PhraseFilter.cs`):
- Static class loading an embedded `phrases.json` resource at class init into `Dictionary<string, Entry>` where `Entry` is `record Entry(string language, Dictionary<string, string>? Translations)`.
- `EnTokens`: HashSet of all phrase keys whose language is English — used for English token detection.
- `Normalize(text)` pipeline:
  1. Unicode FormKC normalization
  2. `ToLowerInvariant()`
  3. Early return if exact match in Filter
  4. Trim punctuation (broad set including JP punctuation `！？。～`)
  5. Collapse repeated characters: `(.)\1{2,}` → `$1$1`
  6. Collapse repeated patterns: `(ha){3,}` → `haha`, `(lo){2,}l` → `lol`, `xd+` → `xd`
  7. For short phrases (≤8 chars with spaces): try removing spaces
  8. JP variant normalization switch (e.g., `おつかれさまでした` → `お疲れさまでした`)
- `HasEnToken(text)`: returns false immediately if any CJK character is found. Otherwise tokenizes by punctuation/whitespace, checks each token (and its repeated-char-collapsed form) against `EnTokens`.
- `TryFilter(message, out detectedIso)` logic:
  - Normalizes `CleanedContent`, looks up in `Filter`.
  - If matched: resolves ISO code via `LanguageDetector.NameToIsoCode`, updates channel cache.
  - If no `Translations` dict: swallows message only if it's non-linguistic (no detected ISO), otherwise passes through with detected ISO for the main pipeline to handle.
  - If known language: prints original text to MainWindow, returns true (skip translation).
  - If static translation available for target language: sets `TranslatedContent` and outputs, returns true.
  - Otherwise returns false (phrase matched but no applicable translation — continue to main pipeline).

**ChatRegex** (`ChatRegex.cs`):
- All patterns are `[GeneratedRegex]` on `static partial Regex` methods (source-generated at compile time).
- `AutoTranslateRegex`: matches FFXIV auto-translate markers `\uE040 ... \uE041`.
- `SpecialCharacterRegex`: matches private-use area `\uE000-\uF8FF`.
- `JPWelcomeRegex`, `JPByeRegex`, `JPDomaRegex`: common Japanese greeting/farewell patterns.
- Additional regexes in `ChatHandler` itself: `SoundMacroRegex` (`^<se.\d+>$`), `PureLinkRegex` (pure URL lines), `HasTranslatableContentRegex` (CJK or 2+ letter words).

### Translation layer (`Translate/`)

**TranslationHandler** (`TranslationHandler.cs`):
- Static class. Owns the shared `HttpClient` configured with:
  - `HappyEyeballsCallback` (Dalamud's dual-stack IPv4/IPv6 connector)
  - Automatic decompression (all methods)
  - HTTP/3 default with fallback to lower versions
  - 20-second timeout
- Translation cache: `ConcurrentDictionary<string, string>`, max 120 entries. On overflow, removes the first 50% of keys (FIFO, not LRU). Only caches results that are not from `MainWindow` source and not `MachineTranslate` mode.
- `TranslateMessage(message, targetLanguage?)`:
  1. Check cache by `message.OriginalText`.
  2. Dispatch to engine based on `SelectedTranslationEngine` and `LLM_Provider`.
  3. Set `TranslatedContent` and `TranslationMode` on the message.
  4. Cache the result (if eligible).
  5. Return the message.
- All engine return type: `Task<(string translatedText, TranslationMode? mode)>`.

**DeeplsTranslate** (`DeepLTranslate.cs`) — primary DeepL engine:
- Reverse-engineered DeepL web API (based on DeepLX project).
- Constructs a JSON-RPC request to `https://www2.deepl.com/jsonrpc` with method `LMT_handle_jobs`.
- Anti-bot obfuscation: request ID is random 8.3M–8.4M range × 1000 + 1. Timestamp is adjusted based on count of 'i' characters in the message. The `"method"` key spacing in JSON is varied based on `(id + 5) % 29 == 0 || (id + 3) % 13 == 0`.
- Sets browser-like headers (User-Agent, Origin, Referer, Sec-Fetch-*).
- Handles regional variants: Chinese Simplified → `ZH-HANS`, Chinese Traditional → `ZH-HANT`.
- Fallback: DeeplsTranslate → DeepLTranslate (official API, if key ≠ default) → MachineTranslate.

**DeepLTranslate** (`DeepLTranslate.cs`) — official API:
- POSTs to `https://api-free.deepl.com/v2/translate` with `DeepL-Auth-Key` header.
- Sends FFXIV context hint: `"context": "FFXIV, MMORPG"`.
- Language code resolution via `LanguageDetector.NameToIsoCode` → uppercase ISO.
- Fallback: → MachineTranslate.

**OpenAITranslate** (`LLMTranslate.cs`):
- Configurable base URL (default `https://api.openai.com/v1/chat/completions`), model, and API key.
- System prompt instructs the LLM to:
  1. Output a `#### Reasoning` section (brief FFXIV term analysis)
  2. Output a `#### Translation` section (only translated text)
  3. Preserve formatting/tone, be mindful of FFXIV-specific terms
  4. If already in target language, return without modification
- Context appended as `<context>...</context>` block if enabled.
- Custom prompt support: replaces `{targetLanguage}` placeholder in user's custom prompt string.
- Response parsing: extracts text after `#### Translation` via `TranslationSectionRegex`. If the extracted text has ≥4 lines, takes only the first paragraph (anti-slop measure).
- Detects untranslated output (translated == original) and falls back.
- `temperature = 0.6`, `max_tokens = max(prompt.Length, 80)`.
- Fallback: → MachineTranslate.

**OpenAICompatible** (`LLMTranslate.cs`):
- Thin wrapper that calls `OpenAITranslate.Translate` with the user's custom endpoint (`LLM_API_endpoint`), model (`LLM_Model`), and key (`LLM_API_Key`).

**LLMProxyTranslate** (`LLMTranslate.cs`):
- Plugin-hosted translation proxy at the configured `Proxy_Url` (default `https://cfv5.kelpcc.com`).
- API key loaded from embedded resource `cfv5.secret` at class init (release builds). In `#if DEBUG`, reads from `configuration.Proxy_API_Key` instead.
- Simple JSON payload: `{targetLanguage, message, context}`. Sends key via `x-api-key` header.
- Response: reads `translated` property. Logs `responseTime` if present.
- Strips newlines from translated text.
- Fallback: → MachineTranslate.

**MachineTranslate** (`MachineTranslate.cs`):
- Ultimate fallback engine used by all other engines on failure.
- Lazy-initialized `GoogleTranslator` and `BingTranslator` (from GTranslate library), both sharing `TranslationHandler.HttpClient`.
- Fallback chain: Bing → Google → return original text unchanged (with `null` TranslationMode).
- Each step validates: non-empty, non-whitespace, and different from original.

**LanguageDetector** (`LanguageDetector.cs`):
- Wraps the Lingua library for ML-based language detection plus Google Translate as a supplementary detector.
- Singleton `LinguaLD` instance behind `volatile` field + `Lock _buildLock` for thread-safe rebuild.
- **Language table**: 31 entries mapping display name → `Lingua.Language` enum → ISO 639-1 code. Chinese Simplified and Chinese Traditional both map to `Language.Chinese` / `"zh"`.
- Derived lookup dictionaries: `NameToLingua`, `NameToIsoCode`, `LinguaToIso`, `IsoToNames`, `ValidIsoCodes`.
- **Shipped models** (bundled in build output): en, ja, de, fr, zh, ko, es. Other languages download n-gram models from GitHub on demand (`unigrams.json.br` through `fivegrams.json.br`).
- `GetLinguaResult(text)`: runs `ComputeLanguageConfidenceValues`, returns top result's (score, ISO). Returns `(0.0, null)` for `Language.Unknown`.
- `ComputeReliabilityAsync(text, channel, hasEnTokens)`:
  - Runs `GetLinguaResult` on a thread pool thread.
  - Boosts EN confidence by +0.10 if `hasEnTokens && linguaIso == "en"`.
  - `lengthFactor = clamp((text.Length + CJKcount×2) / 20, 0, 1)` — CJK characters count 3× for length scoring since they carry more semantic weight.
  - `channelBoost = GetChannelBoost(channel, linguaIso, lengthFactor)` — per-channel detection history with exponential decay (half-life 150 seconds, 5-minute expiry). Positive boost when Lingua agrees with recent channel detection, negative when it disagrees.
  - `reliability = clamp((confidence × lengthFactor) + (channelBoost × (1−lengthFactor) × 0.5), 0, 1)`.
- `DetectIsoAsync(message)`: uses `GoogleTranslator.DetectLanguageAsync` as a supplementary detector. Updates channel cache with result.
- `RebuildDetectorAsync()`: collects user's known languages + core shipped languages, downloads missing models, builds new `LanguageDetector` under lock, unloads old models. Called when user changes known languages in settings.
- `IsKnownIsoCode(iso)`: checks if ISO maps to any of the user's `KnownLanguages` via `IsoToNames`.
- `IsTargetIsoCode(iso)`: checks if ISO matches `EffectiveTargetLanguage` via `NameToIsoCode`. Ensures messages already in the target language are dropped even if the target is not in `KnownLanguages`. Note: does not work for custom languages since their names aren't in the 31-language lookup table; this is acceptable because `OutputMessage` skips output when translation equals original.
- `IsKnownLanguageOrMeaningless(text)`: returns true if Lingua detects a known language or `Language.Unknown` (meaningless text like emoji/numbers).

### Service locator (`Utils/Service.cs`)

Static class acting as the global service container. Two categories of members:

**Dalamud-injected** (via `[PluginService]` attribute, populated by `pluginInterface.Create<Service>()` during plugin init):
- `pluginInterface` (IDalamudPluginInterface)
- `chatGui` (IChatGui)
- `gameGui` (IGameGui)
- `contextMenu` (IContextMenu)
- `condition` (ICondition)
- `pluginLog` (IPluginLog)
- `commandManager` (ICommandManager)

**Plugin-owned** (set manually during `Plugin` constructor):
- `plugin` (Plugin)
- `configuration` (Configuration)
- `configWindow` (ConfigWindow)
- `mainWindow` (MainWindow)
- `chatHandler` (ChatHandler)

All members are `internal static` with `null!` default (trusted to be initialized before use).

### UI layer (`Windows/`)

All UI uses Dalamud's ImGui bindings (`Dalamud.Bindings.ImGui`). Rendering happens every frame via `pluginInterface.UiBuilder.Draw += DrawUI` which calls `WindowSystem.Draw()`.

**MainWindow** (`MainWindow.cs`):
- Extends `Dalamud.Interface.Windowing.Window` with `NoScrollbar | NoScrollWithMouse` flags.
- Default size 360×220, applied only on first use (`ImGuiCond.FirstUseEver`).
- **Thread-safe output buffer**: static `StringBuilder sb` + `Lock sbLock`. `PrintToOutput(message)` appends timestamped lines `[HH:mm] message\n` under lock and resets `lastContentHash` to trigger re-render.
- **Smart text wrapping**: On each frame, checks if content hash or field width changed. If so, reflows text using `WordRegex` (CJK-aware: each CJK character is a breakable unit). Uses `ImGui.CalcTextSize` to measure wrapped width. Caches result until next change.
- **Output field**: `ImGui.InputTextMultiline` with `ReadOnly` flag. Buffer size = `max(outputText.Length + 4096, 8192)`.
- **Clipboard**: intercepts Ctrl+C via `ImGui.IsKeyPressed(ImGuiKey.C)` + modifier check, removes soft line breaks on a `Task.Run` thread.
- **Input field**: language selector combo (Japanese/English/German/French) + text input + Translate button + Copy button. Translation runs via `Task.Run(() => ProcessInputAsync(message))` (fire-and-forget).
- **Reverse translation**: after translating user input, performs a second `MachineTranslate.Translate` back to the plugin UI language for verification. Displayed as `RT:` label.

**ConfigWindow** (`ConfigWindow.cs`):
- Two-column layout: left sidebar (200px) with selectable tab names, right content area.
- Tab names are localized via `Resources`.
- Four tabs, each a separate class instance: `GeneralTab`, `LanguagesTab`, `ChatChannelsTab`, `TranslationModeTab`.
- Each tab has a `Draw(Configuration)` method called based on `CurrentTab` index.

**Config tabs** (`ConfigTabs/`):

*GeneralTab* (`GeneralTab.cs`):
- Plugin enable/disable checkbox, enable-in-duty checkbox.
- Chat integration section with indented sub-options (hide original, show colored text, use echo channel) — only shown when chat integration is enabled.
- Plugin UI language selector (8 languages). `SetLanguageCulture()` maps display name → CultureInfo code (e.g., "Japanese" → "ja-JP") and sets `Resources.Culture`.
- `#if DEBUG` block: test input field + "Magic button" for manual language detection testing.

*LanguagesTab* (`LanguagesTab.cs`):
- "My Languages" collapsing header with checkboxes for 31 supported languages. Changing known languages triggers `LanguageDetector.RebuildDetectorAsync()`.
- Target language combo selector (disabled when custom language is active).
- Custom target language: text input + Apply button validating via `Language.TryGetLanguage()` from GTranslate. Link to GTranslate language list. When active, `EffectiveTargetLanguage` returns the custom value and translation is forced through `MachineTranslate` (Bing → Google).

*ChatChannelsTab* (`ChatChannelsTab.cs`):
- Three sub-tabs: Generic Channels, LS (Linkshells 1-8), CWLS (Cross-world Linkshells 1-8).
- Each renders a 2-column grid of checkboxes for `XivChatType` values.
- Default enabled channels: Say, Shout, TellIncoming, Party, Alliance, FreeCompany, NoviceNetwork, Yell, CrossParty, PvPTeam, CustomEmote.

*TranslationModeTab* (`TranslationModeTab.cs`):
- Engine selector combo (DeepL / LLM).
- DeepL mode: shows explanation text + `DeepLSettings` (API key input).
- LLM mode: shows explanation text + context settings + provider radio buttons (LLM Proxy / OpenAI / OpenAI-compatible) + provider-specific settings.

**Translation engine settings** (`ConfigTabs/TranslationEngineTabs/`):

*DeepLSettings*: API key input + Apply button. Static class with `Draw(Configuration)`.

*LLMSettings*: Context toggle checkbox with explanation. Provider radio buttons (0=LLM Proxy, 1=OpenAI, 2=OpenAI-compatible). Static class.

*LLMProxySettings*: Explanation text only in release. `#if DEBUG` block: proxy URL and API key inputs.

*OpenAISettings*: API key input with async validation (hits `/v1/models` endpoint, shows ✓/✗). Model selector combo (gpt-5-mini, gpt-5, gpt-4.1-mini, gpt-4.1, gpt-4o-mini, gpt-4o). Price estimation text. API key security warning. Custom prompt editor.

*OpenAICompatibleSettings*: API endpoint input (with tooltip showing example format), API key input with async validation (detects OpenRouter and adjusts validation endpoint to `/auth/key`), model text input. Custom prompt editor.

*CustomPromptEditor*: Checkbox to enable custom prompts. Multi-line text input (10000 char limit, 200px height). Apply and Reset-to-Default buttons. Default prompt generated by `OpenAITranslate.BuildPrompt("{targetLanguage}", null)`.

### IPC (`IpcManager.cs`)

Registers a Dalamud IPC call gate `ChatTranslated.Translate` with signature `(string originalText, string? context, string targetLanguage) → Task<string>`. Creates a `Message` with `MessageSource.Ipc`, translates via `TranslationHandler.TranslateMessage`, returns `TranslatedContent`.

### Configuration (`Configuration.cs`)

`[Serializable]` class implementing `IPluginConfiguration`. Schema version tracked via `int Version` property (currently 7).

**Version migrations** (run sequentially in `Plugin` constructor):
- `< 5`: Migrates `SelectedChatTypes` to new list format.
- `< 6`: Adds `XivChatType.CustomEmote` to selected chat types.
- `< 7`: Initializes `KnownLanguages` from `SelectedTargetLanguage`.
- Unconditional: resets `Proxy_Url` to `https://cfv5.kelpcc.com` if changed.

**Enums**:
- `TranslationEngine`: `DeepL`, `LLM` — top-level engine selection.
- `TranslationMode`: `MachineTranslate`, `DeepL`, `OpenAI`, `LLMProxy`, `LLM` — tracks which engine actually produced a translation.

**Key properties**: `Enabled`, `ChatIntegration`, `EnabledInDuty`, `SelectedTargetLanguage`, `SelectedMainWindowTargetLanguage`, `SelectedPluginLanguage`, `KnownLanguages`, `SelectedChatTypes`, `LLM_Provider` (0=Proxy, 1=OpenAI, 2=OpenAI-compatible), `UseContext`, `UseCustomPrompt`, `UseCustomLanguage`, `CustomTargetLanguage`, various API keys and endpoints. `EffectiveTargetLanguage` is a computed property returning `CustomTargetLanguage` when `UseCustomLanguage` is active, else `SelectedTargetLanguage`.

`Save()` delegates to `Service.pluginInterface.SavePluginConfig(this)`.

### Plugin lifecycle (`Plugin.cs`)

`Plugin` implements `IDalamudPlugin`. Constructor:
1. Creates `Service` (triggers Dalamud IoC injection via `pluginInterface.Create<Service>()`).
2. Loads configuration (or creates default).
3. Registers IPC gate.
4. Creates windows (ConfigWindow, MainWindow), adds to WindowSystem.
5. Subscribes to UI builder events (Draw, OpenConfigUi, OpenMainUi).
6. Creates `ChatHandler` (subscribes to chat events).
7. Registers context menu item for Party Finder translation.
8. Registers `/pchat` command handler (subcommands: `config`, `on`, `off`, `integration`; no args opens MainWindow).
9. Sets UI language culture.
10. Initializes default chat types if null.
11. Runs configuration migrations.
12. Kicks off async `LanguageDetector.RebuildDetectorAsync()`.

`Dispose()`:
1. Unregisters IPC.
2. Disposes LanguageDetector (unloads models under lock).
3. Disposes shared HttpClient.
4. Removes all windows.
5. Disposes ChatHandler (unsubscribes from chat events).
6. Removes command handler.

### Localization (`Localization/`)

Uses .NET `.resx` resource files with auto-generated `Resources.Designer.cs`. Supported locales: en (base), de-DE, es-ES, fr-FR, ja-JP, ko-KR, zh-CN, zh-TW. Community translations managed via [Weblate](https://hosted.weblate.org/projects/chattranslated/). Access pattern: `Resources.PropertyName` for typed access, `Resources.ResourceManager.GetString(key, Resources.Culture)` for dynamic keys (chat type names, language names).

## Key conventions

### Async patterns

- All I/O (translation, HTTP, language detection) is `async Task`. Use `.ConfigureAwait(false)` on awaits in non-UI code.
- `async void` is only used for two Dalamud event handlers (`HandleChatMessage`, `ProcessInputAsync`) — nowhere else.
- Fire-and-forget UI actions use `Task.Run(() => ...)`.
- Low-confidence language detection runs parallel tasks via `Task.WhenAll`.
- Expensive compute (Lingua detection) is offloaded via `Task.Run(() => GetLinguaResult(text))`.

### Static-heavy design

Most components are static classes (`TranslationHandler`, `PhraseFilter`, `ChatRegex`, `DeepLTranslate`, `DeeplsTranslate`, `MachineTranslate`, `LanguageDetector`, `OpenAITranslate`, `LLMProxyTranslate`, `OpenAICompatible`). Mutable shared state uses `ConcurrentDictionary` (translation cache), `volatile` + `Lock` (language detector singleton), or `Lock` (MainWindow StringBuilder).

### Translation engine fallback chains

Every translation path must degrade gracefully. If an engine throws, catch the exception, log a warning, and fall back to the next tier. The final fallback (`MachineTranslate`) returns the original text unchanged if both Bing and Google fail — never throw from a translation path.

Return type for all engines: `Task<(string translatedText, TranslationMode? mode)>`. Return `null` mode only when returning original text unmodified.

### Regex

All regex patterns use C# source-generated `[GeneratedRegex]` on `partial` methods — never use `new Regex()`. Patterns live in `ChatRegex.cs` (shared patterns), `PhraseFilter.cs` (normalization patterns), `ChatHandler.cs` (message filter patterns), `MainWindow.cs` (word wrapping), and `LLMTranslate.cs` (response parsing).

### ImGui UI

- Config tab classes expose a `Draw(Configuration)` method. Translation engine setting classes use `static Draw(Configuration)`.
- Use local variables for ImGui widget bindings (checkbox, combo), then write back to `configuration` and call `Save()` on change.
- Suffix ImGui IDs with `##UniqueLabel` to avoid ID collisions (e.g., `"##TranslationEngineCombo"`, `"###DeepL_API_Key"`).
- Use `ImGui.BeginDisabled()`/`EndDisabled()` for conditional control states (wrap both calls in matching `if` checks on the same condition).
- Use `ImGui.Indent(20)`/`Unindent(20)` for nested sub-options.
- Tooltips: `ImGui.TextDisabled("?")` followed by `if (ImGui.IsItemHovered()) ImGui.SetTooltip(...)`.
- Debug-only UI goes inside `#if DEBUG` blocks.
- API key inputs use a static field to hold the editing value, only writing to config on Apply button click.

### SeString payloads

When walking `SeString.Payloads`, interactive link payloads (`Player`, `Item`, `Quest`, `MapLink`, `Status`, `PartyFinder`) are followed by formatting payloads, an inner `TextPayload` (the link's visible name), and a closing `RawPayload` link-terminator (`0x27 0x03`). To extract clean text without duplicating the inner name, use an `inLink` flag set on the link-start payload and cleared on the closing `RawPayload`; suppress `TextPayload` emission while the flag is set. `AutoTranslatePayload` is the only exception — it has no link-terminator, so handle it via `i += 2`.

Use `SeStringBuilder` for constructing chat output. `PlayerPayload("[CT] " + sender, 0)` fakes a player payload for chat bubble display.

### Language detection

`LanguageDetector` uses Lingua with a confidence × length scoring formula, boosted by per-channel history (exponential decay with 150-second half-life, 5-minute expiry). Channel boost is positive when Lingua agrees with recent detection on the same chat channel, negative when it disagrees. Core language models (en, ja, de, fr, zh, ko, es) ship with the plugin; others are downloaded on demand from GitHub. Changing known languages triggers an async detector rebuild under lock.

### Configuration versioning

When adding new config properties, always:
1. Add the property with a sensible default to `Configuration`.
2. If migration from old configs is needed, add a version check block in `Plugin` constructor (`if (Service.configuration.Version < N)`).
3. Increment `Version`, call `Save()`.

### Naming

- Private instance fields: `camelCase` (e.g., `lastContentHash`, `inputText`)
- Private static fields/readonly: `PascalCase` (e.g., `TranslationCache`, `Filter`, `EnTokens`)
- Private constants: `UPPER_CASE` (e.g., `MAX_CACHE_SIZE`) or `PascalCase` (e.g., `BaseUrl`, `ModelBaseUrl`)
- Events: `OnPascalCase` (e.g., `OnChatMessage`, `OnContextMenuOpened`)
- Regex methods: suffix `Regex` (e.g., `AutoTranslateRegex()`, `PunctuationTrimRegex()`)
- Config tab classes: suffix `Tab` (e.g., `GeneralTab`, `LanguagesTab`)
- Translation engine classes: suffix `Translate` (e.g., `DeepLTranslate`, `MachineTranslate`)
- Engine settings UI classes: suffix `Settings` (e.g., `DeepLSettings`, `OpenAISettings`)
- Dalamud service properties in `Service.cs`: `camelCase` (e.g., `pluginInterface`, `chatGui`)

### Code style

- 4-space indentation, LF line endings, UTF-8, insert final newline
- Prefer `var` everywhere (`csharp_style_var_*: true:suggestion`)
- Allman brace style (braces on new lines for all constructs)
- File-scoped namespaces (`namespace ChatTranslated;`)
- Collection expressions: `[]` for empty collections, `[.. source]` for spread
- Null-forgiving `null!` for service locator properties that are guaranteed initialized before use
- Primary constructors for data classes (e.g., `Message`)
- `using static ChatTranslated.Configuration;` for convenient `TranslationMode`/`TranslationEngine` access
