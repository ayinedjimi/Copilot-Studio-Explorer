namespace StudioDataverse;

public record AgentFilter(string Search = "", int Status = -1, int Publication = 0, int Days = 0, int? Language = null, int Managed = -1, int Sort = 0);

public static class AgentQuery
{
    public static List<Agent> Apply(IEnumerable<Agent> source, AgentFilter filter, DateTimeOffset now)
    {
        var terms = filter.Search.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var result = source.Where(x => terms.All(term =>
            x.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            x.Schema.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            x.Id.ToString().Contains(term, StringComparison.OrdinalIgnoreCase)));
        if (filter.Status >= 0) result = result.Where(x => x.State == filter.Status);
        if (filter.Publication > 0) result = result.Where(x => filter.Publication == 1 ? x.PublishedOn.HasValue : !x.PublishedOn.HasValue);
        if (filter.Days > 0) result = result.Where(x => x.ModifiedOn >= now.AddDays(-filter.Days));
        if (filter.Language.HasValue) result = result.Where(x => x.Language == filter.Language);
        if (filter.Managed >= 0) result = result.Where(x => x.Managed == (filter.Managed == 1));
        return (filter.Sort switch
        {
            1 => result.OrderBy(x => x.ModifiedOn).ThenBy(x => x.Id),
            2 => result.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(x => x.Id),
            3 => result.OrderByDescending(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(x => x.Id),
            4 => result.OrderByDescending(x => x.CreatedOn).ThenBy(x => x.Id),
            _ => result.OrderByDescending(x => x.ModifiedOn).ThenBy(x => x.Id)
        }).ToList();
    }
}
