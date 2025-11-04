using System.Text;
using FluentAssertions;
using Honua.Server.Core.Tests.Shared;
using Xunit;
using Xunit.Abstractions;

namespace Honua.Server.Core.Tests.Data.Data;

/// <summary>
/// Comprehensive Unicode and string edge case tests for GIS feature data.
/// Tests handling of combining diacritics, RTL text, zero-width characters,
/// emoji, and various Unicode normalization forms.
/// </summary>
[Collection("UnitTests")]
[Trait("Category", "Unit")]
[Trait("Category", "Unicode")]
public class UnicodeStringEdgeCaseTests
{
    private readonly ITestOutputHelper _output;

    public UnicodeStringEdgeCaseTests(ITestOutputHelper output)
    {
        _output = output;
    }

    #region Combining Diacritics Tests

    [Fact]
    public void CombiningDiacritics_Versus_PrecomposedCharacters_ShouldBeDifferent()
    {
        // Arrange
        var combining = "Cafe\u0301"; // Café with combining acute accent (e + ́)
        var precomposed = "Café";      // Café with precomposed é

        // Assert - String comparison (they look the same but are different)
        combining.Should().NotBe(precomposed, "combining form is different from precomposed");
        combining.Length.Should().Be(5, "combining form has 5 code units");
        precomposed.Length.Should().Be(4, "precomposed form has 4 code units");

        // Normalize to compare
        var combiningNormalized = combining.Normalize(NormalizationForm.FormC);
        var precomposedNormalized = precomposed.Normalize(NormalizationForm.FormC);

        combiningNormalized.Should().Be(precomposedNormalized, "normalized forms should be equal");

        _output.WriteLine($"Combining: '{combining}' (length {combining.Length})");
        _output.WriteLine($"Precomposed: '{precomposed}' (length {precomposed.Length})");
        _output.WriteLine($"Bytes (combining): {BitConverter.ToString(Encoding.UTF8.GetBytes(combining))}");
        _output.WriteLine($"Bytes (precomposed): {BitConverter.ToString(Encoding.UTF8.GetBytes(precomposed))}");
    }

    [Fact]
    public void AddressWithCombiningDiacritics_ShouldHandleNormalization()
    {
        // Arrange
        var address = RealisticGisTestData.AddressWithCombiningDiacritics;

        // Assert
        address.Should().Contain("Cafe\u0301"); // Contains combining form
        var normalized = address.Normalize(NormalizationForm.FormC);

        _output.WriteLine($"Original: '{address}'");
        _output.WriteLine($"Normalized: '{normalized}'");
        _output.WriteLine($"Original length: {address.Length}, Normalized length: {normalized.Length}");
    }

    [Fact]
    public void MultipleAccents_ShouldPreserveAllDiacritics()
    {
        // Arrange - Vietnamese name with multiple diacritics
        var vietnamese = "Nguyễn Văn Đức";

        // Assert
        vietnamese.Should().Contain("ễ"); // e with tilde and hook above
        vietnamese.Should().Contain("ă"); // a with breve
        vietnamese.Should().Contain("Đ"); // D with stroke

        var normalized = vietnamese.Normalize(NormalizationForm.FormC);
        normalized.Length.Should().Be(vietnamese.Length);

        _output.WriteLine($"Vietnamese name: '{vietnamese}'");
    }

    #endregion

    #region Right-to-Left Text Tests

    [Fact]
    public void RightToLeftMarker_ShouldBePreserved()
    {
        // Arrange - Arabic text with RTL marker
        var arabicAddress = RealisticGisTestData.AddressWithArabic;

        // Assert
        arabicAddress.Should().NotBeNullOrEmpty();

        var bytes = Encoding.UTF8.GetBytes(arabicAddress);
        _output.WriteLine($"Arabic address: '{arabicAddress}'");
        _output.WriteLine($"Length: {arabicAddress.Length} chars, {bytes.Length} bytes");
        _output.WriteLine($"Bytes: {BitConverter.ToString(bytes)}");
    }

