namespace CafeChain.Infrastrusture.Interfaces.Systems
{
    public interface ISystemSettingRepository
    {
        Task<Dictionary<string, string>> GetValuesAsync(IEnumerable<string> keys);
    }
}
