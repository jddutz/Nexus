namespace Nexus.Graphics.Text;

public interface ITextStyleRegistry : IDisposable
{
    ITextStyle GetOrCreate(TextStyleDescription description);
    ITextStyle Get(TextStyleId textstyle);
    void Reset();
}
