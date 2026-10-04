namespace Nexus.Graphics.Text;

public interface ITextStyleRegistry : IDisposable
{
    ITextStyle GetOrCreate(TextStyleDescription description);
    ITextStyle Get(TextStyleId textstyle);
    IEnumerable<ITextStyle> Update(TextStyleId textstyle);
    IEnumerable<ITextStyle> Release(TextStyleId textstyle);

    void Reset();
}
