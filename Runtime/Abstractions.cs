namespace Exerussus.GameSharing.Runtime
{    
    public interface IInjectable
    {
        public void Inject(GameShare gameShare);
    }
    
    public interface IGameSharable
    {
        public void ShareWith(GameShare gameShare);
    }
}