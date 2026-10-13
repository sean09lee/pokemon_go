namespace TeamBuilder.Services.Contracts;

public interface IPvpApiService
{
    Task<string> GetPvpData();
}
