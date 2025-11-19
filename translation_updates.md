# Translation Updates for New UI Tabs

## Summary
Added translations for two new resource strings to all language variants:
- **"Translation Engine"** - The tab for translation engine configuration
- **"Chat"** - The tab for chat integration and channel settings

## Translations Applied

| Language | Translation Engine | Chat | Context |
|----------|-------------------|------|---------|
| **English** (base) | Translation Engine | Chat | Base language |
| **German** (de-DE) | Übersetzungs-Engine | Chat | "Engine" is commonly used in German technical contexts |
| **Spanish** (es-ES) | Motor de traducción | Chat | "Motor" = engine/motor in Spanish |
| **French** (fr-FR) | Moteur de traduction | Chat | "Moteur" = engine in French |
| **Japanese** (ja-JP) | 翻訳エンジン | チャット | エンジン (enjin) = engine in katakana |
| **Korean** (ko-KR) | 번역 엔진 | 채팅 | 엔진 (enjin) = engine in Korean |
| **Chinese (Simplified)** (zh-CN) | 翻译引擎 | 聊天 | 引擎 (yǐnqíng) = engine in simplified Chinese |
| **Chinese (Traditional)** (zh-TW) | 翻譯引擎 | 聊天 | 引擎 = engine in traditional Chinese |

## Translation Rationale

### "Translation Engine"
This term refers to the backend service/API that performs the actual translation:
- **Technical term**: "Engine" is widely understood in technical contexts across languages
- **Context**: Used for the configuration tab where users select between DeepL, OpenAI, or other translation APIs
- **Consistency**: Follows existing pattern (e.g., "Translation Mode" → "翻訳モード" in Japanese)

### "Chat"
This term refers to the in-game chat system:
- **Universal term**: "Chat" is commonly used across languages for messaging systems
- **Context**: Used for the configuration tab where users configure chat integration and select which chat channels to translate
- **Consistency**:
  - Chinese: Uses "聊天" matching existing "聊天頻道" (Chat Channels)
  - Japanese: Uses "チャット" matching game terminology
  - Korean: Uses "채팅" matching existing "채팅 채널" (Chat Channels)
  - European languages: Keep "Chat" as it's universally understood

## Files Updated
All translations maintain consistency with existing terminology in each language variant:

1. ✅ Resources.de-DE.resx
2. ✅ Resources.es-ES.resx
3. ✅ Resources.fr-FR.resx
4. ✅ Resources.ja-JP.resx
5. ✅ Resources.ko-KR.resx
6. ✅ Resources.zh-CN.resx
7. ✅ Resources.zh-TW.resx

## Verification
All translations have been added to match the structure in Resources.resx (base English file).
Each translation follows the existing terminology and style used in that language variant.
