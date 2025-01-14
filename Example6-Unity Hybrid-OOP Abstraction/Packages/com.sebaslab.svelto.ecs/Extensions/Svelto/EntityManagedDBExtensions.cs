using System.Runtime.CompilerServices;
using Svelto.Common;
using Svelto.DataStructures;
using Svelto.ECS.Hybrid;
using Svelto.ECS.Internal;

namespace Svelto.ECS
{
    public static class EntityManagedDBExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MB<T> QueryEntitiesAndIndex<T>
            (this EntitiesDB entitiesDb, EGID entityGID, out uint index) where T : struct, IEntityViewComponent
        {
            if (entitiesDb.QueryEntitiesAndIndexInternal<T>(entityGID, out index, out MB<T> array) == true)
                return array;

            throw new EntityNotFoundException(entityGID, typeof(T));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryQueryEntitiesAndIndex<T>
            (this EntitiesDB entitiesDb, EGID entityGID, out uint index, out MB<T> array)
            where T : struct, IEntityViewComponent
        {
            if (entitiesDb.QueryEntitiesAndIndexInternal<T>(entityGID, out index, out array) == true)
                return true;

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryQueryEntitiesAndIndex<T>
            (this EntitiesDB entitiesDb, uint id, ExclusiveGroupStruct group, out uint index, out MB<T> array)
            where T : struct, IEntityViewComponent
        {
            if (entitiesDb.QueryEntitiesAndIndexInternal<T>(new EGID(id, group), out index, out array) == true)
                return true;

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool QueryEntitiesAndIndexInternal<T>
            (this EntitiesDB entitiesDb, EGID entityGID, out uint index, out MB<T> buffer)
            where T : struct, IEntityViewComponent
        {
            index  = 0;
            buffer = default;
            if (entitiesDb.SafeQueryEntityDictionary<T>(entityGID.groupID, out var safeDictionary) == false)
                return false;

            if (safeDictionary.TryFindIndex(entityGID.entityID, out index) == false)
                return false;

            buffer = (MB<T>) (safeDictionary as ITypeSafeDictionary<T>).GetValues(out _);

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T QueryEntity<T>
            (this EntitiesDB entitiesDb, EGID entityGID) where T : struct, IEntityViewComponent
        {
            var array = entitiesDb.QueryEntitiesAndIndex<T>(entityGID, out var index);

            return ref array[(int) index];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T QueryEntity<T>
            (this EntitiesDB entitiesDb, uint id, ExclusiveGroupStruct group) where T : struct, IEntityViewComponent
        {
            return ref entitiesDb.QueryEntity<T>(new EGID(id, group));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T QueryUniqueEntity<T>
            (this EntitiesDB entitiesDb, ExclusiveGroupStruct group) where T : struct, IEntityViewComponent
        {
            var (entities, entitiescount) = entitiesDb.QueryEntities<T>(@group);

#if DEBUG && !PROFILE_SVELTO
            if (entitiescount == 0)
                throw new ECSException("Unique entity not found '".FastConcat(typeof(T).ToString()).FastConcat("'"));
            if (entitiescount != 1)
                throw new ECSException("Unique entities must be unique! '".FastConcat(typeof(T).ToString())
                                                                          .FastConcat("'"));
#endif
            return ref entities[0];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static MB<T> GetArrayAndEntityIndex<T>
            (this EGIDMapper<T> mapper, uint entityID, out uint index) where T : struct, IEntityViewComponent
        {
            if (mapper._map.TryFindIndex(entityID, out index))
            {
                return (MB<T>) mapper._map.GetValues(out _);
            }

            throw new ECSException("Entity not found");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetArrayAndEntityIndex<T>
            (this EGIDMapper<T> mapper, uint entityID, out uint index, out MB<T> array)
            where T : struct, IEntityViewComponent
        {
            index = default;
            if (mapper._map != null && mapper._map.TryFindIndex(entityID, out index))
            {
                array = (MB<T>) mapper._map.GetValues(out _);
                return true;
            }

            array = default;
            return false;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static AllGroupsEnumerable<T1> QueryEntities<T1>(this EntitiesDB db)
            where T1 :struct, IEntityComponent
        {
            return new AllGroupsEnumerable<T1>(db);
        }
    }
}