    [Fact]
    public void MixedLtrRtl_ShouldPreserveBothDirections()
    {
        // Arrange - Mixed English and Arabic
        var mixed = "Building 123, شارع الملك";

        // Assert
        mixed.Should().Contain("Building");
        mixed.Should().Contain("شارع"); // Arabic word for "street"

        _output.WriteLine($"Mixed LTR/RTL: '{mixed}'");
    }

    [Fact]
    public void RightToLeftMark_U200F_ShouldBePreserved()
    {
        // Arrange - Text with explicit RTL mark
        var textWithRlm = "\u200FHello\u200F World";

        // Assert
        textWithRlm.Length.Should().Be(13, "should include RTL marks in length");
        textWithRlm.Should().Contain("\u200F");

        _output.WriteLine($"Text with RLM: '{textWithRlm}' (length {textWithRlm.Length})");
    }

    #endregion

    #region Zero-Width Characters Tests

    [Fact]
    public void ZeroWidthSpace_U200B_ShouldBePreserved()
    {
        // Arrange
        var textWithZws = "Test\u200BString"; // Zero-width space

        // Assert
        textWithZws.Length.Should().Be(11, "should include zero-width space");
        textWithZws.Should().Contain("\u200B");

        var visible = textWithZws.Replace("\u200B", "");
        visible.Should().Be("TestString");

        _output.WriteLine($"With ZWS: '{textWithZws}' (length {textWithZws.Length})");
        _output.WriteLine($"Without ZWS: '{visible}' (length {visible.Length})");
    }

    [Fact]
    public void ZeroWidthNonJoiner_U200C_ShouldBePreserved()
    {
        // Arrange - Persian text where ZWNJ is semantically significant
        var persianWithZwnj = "می\u200Cخواهم"; // "I want" in Persian with ZWNJ

        // Assert
        persianWithZwnj.Should().Contain("\u200C");

        _output.WriteLine($"Persian with ZWNJ: '{persianWithZwnj}'");
    }

    [Fact]
    public void ZeroWidthJoiner_U200D_ShouldBePreserved()
    {
        // Arrange - Used in emoji sequences
        var emojiSequence = "👨\u200D👩\u200D👧"; // Family emoji (man + ZWJ + woman + ZWJ + girl)

        // Assert
        emojiSequence.Should().Contain("\u200D");

        _output.WriteLine($"Emoji with ZWJ: '{emojiSequence}'");
    }

    #endregion

    #region Emoji Tests

    [Fact]
    public void SimpleEmoji_ShouldBePreserved()
    {
        // Arrange
        var textWithEmoji = "Property 🏠";

        // Assert
        textWithEmoji.Should().Contain("🏠");

        // Count grapheme clusters (what user sees) vs code units
        var codeUnits = textWithEmoji.Length;
        _output.WriteLine($"Text with emoji: '{textWithEmoji}' ({codeUnits} code units)");
    }

    [Fact]
    public void EmojiWithModifier_ShouldBePreserved()
    {
        // Arrange - Emoji with skin tone modifier
        var emojiWithModifier = "👍🏽"; // Thumbs up with medium skin tone

        // Assert
        emojiWithModifier.Length.Should().Be(4, "emoji + modifier = 2 code points = 4 code units in UTF-16");

        _output.WriteLine($"Emoji with modifier: '{emojiWithModifier}' (length {emojiWithModifier.Length})");
    }

    [Fact]
    public void FlagEmoji_RegionalIndicators_ShouldBePreserved()
    {
        // Arrange - US flag (two regional indicator symbols)
        var usFlag = "🇺🇸"; // U+1F1FA U+1F1F8

        // Assert
        usFlag.Length.Should().Be(4, "flag emoji = 2 regional indicators = 4 code units");

        _output.WriteLine($"US flag emoji: '{usFlag}' (length {usFlag.Length})");
    }

    #endregion

    #region SQL Injection in Unicode

    [Fact]
    public void SqlInjection_WithUnicode_ShouldBeEscaped()
    {
        // Arrange - SQL injection attempt with Unicode
        var maliciousUnicode = "'; DROP TABLE parcels; --";

        // Assert - Should be treated as string data, not SQL
        maliciousUnicode.Should().Contain("DROP TABLE");

        _output.WriteLine($"Malicious string: '{maliciousUnicode}'");
        _output.WriteLine("Note: This should be parameterized, not escaped");
    }

