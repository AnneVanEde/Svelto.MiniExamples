using Svelto.Common.Internal;

namespace Svelto.ECS.MiniExamples.Turrets
{
    /// <summary>
    /// Iterate all the entities that have matrices and, assuming they are stride objects, set the matrices to the
    /// matrix to the Stride Entity
    /// </summary>
    class SetTransformsEngine : IQueryingEntitiesEngine, IUpdateEngine
    {
        public SetTransformsEngine(ECSStrideEntityManager ecsStrideEntityManager)
        {
            _ECSStrideEntityManager = ecsStrideEntityManager;
        }

        public EntitiesDB entitiesDB { get; set; }

        public void Ready() { }

        public string name => this.TypeName();

        public void Step(in float deltaTime)
        {
            var groups = entitiesDB.FindGroups<EGIDComponent, MatrixComponent>();
            foreach (var ((egids, transforms, count), _) in
                entitiesDB.QueryEntities<EGIDComponent, MatrixComponent>(groups))
            {
                for (int i = 0; i < count; i++)
                {
                    var transformComponent = _ECSStrideEntityManager.GetStrideEntity(egids[i].ID.entityID).Transform;
                    transformComponent.WorldMatrix = transforms[i].matrix;
                    transformComponent.UpdateLocalFromWorld();
                }
            }
        }

        readonly ECSStrideEntityManager _ECSStrideEntityManager;
    }
}