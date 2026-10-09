using OpenConquer.Content.Magic;
using OpenConquer.Content.Text;
using OpenConquer.Rendering.Sprites;

namespace OpenConquer.Client.UI.Hud.StatusHints.Magic;

internal readonly record struct MainHudMagicHintTextGroup(ReadOnlyMemory<byte> EncodedText, SpriteColor Color);

internal sealed class MainHudMagicHintTextBuilder
{
    private const int ShortFormatLimit = 255;
    private const int RequirementFormatLimit = 511;

    private const int ExperienceStringId = 10019;
    private const int ElementaryLevelStringId = 11038;
    private const int SkillLevelStringId = 11039;
    private const int FixedStringId = 11040;

    private const uint MountMagicType = 7001;

    private static readonly SpriteColor s_warningColor = new(255, 0, 0, 255);

    private readonly MagicEffectFile _effects;
    private readonly ClientStringResources _numericStrings;
    private readonly ClientKeyedStringResources _keyedStrings;
    private readonly SubProfessionInfoFile _subprofessions;

    public MainHudMagicHintTextBuilder(MagicEffectFile effects, ClientStringResources numericStrings, ClientKeyedStringResources keyedStrings, SubProfessionInfoFile subprofessions)
    {
        ArgumentNullException.ThrowIfNull(effects);
        ArgumentNullException.ThrowIfNull(numericStrings);
        ArgumentNullException.ThrowIfNull(keyedStrings);
        ArgumentNullException.ThrowIfNull(subprofessions);

        _effects = effects;
        _numericStrings = numericStrings;
        _keyedStrings = keyedStrings;
        _subprofessions = subprofessions;
    }

    public IReadOnlyList<MainHudMagicHintTextGroup> Build(MagicTypeRecord record, MainHudLearnedMagic learned, MainHudMagicHintRuntimeSnapshot runtime)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(runtime);

        if (record.Type != learned.Type || record.Level != learned.Level)
        {
            throw new ArgumentException("Learned magic and its static record must identify the same type and level.", nameof(learned));
        }

        List<MainHudMagicHintTextGroup> groups = new(6);
        ReadOnlyMemory<byte> name = _effects.GetEncoded(record.Type, record.Level, MagicEffectTextKind.Name);

        Add(groups, name, SpriteColor.White);

        Add(groups, record.Level == 0
            ? _numericStrings.GetEncoded(ElementaryLevelStringId)
            : NativeMagicStringFormatter.Format(_numericStrings.GetEncoded(SkillLevelStringId).Span, ShortFormatLimit, record.Level),
            SpriteColor.White);

        AppendSubprofessionRequirement(groups, record, name, runtime);
        AppendDescription(groups, record, runtime);
        AppendProgress(groups, record, learned, runtime);
        AppendDanceWarning(groups, record, runtime);

