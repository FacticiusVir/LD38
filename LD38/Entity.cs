using System;
using System.Collections.Generic;

namespace LD38
{
    public class Entity
    {
        private readonly Dictionary<Type, EntityComponent> components = new Dictionary<Type, EntityComponent>();

        private readonly IEntityService manager;
        private readonly IServiceProvider provider;

        public Entity(IEntityService manager, IServiceProvider provider)
        {
            this.manager = manager;
            this.provider = provider;
        }

        public void AddComponent<T>()
            where T : EntityComponent
        {
            var component = this.provider.CreateInstance<T>();

            component.Entity = this;

            this.components.Add(typeof(T), component);

            this.manager.RegisterComponent(component);
        }

        public T GetComponent<T>()
            where T : EntityComponent
        {
            this.components.TryGetValue(typeof(T), out var result);

            return (T)result;
        }
    }
}
