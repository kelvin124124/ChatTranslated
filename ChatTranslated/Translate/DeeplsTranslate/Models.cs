using ProtoBuf;
using System.Collections.Generic;

namespace ChatTranslated.Translate
{
    [ProtoContract]
    public class TextRange
    {
        [ProtoMember(1)] public int Start;
        [ProtoMember(2)] public int End;
    }

    [ProtoContract]
    public class ParticipantId
    {
        [ProtoMember(1)] public int Value;
    }

    [ProtoContract]
    public class EventVersionValue
    {
        [ProtoMember(1)] public int Value;
    }

    [ProtoContract]
    public class EventVersion
    {
        [ProtoMember(1)] public EventVersionValue? Version;
    }

    [ProtoContract]
    public class Language
    {
        [ProtoMember(1)] public string? Code;
    }

    [ProtoContract]
    public class LanguageModel
    {
        [ProtoMember(1)] public string? Value;
    }

    [ProtoContract]
    public class Idle
    {
        [ProtoMember(1)] public EventVersion? EventVersion;
    }

    [ProtoContract]
    public class MetaInfoMessage
    {
        [ProtoMember(1)] public Idle? Idle;
    }

    [ProtoContract]
    public class ConfirmedMessage
    {
        [ProtoMember(1)] public EventVersion? CurrentVersion;
    }

    [ProtoContract]
    public class StartSessionResponse
    {
        [ProtoMember(3)] public string? SessionToken;
    }

    [ProtoContract]
    public class TextChangeOperation
    {
        [ProtoMember(1)] public TextRange? Range;
        [ProtoMember(2)] public string? Text;
    }

    [ProtoContract]
    public class TranslatorMaximumTextLengthValue
    {
        [ProtoMember(1)] public int Max;
    }

    [ProtoContract]
    public class TranslatorSourceLanguagesValue
    {
        [ProtoMember(1)] public List<Language>? SourceLanguages;
    }

    [ProtoContract]
    public class TranslatorTargetLanguagesValue
    {
        [ProtoMember(1)] public List<Language>? TargetLanguages;
    }

    [ProtoContract]
    public class TranslatorRequestedTargetLanguageValue
    {
        [ProtoMember(1)] public Language? TargetLanguage;
    }

    [ProtoContract]
    public class TranslatorLanguageModelValue
    {
        [ProtoMember(1)] public LanguageModel? LanguageModel;
    }

    [ProtoContract]
    public class ParticipantRequest
    {
        [ProtoMember(1)] public AppendMessage? AppendMessage;
    }

    [ProtoContract]
    public class AppendMessage
    {
        [ProtoMember(1)] public List<FieldEvent> Events = [];
        [ProtoMember(2)] public EventVersion? BaseVersion;
    }

    [ProtoContract]
    public class PublishedMessage
    {
        [ProtoMember(1)] public List<FieldEvent>? Events;
        [ProtoMember(2)] public EventVersion? CurrentVersion;
    }

    [ProtoContract]
    public class ParticipantResponse
    {
        [ProtoMember(1)] public ConfirmedMessage? ConfirmedMessage;
        [ProtoMember(3)] public PublishedMessage? PublishedMessage;
        [ProtoMember(4)] public MetaInfoMessage? MetaInfoMessage;
    }

    [ProtoContract]
    public class FieldEvent
    {
        [ProtoMember(1)] public int FieldName;
        [ProtoMember(2)] public TextChangeOperation? TextChangeOperation;
        [ProtoMember(5)] public SetPropertyOperation? SetPropertyOperation;
        [ProtoMember(6)] public ParticipantId? ParticipantId;
    }

    [ProtoContract]
    public class SetPropertyOperation
    {
        [ProtoMember(1)] public int PropertyName;
        [ProtoMember(2)] public TranslatorSourceLanguagesValue? TranslatorSourceLanguagesValue;
        [ProtoMember(3)] public TranslatorTargetLanguagesValue? TranslatorTargetLanguagesValue;
        [ProtoMember(5)] public TranslatorRequestedTargetLanguageValue? TranslatorRequestedTargetLanguageValue;
        [ProtoMember(14)] public TranslatorMaximumTextLengthValue? TranslatorMaximumTextLengthValue;
        [ProtoMember(16)] public TranslatorLanguageModelValue? TranslatorLanguageModelValue;
    }
}