        return groups;
    }

    private void AppendSubprofessionRequirement(List<MainHudMagicHintTextGroup> groups, MagicTypeRecord record, ReadOnlyMemory<byte> magicName, MainHudMagicHintRuntimeSnapshot runtime)
    {
        if (record.RequiredSubprofessionPhase == 0 || !_subprofessions.TryGetTitle(record.RequiredSubprofessionClass, out ReadOnlyMemory<byte> title))
        {
            return;
        }

        uint requiredPhase = record.RequiredSubprofessionPhase;
        uint requiredClass = record.RequiredSubprofessionClass;

        bool learnedClass = runtime.HasLearnedSubprofession(requiredClass);
        bool activeClassMatches = runtime.ActiveSubprofessionClass >= 0 && (uint)runtime.ActiveSubprofessionClass == requiredClass;
        bool sufficientPhase = runtime.ActiveSubprofessionPhase >= 0 && (uint)runtime.ActiveSubprofessionPhase >= requiredPhase;

        string key;
        SpriteColor color;
        object[] arguments;

        if (learnedClass && activeClassMatches && sufficientPhase)
        {
            key = "STR_MAGIC_REQ_SUBPRO_ACCORD";
            color = SpriteColor.White;
            arguments = [(int)requiredPhase, title];
        }
        else if (learnedClass && activeClassMatches)
        {
            key = "STR_MAGIC_REQ_SUBPRO_UNACCORD_STEP";
            color = s_warningColor;
            arguments = [(int)requiredPhase, title, (int)requiredPhase, title, magicName];
        }
        else
        {
            key = "STR_MAGIC_SUBPRO_UNACCORD_SWITCH";
            color = s_warningColor;
            arguments = [(int)requiredPhase, title, (int)requiredPhase, title, magicName];
        }

        ReadOnlyMemory<byte> template = _keyedStrings.GetEncoded(key);

        if (!template.IsEmpty)
        {
            Add(groups, NativeMagicStringFormatter.Format(template.Span, RequirementFormatLimit, arguments), color);
        }
    }

    private void AppendDescription(List<MainHudMagicHintTextGroup> groups, MagicTypeRecord record, MainHudMagicHintRuntimeSnapshot runtime)
    {
        if (record.Type != MountMagicType)
        {
            Add(groups, _effects.GetEncoded(record.Type, record.Level, MagicEffectTextKind.ExtendedDescription), SpriteColor.White);
            return;
        }

        MainHudEquippedMount? mount = runtime.EquippedMount;

        if (mount is null)
        {
            Add(groups, NativeMagicStringFormatter.TranslateEscapes(_keyedStrings.GetEncoded("STR_MOUNT_MAGIC_TIP_NO_EQUITMENT").Span), SpriteColor.White);
            return;
        }

        ReadOnlyMemory<byte> template = _keyedStrings.GetEncoded("STR_MOUNT_MAGIC_TIP");

        if (template.IsEmpty)
        {
            return;
        }

        byte[] formatted = NativeMagicStringFormatter.Format(template.Span, ShortFormatLimit, mount.EncodedDisplayName, mount.LineageLevel);

        Add(groups, NativeMagicStringFormatter.TranslateEscapes(formatted), SpriteColor.White);
    }

    private void AppendProgress(List<MainHudMagicHintTextGroup> groups, MagicTypeRecord record, MainHudLearnedMagic learned, MainHudMagicHintRuntimeSnapshot runtime)
    {
        if (record.RequiredExperience == 0)
        {
            Add(groups, _numericStrings.GetEncoded(FixedStringId), SpriteColor.White);
            return;
        }

        if (record.RequiredCharacterLevel > runtime.CharacterLevel)
        {
            ReadOnlyMemory<byte> description = _effects.GetEncoded(record.Type, record.Level, MagicEffectTextKind.Description);

            if (!description.IsEmpty)
            {
                Add(groups, NativeMagicStringFormatter.Format(description.Span, ShortFormatLimit), SpriteColor.White);
            }

            return;
        }

        float multipliedExperience = (float)((double)learned.CurrentExperience * 100.0);
        double percent = (double)multipliedExperience / record.RequiredExperience;

        Add(groups, NativeMagicStringFormatter.Format(_numericStrings.GetEncoded(ExperienceStringId).Span, ShortFormatLimit, percent), SpriteColor.White);
    }

    private void AppendDanceWarning(List<MainHudMagicHintTextGroup> groups, MagicTypeRecord record, MainHudMagicHintRuntimeSnapshot runtime)
    {
        if (!IsDanceSkill(record.Type))
        {
            return;
        }

        if (record.Type is >= 1415 and <= 1419 && runtime.ActiveSubprofessionClass != 6)
        {
            Add(groups, _keyedStrings.GetEncoded("STR_NOT_COOL_DANCE_STATE"), s_warningColor);
        }
    }

    private static bool IsDanceSkill(uint type)
    {
        return type is 1380 or 1385 or 1390 or 1395 or 1400 or 1405 or >= 1410 and <= 1423;
    }

    private static void Add(List<MainHudMagicHintTextGroup> groups, ReadOnlyMemory<byte> text, SpriteColor color)
    {
        if (!text.IsEmpty)
        {
            groups.Add(new MainHudMagicHintTextGroup(text, color));
        }
    }
}
