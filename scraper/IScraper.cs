using System.Threading.Tasks;

namespace RomeScraper.Scraper 
{
    public interface IScraper 
    {
        // bool IsRunning {get;}
        // string Name {get;}

        Task StartAsync();
        Task<bool> Verify();

        Task TestSubscriptions();

        Task TestTxHistory();

        Task TestGetTx();
    }
}