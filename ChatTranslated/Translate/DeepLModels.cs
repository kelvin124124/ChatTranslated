using ProtoBuf;
using System.Collections.Generic;

namespace ChatTranslated.Translate
{
    [ProtoContract] internal class TextRange { [ProtoMember(1)] public int Start; [ProtoMember(2)] public int End; }
    [ProtoContract] internal class ParticipantId { [ProtoMember(1)] public int Value; }
    [ProtoContract] internal class EventVersionValue { [ProtoMember(1)] public int Value; }
    [ProtoContract] internal class EventVersion { [ProtoMember(1)] public EventVersionValue? Version; }
    [ProtoContract] internal class Language { [ProtoMember(1)] public string? Code; }
    [ProtoContract] internal class LanguageModel { [ProtoMember(1)] public string? Value; }
    [ProtoContract] internal class Idle { [ProtoMember(1)] public EventVersion? EventVersion; }
    [ProtoContract] internal class MetaInfoMessage { [ProtoMember(1)] public Idle? Idle; }
    [ProtoContract] internal class ConfirmedMessage { [ProtoMember(1)] public EventVersion? CurrentVersion; }
    [ProtoContract] internal class StartSessionResponse { [ProtoMember(3)] public string? SessionToken; }
    [ProtoContract] internal class TextChangeOperation { [ProtoMember(1)] public TextRange? Range; [ProtoMember(2)] public string? Text; }
    [ProtoContract] internal class TranslatorMaximumTextLengthValue { [ProtoMember(1)] public int Max; }
    [ProtoContract] internal class TranslatorSourceLanguagesValue { [ProtoMember(1)] public List<Language>? SourceLanguages; }
    [ProtoContract] internal class TranslatorTargetLanguagesValue { [ProtoMember(1)] public List<Language>? TargetLanguages; }
    [ProtoContract] internal class TranslatorRequestedTargetLanguageValue { [ProtoMember(1)] public Language? TargetLanguage; }
    [ProtoContract] internal class TranslatorLanguageModelValue { [ProtoMember(1)] public LanguageModel? LanguageModel; }
    [ProtoContract] internal class ParticipantRequest { [ProtoMember(1)] public AppendMessage? AppendMessage; }
    [ProtoContract] internal class AppendMessage { [ProtoMember(1)] public List<FieldEvent> Events = []; [ProtoMember(2)] public EventVersion? BaseVersion; }
    [ProtoContract] internal class PublishedMessage { [ProtoMember(1)] public List<FieldEvent>? Events; [ProtoMember(2)] public EventVersion? CurrentVersion; }
    [ProtoContract] internal class ParticipantResponse { [ProtoMember(1)] public ConfirmedMessage? ConfirmedMessage; [ProtoMember(3)] public PublishedMessage? PublishedMessage; [ProtoMember(4)] public MetaInfoMessage? MetaInfoMessage; }

    [ProtoContract]
    internal class FieldEvent
    {
        [ProtoMember(1)] public int FieldName;
        [ProtoMember(2)] public TextChangeOperation? TextChangeOperation;
        [ProtoMember(5)] public SetPropertyOperation? SetPropertyOperation;
        [ProtoMember(6)] public ParticipantId? ParticipantId;
    }

    [ProtoContract]
    internal class SetPropertyOperation
    {
        [ProtoMember(1)] public int PropertyName;
        [ProtoMember(2)] public TranslatorSourceLanguagesValue? TranslatorSourceLanguagesValue;
        [ProtoMember(3)] public TranslatorTargetLanguagesValue? TranslatorTargetLanguagesValue;
        [ProtoMember(5)] public TranslatorRequestedTargetLanguageValue? TranslatorRequestedTargetLanguageValue;
        [ProtoMember(14)] public TranslatorMaximumTextLengthValue? TranslatorMaximumTextLengthValue;
        [ProtoMember(16)] public TranslatorLanguageModelValue? TranslatorLanguageModelValue;
    }
}
