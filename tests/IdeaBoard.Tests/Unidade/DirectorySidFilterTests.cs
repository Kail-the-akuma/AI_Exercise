using System.Globalization;
using System.Runtime.Versioning;
using System.Security.Principal;
using IdeaBoard.App.Services;
using Xunit;

namespace IdeaBoard.Tests.Unidade;

/// <summary>
/// The search filter the directory lookup is made with.
///
/// What this can and cannot establish, stated plainly rather than implied. There is no
/// domain on the machine these tests run on, so NOTHING here proves that a directory
/// accepts the filter or answers it. What it does prove is that the bytes written into the
/// filter are exactly the security identifier's binary form and nothing else, which is the
/// part that can be wrong silently.
///
/// The end to end behaviour against a real domain controller stays on the manual
/// verification list, and it is the only place it can live.
/// </summary>
[SupportedOSPlatform("windows")]
public class DirectorySidFilterTests
{
    #region Constants

    /// <summary>A security identifier of the shape a real domain issues.</summary>
    private const string RealSid = "S-1-5-21-2901470336-2226362600-3229698106-1001";

    #endregion


    #region Shape

    /// <summary>
    /// The filter asks about objectSid and is made only of escaped bytes. No text taken
    /// from the request reaches it, which is what makes the question of what a value from
    /// a claim could do inside a filter not arise.
    /// </summary>
    [Fact]
    public void TheFilterIsMadeOnlyOfEscapedBytes()
    {
        string filter = DirectoryDisplayNameResolver.BuildSidFilter(new SecurityIdentifier(RealSid));

        Assert.StartsWith("(objectSid=", filter, StringComparison.Ordinal);
        Assert.EndsWith(")", filter, StringComparison.Ordinal);

        string body = filter["(objectSid=".Length..^1];

        Assert.NotEmpty(body);

        // Every byte is a backslash and exactly two lowercase hexadecimal digits.
        Assert.Equal(0, body.Length % 3);

        for (int offset = 0; offset < body.Length; offset += 3)
        {
            Assert.Equal('\\', body[offset]);
            Assert.True(IsLowerHex(body[offset + 1]), $"'{body[offset + 1]}' is not a hex digit.");
            Assert.True(IsLowerHex(body[offset + 2]), $"'{body[offset + 2]}' is not a hex digit.");
        }
    }


    /// <summary>
    /// The escaped bytes are the security identifier itself.
    ///
    /// Decoding them back and rebuilding the identifier is what makes this a real check
    /// rather than a restatement of the code: if the encoding dropped a byte, reversed the
    /// order of a sub-authority or wrote the wrong width, the value that comes back would
    /// differ or would not parse at all.
    /// </summary>
    [Theory]
    [InlineData(RealSid)]
    [InlineData("S-1-5-21-1-2-3-500")]
    [InlineData("S-1-1-0")]
    [InlineData("S-1-5-32-544")]
    public void TheEscapedBytesDecodeBackToTheSameSecurityIdentifier(string sid)
    {
        SecurityIdentifier original = new SecurityIdentifier(sid);

        string filter = DirectoryDisplayNameResolver.BuildSidFilter(original);
        string body = filter["(objectSid=".Length..^1];

        byte[] decoded = new byte[body.Length / 3];

        for (int index = 0; index < decoded.Length; index++)
        {
            string pair = body.Substring((index * 3) + 1, 2);
            decoded[index] = byte.Parse(pair, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        SecurityIdentifier rebuilt = new SecurityIdentifier(decoded, 0);

        Assert.Equal(original, rebuilt);
        Assert.Equal(sid, rebuilt.Value);
    }


    /// <summary>
    /// The first bytes are the revision, the sub-authority count and the identifier
    /// authority, in that order. This pins the layout rather than trusting that a
    /// round-trip through the same library would catch a systematic mistake.
    /// </summary>
    [Fact]
    public void TheFirstBytesAreTheRevisionTheCountAndTheAuthority()
    {
        string filter = DirectoryDisplayNameResolver.BuildSidFilter(new SecurityIdentifier(RealSid));

        // Revision 1, five sub-authorities, authority 5: S-1-5-21-x-y-z-1001.
        Assert.StartsWith(
            "(objectSid=\\01\\05\\00\\00\\00\\00\\00\\05",
            filter,
            StringComparison.Ordinal);
    }

    #endregion


    #region Helpers

    private static bool IsLowerHex(char value)
    {
        if (value >= '0' && value <= '9')
        {
            return true;
        }

        return value >= 'a' && value <= 'f';
    }

    #endregion
}