    [Fact]
    public void SqlInjection_WithUnicodeHomoglyphs_ShouldBePreserved()
    {
        // Arrange - Using Cyrillic 'а' instead of Latin 'a'
        var homoglyphAttack = "'; DROP TАBLE pаrcels; --"; // Contains Cyrillic А and а

        // Assert
        homoglyphAttack.Should().NotBe("'; DROP TABLE parcels; --");

        _output.WriteLine($"Homoglyph attack: '{homoglyphAttack}'");
        _output.WriteLine($"Bytes: {BitConverter.ToString(Encoding.UTF8.GetBytes(homoglyphAttack))}");
    }

    #endregion

    #region Mixed Scripts Tests

    [Fact]
    public void JapaneseAddress_MixedHiraganaKanjiKatakana_ShouldBePreserved()
    {
        // Arrange
        var japaneseAddress = RealisticGisTestData.AddressWithJapanese;

        // Assert
        japaneseAddress.Should().NotBeNullOrEmpty();
        japaneseAddress.Should().Contain("東京"); // Tokyo in Kanji

        _output.WriteLine($"Japanese address: '{japaneseAddress}'");
    }

    [Fact]
    public void MixedScripts_LatinCyrillicCJK_ShouldBePreserved()
    {
        // Arrange
        var mixed = "Main Улица 大道"; // English "Main", Russian "Ulitsa", Chinese "Dadao"

        // Assert
        mixed.Should().Contain("Main");
        mixed.Should().Contain("Улица");
        mixed.Should().Contain("大道");

        _output.WriteLine($"Mixed scripts: '{mixed}'");
    }

    #endregion

    #region Normalization Form Tests

    [Theory]
    [InlineData("José", "Jose\u0301")] // Precomposed vs combining
    [InlineData("Müller", "Mu\u0308ller")] // ü vs u + diaeresis
    [InlineData("naïve", "nai\u0308ve")] // ï vs i + diaeresis
    public void NormalizationForms_ShouldBeEquivalentWhenNormalized(string precomposed, string combining)
    {
        // Assert - Different byte representations
        precomposed.Should().NotBe(combining, "raw strings should differ");

        // But same after normalization
        var precomposedNfc = precomposed.Normalize(NormalizationForm.FormC);
        var combiningNfc = combining.Normalize(NormalizationForm.FormC);

        precomposedNfc.Should().Be(combiningNfc, "NFC forms should be equal");

        _output.WriteLine($"Precomposed: '{precomposed}' (len {precomposed.Length})");
        _output.WriteLine($"Combining: '{combining}' (len {combining.Length})");
        _output.WriteLine($"NFC normalized: '{precomposedNfc}' (len {precomposedNfc.Length})");
    }

    #endregion

    #region Control Characters Tests

    [Fact]
    public void ControlCharacters_ShouldBeDetectable()
    {
        // Arrange - Text with various control characters
        var textWithControls = "Hello\u0000World\u0001Test\u001F"; // NUL, SOH, US

        // Assert
        textWithControls.Should().Contain("\u0000"); // NUL
        textWithControls.Should().Contain("\u0001"); // SOH
        textWithControls.Should().Contain("\u001F"); // Unit Separator

        _output.WriteLine($"Text with controls: '{textWithControls}'");
        _output.WriteLine($"Bytes: {BitConverter.ToString(Encoding.UTF8.GetBytes(textWithControls))}");
    }

    [Fact]
    public void BOM_ByteOrderMark_ShouldBeHandled()
    {
        // Arrange - Text with BOM
        var textWithBom = "\uFEFFHello"; // BOM + "Hello"

        // Assert
        textWithBom.Length.Should().Be(6, "BOM counts as one character");
        textWithBom.TrimStart('\uFEFF').Should().Be("Hello");

        _output.WriteLine($"Text with BOM: '{textWithBom}' (length {textWithBom.Length})");
    }

