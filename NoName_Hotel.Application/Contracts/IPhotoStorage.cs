namespace NoName_Hotel.Application.Contracts;

public interface IPhotoStorage
{
    Task<string> SaveAsync(Stream source, CancellationToken cancellationToken);
    void Delete(string url);
}
