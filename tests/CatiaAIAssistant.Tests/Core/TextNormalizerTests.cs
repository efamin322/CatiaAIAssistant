using CatiaAIAssistant.Core.Text;

namespace CatiaAIAssistant.Tests.Core
{
    public class TextNormalizerTests
    {
        [Theory]
        [InlineData("۰۱۲۳۴۵۶۷۸۹", "0123456789")]
        [InlineData("٠١٢٣٤٥٦٧٨٩", "0123456789")]
        [InlineData("قطر ۲۵٫۵ mm", "قطر 25.5 mm")]
        [InlineData("abc 12", "abc 12")]
        [InlineData("", "")]
        [InlineData(null, "")]
        public void NormalizeDigits_ConvertsPersianAndArabicDigits(string? input, string expected)
            => Assert.Equal(expected, TextNormalizer.NormalizeDigits(input));

        [Fact]
        public void ExtractNumbers_ReadsDecimalsWithBothSeparators()
            => Assert.Equal(new[] { 40.0, 25.5, 12.0, 3.25 }, TextNormalizer.ExtractNumbers("۴۰ در ۲۵.۵ در 12 و 3,25"));

        [Fact]
        public void ExtractNumbers_EmptyForNoDigits()
            => Assert.Empty(TextNormalizer.ExtractNumbers("بدون عدد"));

        [Fact]
        public void TryParseBlockDimensions_ValidPersianPrompt()
        {
            bool ok = TextNormalizer.TryParseBlockDimensions("یک مکعب با ابعاد ۴۰ در ۲۵.۵ در ۱۲ میلی‌متر بساز", out var x, out var y, out var z, out var err);
            Assert.True(ok, err);
            Assert.Equal(40.0, x, 9);
            Assert.Equal(25.5, y, 9);
            Assert.Equal(12.0, z, 9);
        }

        [Theory]
        [InlineData("40 در 25")]              // too few
        [InlineData("1 2 3 4")]               // too many
        [InlineData("0 در 10 در 10")]         // zero (boundary)
        [InlineData("10 در 10 در 10001")]     // above upper bound
        public void TryParseBlockDimensions_RejectsInvalid(string prompt)
        {
            Assert.False(TextNormalizer.TryParseBlockDimensions(prompt, out _, out _, out _, out var err));
            Assert.False(string.IsNullOrWhiteSpace(err));
        }

        [Fact]
        public void TryParseBlockDimensions_AcceptsUpperBoundExactly()
            => Assert.True(TextNormalizer.TryParseBlockDimensions("10000 در 1 در 1", out _, out _, out _, out _));

        [Theory]
        [InlineData("مستطیل ۱*۲", "Rect_1_2")]
        [InlineData("my part:v2?", "my_part_v2")]
        [InlineData("بلوک", "Block")]
        public void SanitizeIdentifier_ProducesSafeAsciiName(string raw, string expected)
            => Assert.Equal(expected, TextNormalizer.SanitizeIdentifier(raw));

        [Fact]
        public void SanitizeIdentifier_FallsBackToPrefixWhenNothingLeft()
            => Assert.Equal("PART", TextNormalizer.SanitizeIdentifier("؟؟؟"));

        [Fact]
        public void SanitizeIdentifier_EmptyInputGetsTimestampedPrefix()
            => Assert.Matches(@"^GEAR_\d{8}_\d{6}$", TextNormalizer.SanitizeIdentifier("  ", "GEAR"));
    }
}