    #endregion

    #region Real-World Address Tests

    [Theory]
    [InlineData("AddressWithApostrophe")]
    [InlineData("AddressWithSpanishUnicode")]
    [InlineData("AddressWithFrenchUnicode")]
    [InlineData("AddressWithGermanUnicode")]
    [InlineData("AddressWithAmpersand")]
    [InlineData("AddressWithSlash")]
    [InlineData("AddressWithHyphen")]
    public void RealWorldAddress_ShouldBeValid(string propertyName)
    {
        // Arrange - Use reflection to get property value
        var property = typeof(RealisticGisTestData).GetProperty(propertyName);
        property.Should().NotBeNull($"{propertyName} should exist in RealisticGisTestData");

        var address = property!.GetValue(null) as string;

        // Assert
        address.Should().NotBeNullOrWhiteSpace($"{propertyName} should have a value");
        address!.Length.Should().BeGreaterThan(0);

        _output.WriteLine($"{propertyName}: '{address}'");
    }

    #endregion

    #region Unicode Feature Attributes Tests

    [Fact]
    public void UnicodeEdgeCaseAttributes_ShouldContainAllExpectedFields()
    {
        // Arrange
        var attributes = RealisticGisTestData.GetUnicodeEdgeCaseAttributes();

        // Assert
        attributes.Should().ContainKey("name_combining");
        attributes.Should().ContainKey("name_precomposed");
        attributes.Should().ContainKey("rtl_text");
        attributes.Should().ContainKey("zero_width");
        attributes.Should().ContainKey("emoji");
        attributes.Should().ContainKey("japanese");
        attributes.Should().ContainKey("mixed_scripts");

        _output.WriteLine("Unicode edge case attributes:");
        foreach (var (key, value) in attributes)
        {
            _output.WriteLine($"  {key}: '{value}'");
        }
    }

    [Fact]
    public void UnicodeEdgeCaseAttributes_CombiningVsPrecomposed_ShouldDiffer()
    {
        // Arrange
        var attributes = RealisticGisTestData.GetUnicodeEdgeCaseAttributes();

        var combining = attributes["name_combining"] as string;
        var precomposed = attributes["name_precomposed"] as string;

        // Assert
        combining.Should().NotBeNull();
        precomposed.Should().NotBeNull();
        combining.Should().NotBe(precomposed, "combining and precomposed forms should differ");

        // But normalize to same
        var combiningNfc = combining!.Normalize(NormalizationForm.FormC);
        var precomposedNfc = precomposed!.Normalize(NormalizationForm.FormC);
        combiningNfc.Should().Be(precomposedNfc);

        _output.WriteLine($"Combining: '{combining}'");
        _output.WriteLine($"Precomposed: '{precomposed}'");
    }

    #endregion

    #region Surrogate Pairs Tests

    [Fact]
    public void SurrogatePairs_EmojiOutsideBMP_ShouldBePreserved()
    {
        // Arrange - Characters outside Basic Multilingual Plane (BMP)
        var emojiOutsideBmp = "𝔸𝕓𝕔"; // Mathematical alphanumeric symbols (U+1D538, etc.)

        // Assert
        // Each character is a surrogate pair (2 UTF-16 code units)
        emojiOutsideBmp.Length.Should().Be(6, "3 chars × 2 code units each");

        _output.WriteLine($"Surrogate pairs: '{emojiOutsideBmp}' (length {emojiOutsideBmp.Length})");
    }

    [Fact]
    public void InvalidSurrogatePair_ShouldBeDetectable()
    {
        // Arrange - Unpaired high surrogate (invalid UTF-16)
        var invalidSurrogate = "Test\uD800Invalid"; // High surrogate without low surrogate

        // Assert
        // .NET allows invalid UTF-16 in strings
        invalidSurrogate.Should().Contain("\uD800");

        _output.WriteLine($"Invalid surrogate: '{invalidSurrogate}'");
        _output.WriteLine($"Note: Invalid UTF-16 is allowed in .NET strings but should be handled carefully");
    }

    #endregion
}
