namespace LD38
{
    public interface IEntityService
        : IGameService
    {
        Entity CreateEntity();

        void RegisterComponent(EntityComponent component);
    }
}
