namespace LD38
{
    public class EntityComponent
    {
        public Entity Entity
        {
            get;
            internal set;
        }

        public virtual void Initialise()
        {
        }

        public virtual void Start()
        {
        }

        public virtual void Stop()
        {
        }
    }
}