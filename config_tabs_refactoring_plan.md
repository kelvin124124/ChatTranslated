# Config Tabs Refactoring Plan

## Goals
- Shorten file length for easier reading
- Extract common patterns without adding complexity
- Keep the code maintainable and clear

## Identified Refactoring Opportunities

### 1. **Extract Common UI Helper Methods** (High Impact)
**Files affected:** OpenAISettings.cs (102 lines), OpenAICompatibleSettings.cs (120 lines)

**Issue:** Both files have repetitive patterns for:
- Drawing API key validation status indicators
- Drawing input fields with Apply buttons
- Drawing tooltips with "?" indicators

**Solution:** Create `ConfigUIHelpers.cs` with methods like:
- `DrawApiKeyWithValidation()` - draws input + validation indicator
- `DrawInputWithApplyButton()` - draws input + apply button
- `DrawTooltipIcon()` - draws "?" with tooltip

**Benefit:** Reduces ~30 lines per file, increases consistency

---

### 2. **Extract Chat Type Constants** (Medium Impact)
**File affected:** ChatTab.cs (137 lines)

**Issue:** Three large static arrays (40 lines) defining chat types

**Solution:** Create `ChatTypeConstants.cs` with all chat type arrays

**Benefit:** Reduces file from 137 → ~100 lines, separates data from logic

---

### 3. **Extract Language Constants** (Medium Impact)
**Files affected:** LanguagesTab.cs (145 lines), GeneralTab.cs (91 lines)

**Issue:**
- LanguagesTab has supported languages array (9 lines)
- GeneralTab has language→culture code mapping (13 lines)

**Solution:** Create `LanguageConstants.cs` with:
- Supported languages array
- Language to culture code mapping dictionary
- Helper method `GetCultureCode(string langName)`

**Benefit:** Reduces repetition, centralizes language data

---

### 4. **Split LanguagesTab into Sections** (Medium Impact)
**File affected:** LanguagesTab.cs (145 lines)

**Issue:** File handles both source and target language configuration

**Solution:** Create two files:
- `SourceLanguageSettings.cs` - source language selection logic
- `TargetLanguageSettings.cs` - target language + custom language logic
- Keep `LanguagesTab.cs` as coordinator

**Benefit:** Reduces main file to ~30 lines, each section ~60-70 lines

---

### 5. **Extract Custom Target Language Section** (Low Impact)
**File affected:** LanguagesTab.cs (145 lines)

**Issue:** Custom target language section is 37 lines within DrawTargetLangSelection

**Solution:** Create `CustomTargetLanguageEditor.cs` (similar to CustomPromptEditor)

**Benefit:** Further reduces TargetLanguageSettings if we do #4

---

### 6. **Create Common Validation Helper** (Low Impact)
**Files affected:** OpenAISettings.cs, OpenAICompatibleSettings.cs

**Issue:** Both files have nearly identical ValidateApiKey methods (~25 lines each)

**Solution:** Create `ApiKeyValidator.cs` with shared validation logic

**Benefit:** Reduces duplication by ~25 lines total

---

## Recommended Implementation Order

### Phase 1 - Quick Wins (Recommended to approve)
1. Extract Chat Type Constants → ChatTypeConstants.cs
2. Extract Language Constants → LanguageConstants.cs
3. Extract Common UI Helpers → ConfigUIHelpers.cs

**Impact:** ~60-80 lines reduced across files with minimal complexity increase

### Phase 2 - Structural (Optional)
4. Split LanguagesTab into Source/Target sections
5. Extract Custom Target Language Editor
6. Create API Key Validator helper

**Impact:** Additional ~40-60 lines reduced, better organization

---

## File Size After Phase 1

| File | Current | After Phase 1 | Reduction |
|------|---------|---------------|-----------|
| LanguagesTab.cs | 145 | ~130 | ~15 lines |
| ChatTab.cs | 137 | ~100 | ~37 lines |
| OpenAISettings.cs | 102 | ~80 | ~22 lines |
| OpenAICompatibleSettings.cs | 120 | ~95 | ~25 lines |
| GeneralTab.cs | 91 | ~80 | ~11 lines |

**New files created:**
- ConfigUIHelpers.cs (~40 lines)
- ChatTypeConstants.cs (~45 lines)
- LanguageConstants.cs (~35 lines)

**Total:** ~120 lines of new shared code, ~110 lines removed from existing files

---

## Complexity Assessment

**Phase 1 Changes:**
- ✅ No complexity increase - just extracting constants and simple helpers
- ✅ Makes code MORE readable by removing visual clutter
- ✅ Improves maintainability by centralizing common patterns
- ✅ No changes to logic flow or behavior

**Phase 2 Changes:**
- ⚠️ Slight increase in file count (split LanguagesTab)
- ✅ Each file becomes more focused and easier to understand
- ✅ Similar pattern to TranslationEngine split you already approved

---

## Recommendation

**Approve Phase 1** - Low risk, high value, no complexity increase
**Review Phase 2** - After seeing Phase 1 results

---

## Example Code Snippets

### Before (OpenAISettings.cs - lines 30-50)
```csharp
ImGui.TextUnformatted(Resources.OpenAIAPIKey);
if (OpenAIApiKeyValid.HasValue)
{
    ImGui.SameLine();
    ImGui.TextColored(OpenAIApiKeyValid.Value ? new Vector4(0, 1, 0, 1) : new Vector4(1, 0, 0, 1),
                     OpenAIApiKeyValid.Value ? "✓ valid" : "✗ invalid");
}
else
{
    _ = ValidateApiKey(OpenAIApiKeyInput);
}

ImGui.InputText("##APIKey", ref OpenAIApiKeyInput, 200);
ImGui.SameLine();
if (ImGui.Button(Resources.Apply + "###OpenAI_API_Key"))
{
    configuration.OpenAI_API_Key = OpenAIApiKeyInput;
    configuration.Save();
    Plugin.OutputChatLine($"OpenAI API Key {configuration.OpenAI_API_Key[..12]}... saved successfully.");
    _ = ValidateApiKey(OpenAIApiKeyInput);
}
```

### After (using ConfigUIHelpers)
```csharp
ConfigUIHelpers.DrawApiKeyInput(
    label: Resources.OpenAIAPIKey,
    apiKeyInput: ref OpenAIApiKeyInput,
    validationStatus: OpenAIApiKeyValid,
    onApply: () => {
        configuration.OpenAI_API_Key = OpenAIApiKeyInput;
        configuration.Save();
        Plugin.OutputChatLine($"OpenAI API Key {configuration.OpenAI_API_Key[..12]}... saved successfully.");
    },
    onValidate: () => ValidateApiKey(OpenAIApiKeyInput),
    buttonId: "OpenAI_API_Key"
);
```

**Result:** 20 lines → 11 lines, clearer intent, reusable pattern
