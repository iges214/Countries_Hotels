namespace NoName_Hotel.Common.QuerableXtensions.Filtering;

public abstract class BaseFilterParameters
{
    public string? Search { get; set; }
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; } = false;

}

