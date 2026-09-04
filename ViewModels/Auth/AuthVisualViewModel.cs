namespace BookShelf.ViewModels.Auth;

public class AuthVisualViewModel
{
    public string Eyebrow { get; set; } = "";
    public string Heading { get; set; } = "";
    public string Copy { get; set; } = "";
    public bool ShowStats { get; set; } = false;

    // Stats are optional and page-specific — pass whatever's relevant,
    // don't reuse the same three numbers on every auth page.
    public string Stat1Value { get; set; } = "";
    public string Stat1Label { get; set; } = "";
    public string Stat2Value { get; set; } = "";
    public string Stat2Label { get; set; } = "";
    public string Stat3Value { get; set; } = "";
    public string Stat3Label { get; set; } = "";
}
