using System;
using System.Collections.Generic;

namespace LD38
{
    public class EntityService
        : GameService, IEntityService, IUpdatable
    {
        private readonly List<EntityComponent> newComponents = new List<EntityComponent>();
        private readonly IUpdateLoopService updateLoop;
        private readonly IServiceProvider provider;

        public EntityService(IUpdateLoopService updateLoop, IServiceProvider provider)
        {
            this.updateLoop = updateLoop;
            this.provider = provider;
        }

        public override void Start()
        {
            this.updateLoop.Register(this, UpdateStage.PreUpdate);
        }

        public Entity CreateEntity()
        {
            return new Entity(this, this.provider);
        }

        public void RegisterComponent(EntityComponent component)
        {
            this.newComponents.Add(component);
        }

        public void Update()
        {
            for (int componentIndex = 0; componentIndex < this.newComponents.Count; componentIndex++)
            {
                this.newComponents[componentIndex].Initialise();
            }

            for (int componentIndex = 0; componentIndex < this.newComponents.Count; componentIndex++)
            {
                this.newComponents[componentIndex].Start();
            }

            this.newComponents.Clear();
        }
    }
}
