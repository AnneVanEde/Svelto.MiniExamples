using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Svelto.ECS.Example.Survive.Enemies
{
    public class EnemySpawnerEngine : IQueryingEntitiesEngine, IReactOnSwap<EnemyEntityViewComponent>, IStepEngine
    {
        const int SECONDS_BETWEEN_SPAWNS = 1;
        const int NUMBER_OF_ENEMIES_TO_SPAWN = 12;

        public EnemySpawnerEngine(EnemyFactory enemyFactory, IEntityFunctions entityFunctions)
        {
            _currentWave = -1;
            _numberOfEnemiesToDefeat = 0;

            _entityFunctions = entityFunctions;
            _enemyFactory = enemyFactory;

        }

        public EntitiesDB entitiesDB { private get; set; }
        public void Ready() { _intervaledTick = IntervaledTick(); }
        public void Step() { _intervaledTick.MoveNext(); }
        public string name => nameof(EnemySpawnerEngine);

        public void MovedTo(ref EnemyEntityViewComponent entityComponent, ExclusiveGroupStruct previousGroup, EGID egid)
        {
            //is the enemy dead?
            if (egid.groupID.FoundIn(DeadEnemies.Groups))
            {
                _numberOfEnemiesToDefeat--;
            }
        }

        IEnumerator IntervaledTick()
        {
            //this is of fundamental importance: Never create implementors as Monobehaviour just to hold 
            //data (especially if read only data). Data should always been retrieved through a service layer
            //regardless the data source.
            //The benefits are numerous, including the fact that changing data source would require
            //only changing the service code. In this simple example I am not using a Service Layer
            //but you can see the point.          
            //Also note that I am loading the data only once per application run, outside the 
            //main loop. You can always exploit this pattern when you know that the data you need
            //to use will never change            
            IEnumerator<JSonEnemySpawnData[]> enemiestoSpawnJsons = ReadEnemySpawningDataServiceRequest();
            IEnumerator<JSonEnemyAttackData[]> enemyAttackDataJsons = ReadEnemyAttackDataServiceRequest();
            IEnumerator<JSonWaveData[]> waveDataJsons = ReadWaveDataServiceRequest();

            while (enemiestoSpawnJsons.MoveNext())
                yield return null;
            while (enemyAttackDataJsons.MoveNext())
                yield return null;
            while (waveDataJsons.MoveNext())
                yield return null;

            var enemiestoSpawn = enemiestoSpawnJsons.Current;
            var enemyAttackData = enemyAttackDataJsons.Current;
            var waveData = waveDataJsons.Current;

            var spawningTimes = new float[enemiestoSpawn.Length];

            for (int i = 0; i < enemiestoSpawn.Length; i++)
            {
                spawningTimes[i] = enemiestoSpawn[i].enemySpawnData.spawnTime;
            }

            while (true)
            {
                //Svelto.Tasks can yield Unity YieldInstructions but this comes with a performance hit
                //so the fastest solution is always to use custom enumerators. To be honest the hit is minimal
                //but it's better to not abuse it.                
                var waitForSecondsEnumerator = new WaitForSecondsEnumerator(SECONDS_BETWEEN_SPAWNS);
                while (waitForSecondsEnumerator.MoveNext())
                    yield return null;

                if (_numberOfEnemiesToDefeat == 0)
                {
                    _currentWave++;
                    for (int enemyType = 0; enemyType < enemiestoSpawn.Length; enemyType++)
                    {
                        _numberOfEnemiesToDefeat += waveData[_currentWave].waveData[enemyType];
                    }
                    Debug.Log(_numberOfEnemiesToDefeat);
                }


                //cycle around the enemies to spawn and check if it can be spawned
                for (int enemyType = 0; enemyType < enemiestoSpawn.Length; enemyType++)
                {
                    // instead of 1 int for all enemies, get three ints for each type of enemy
                    if (waveData[_currentWave].waveData[enemyType] > 0 && spawningTimes[enemyType] <= 0.0f)
                    {
                        var spawnData = enemiestoSpawn[enemyType];

                        //In this example every kind of enemy generates the same list of EntityViews
                        //therefore I always use the same EntityDescriptor. However if the 
                        //different enemies had to create different EntityViews for different
                        //engines, this would have been a good example where EntityDescriptorHolder
                        //could have been used to exploit the the kind of polymorphism explained
                        //in my articles.
                        var EnemyAttackComponent = new EnemyAttackComponent
                        {
                            attackDamage = enemyAttackData[enemyType].enemyAttackData.attackDamage
                          ,
                            timeBetweenAttack = enemyAttackData[enemyType].enemyAttackData.timeBetweenAttacks
                        };

                        //have we got a compatible entity previously disabled and can it be reused?
                        //Note, pooling make sense only for Entities that use implementors.
                        //A pure struct based entity doesn't need pooling because it never allocates.
                        //to simplify the logic, we use a recycle group for each entity type
                        //For the sake of this example, we don't need a different enemy group for each enemy type
                        //however since we need to fetch the right prefab from the pool (because of the graphic)
                        //using a range group for pooling helps.
                        var fromGroupId = ECSGroups.EnemiesToRecycleGroups + (uint)spawnData.enemySpawnData.targetType;

                        if (entitiesDB.HasAny<EnemyEntityViewComponent>(fromGroupId))
                            ReuseEnemy(fromGroupId, spawnData);
                        else
                        {
                            var build = _enemyFactory.Build(spawnData.enemySpawnData, EnemyAttackComponent);
                            while (build.MoveNext())
                                yield return null;
                        }

                        spawningTimes[enemyType] = spawnData.enemySpawnData.spawnTime;
                        waveData[_currentWave].waveData[enemyType]--;
                    }

                    spawningTimes[enemyType] -= SECONDS_BETWEEN_SPAWNS;
                }
            }
        }

        /// <summary>
        ///     Reset all the component values when an Enemy is ready to be recycled.
        ///     it's important to not forget to reset all the states.
        ///     note that the only reason why we pool it the entities here is to reuse the implementors,
        ///     pure entity structs entities do not need pool and can be just recreated
        /// </summary>
        /// <param name="spawnData"></param>
        /// <returns></returns>
        void ReuseEnemy(ExclusiveGroupStruct fromGroupId, JSonEnemySpawnData spawnData)
        {
            Svelto.Console.LogDebug("reuse enemy " + spawnData.enemySpawnData.enemyPrefab);

            var (healths, enemyViews, count) =
                entitiesDB.QueryEntities<HealthComponent, EnemyEntityViewComponent>(fromGroupId);

            if (count > 0)
            {
                healths[0].currentHealth = 100;

                var spawnInfo = spawnData.enemySpawnData.spawnPoint;

                enemyViews[0].transformComponent.position = spawnInfo;
                enemyViews[0].movementComponent.navMeshEnabled = true;
                enemyViews[0].movementComponent.setCapsuleAsTrigger = false;
                enemyViews[0].layerComponent.layer = GAME_LAYERS.ENEMY_LAYER;
                enemyViews[0].animationComponent.reset = true;

                _entityFunctions.SwapEntityGroup<EnemyEntityDescriptor>(enemyViews[0].ID, AliveEnemies.BuildGroup);
            }
        }

        static IEnumerator<JSonEnemySpawnData[]> ReadEnemySpawningDataServiceRequest()
        {
            var json = Addressables.LoadAssetAsync<TextAsset>("EnemySpawningData");

            while (json.IsDone == false)
                yield return null;

            var enemiestoSpawn = JsonHelper.getJsonArray<JSonEnemySpawnData>(json.Result.text);

            yield return enemiestoSpawn;
        }

        static IEnumerator<JSonEnemyAttackData[]> ReadEnemyAttackDataServiceRequest()
        {
            var json = Addressables.LoadAssetAsync<TextAsset>("EnemyAttackData");

            while (json.IsDone == false)
                yield return null;

            var enemiesAttackData = JsonHelper.getJsonArray<JSonEnemyAttackData>(json.Result.text);

            yield return enemiesAttackData;
        }

        static IEnumerator<JSonWaveData[]> ReadWaveDataServiceRequest()
        {
            var json = Addressables.LoadAssetAsync<TextAsset>("WaveData");

            while (json.IsDone == false)
                yield return null;

            var waveData = JsonHelper.getJsonArray<JSonWaveData>(json.Result.text);

            yield return waveData;
        }

        readonly EnemyFactory _enemyFactory;
        readonly IEntityFunctions _entityFunctions;

        IEnumerator _intervaledTick;

        int _currentWave;
        int _numberOfEnemiesToDefeat;
    }
}