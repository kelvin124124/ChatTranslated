Main problem:
Lingua detector confidently classify English acronums like "uwu", "I gtg" or simple texts like "hihi" as NOT English, since classified as unknown language to user, hence translated.

Proposed solution:
Create a system centered on calculated confidence level for the message.
- Criteria for confidence level includes
    - Score calculated based on cached validated language from chat channel, lower when validation time is old, capped at ? minutes
[Logic: usually in conversations, people tend to use the same language, so if a language was recently validated in the channel, it's more likely that new messages are in the same language]
    - Length of message [Lingua works better with longer texts]
    - Lingua confidence score for the message, higher is better
    - Relationship: Confidence = ???

Based on the calcculated confidence level, 3 different actions will be taken:
- High confidence: message is likely in the same language as previous messages in the channel, react accordingly, no further action needed (if classified as known -> skip, unknown -> translate)
- Medium confidence: message is somewhat likely in the same language, but not certain, consolt Google translate for opinion, delay translation until proven necessary (Google translate language detection ->> if classified as known -> skip, unknown -> translate)
- Low confidence: message is likely in a different language, translate and consult Google translate, wait for both results to come back
   , if turns out translation not required, discard translation, otherwise output translation (Google translate language detection + translation ->> if classified as known -> abort, unknown -> output translation)

Other problems:
Simplify large chunks of Dictionary in LinguaDetector
Language tab DrawKnownLanguagesSelection too uglt: empty label with text above
Give a better name to EnsureModelsAvailableAsync
