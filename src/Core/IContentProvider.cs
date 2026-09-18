namespace Nexus.Core;

public interface IContentProvider<TSource>
{
    TSource Get(ContentId id);
}
