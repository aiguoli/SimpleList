using SimpleList.Core.Services;
using System;
using Xunit;

namespace SimpleList.Tests;

public class SharePointStorageProviderTests
{
    [Theory]
    [InlineData(
        " https://Contoso.SharePoint.com/sites/Team/ ",
        "https://contoso.sharepoint.com/sites/Team",
        "sites/contoso.sharepoint.com:/sites/Team")]
    [InlineData(
        "https://contoso.sharepoint.com/",
        "https://contoso.sharepoint.com",
        "sites/contoso.sharepoint.com")]
    [InlineData(
        "https://contoso.sharepoint.com/sites/Research%20and%20Development",
        "https://contoso.sharepoint.com/sites/Research%20and%20Development",
        "sites/contoso.sharepoint.com:/sites/Research%20and%20Development")]
    public void Parse_NormalizesUrlAndBuildsGraphSitePath(
        string input,
        string expectedUrl,
        string expectedGraphPath)
    {
        SharePointSiteAddress result = SharePointSiteAddress.Parse(input);

        Assert.Equal(expectedUrl, result.NormalizedUrl);
        Assert.Equal(expectedGraphPath, result.GraphSitePath);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("http://contoso.sharepoint.com/sites/team")]
    [InlineData("https://example.com/sites/team")]
    [InlineData("https://contoso.sharepoint.com/sites/team?view=1")]
    public void Parse_RejectsInvalidSiteUrl(string? input)
    {
        Assert.Throws<ArgumentException>(() => SharePointSiteAddress.Parse(input));
    }
}
