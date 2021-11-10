using System;

namespace Svelto.ECS.Example.Survive
{
    [Serializable]
    public class JSonWaveData
    {
        public int[] waveData;

        public JSonWaveData(int[] _waveData) { waveData = _waveData; }
    }

}
